namespace Arcade.Core.Content;

/// <summary>An engine animation state resolved to an animation of the catalog.</summary>
/// <param name="State">The state that was asked for (e.g. <c>keeper.run</c>).</param>
/// <param name="AnimationId">The animation shown.</param>
/// <param name="UsedState">The state whose mapping provided it: differs from <paramref name="State"/> after a fallback.</param>
/// <param name="FromDefaultLook">True when a theme is active but the animation comes from the default look.</param>
public sealed record ResolvedAnimation(string State, string AnimationId, AnimationDefinition Animation, string UsedState, bool FromDefaultLook);

/// <summary>
/// Maps the renderer's animation states to the animations a catalog provides (<see cref="AssetCatalog.States"/>).
/// A missing state falls back along <see cref="Parents"/> (e.g. <c>dribble</c> → <c>run</c> → <c>idle</c>), first within
/// the active look so that a theme stays visually consistent, then in the default look. Every substitution is reported.
/// Pure data logic: the caller says which textures are actually available.
/// </summary>
public sealed class AnimationStateMap
{
    /// <summary>
    /// States the renderer knows, with the state used when one is missing (null = none, the state is optional).
    /// States marked "reserved" are not emitted yet: they will be when the corresponding actions exist (Lot 3),
    /// and themes may already map them.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Parents { get; } = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["idle"] = null,
        ["run"] = "idle",
        ["dribble"] = "run",
        ["kick"] = "idle",
        ["control"] = "idle",
        ["slide"] = "kick", // reserved
        ["header"] = "kick", // reserved
        ["volley"] = "kick", // reserved (scissor and acrobatic kicks)
        ["celebrate"] = "run", // reserved
        ["keeper.idle"] = "idle",
        ["keeper.run"] = "run",
        ["keeper.kick"] = "kick",
        ["keeper.hold"] = "keeper.idle",
        ["keeper.dive"] = "keeper.idle", // reserved
        ["player.shadow"] = null,
        ["ball"] = null,
        ["ball.high"] = null,
        ["ball.shadow"] = null,
    };

    private readonly Dictionary<string, ResolvedAnimation> _resolved;

    private AnimationStateMap(Dictionary<string, ResolvedAnimation> resolved, IReadOnlyList<string> notes)
    {
        _resolved = resolved;
        Notes = notes;
    }

    /// <summary>Human-readable report of every substitution and missing animation.</summary>
    public IReadOnlyList<string> Notes { get; }

    public IReadOnlyCollection<ResolvedAnimation> All => _resolved.Values;

    /// <summary>Animation for a state, or null when neither the look nor its fallbacks provide one.</summary>
    public ResolvedAnimation? Get(string state) => _resolved.GetValueOrDefault(state);

    /// <param name="catalog">Catalog of the active look (theme merged over the base).</param>
    /// <param name="textureAvailable">Whether a texture id of <paramref name="catalog"/> resolves to a file.</param>
    /// <param name="defaultCatalog">Catalog of the default look when a theme is active, else null.</param>
    /// <param name="defaultTextureAvailable">Same as <paramref name="textureAvailable"/> for the default look.</param>
    public static AnimationStateMap Resolve(
        AssetCatalog catalog,
        Func<string, bool> textureAvailable,
        AssetCatalog? defaultCatalog = null,
        Func<string, bool>? defaultTextureAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(textureAvailable);
        var notes = new List<string>();
        var resolved = new Dictionary<string, ResolvedAnimation>(StringComparer.Ordinal);
        IEnumerable<string> states = Parents.Keys.Concat(catalog.States.Keys).Distinct(StringComparer.Ordinal);
        foreach (string state in states)
        {
            var skipped = new List<string>();
            ResolvedAnimation? found = Walk(state, catalog, textureAvailable, fromDefault: false, skipped)
                ?? (defaultCatalog is null ? null : Walk(state, defaultCatalog, defaultTextureAvailable ?? (_ => true), fromDefault: true, skipped));
            notes.AddRange(skipped.Distinct(StringComparer.Ordinal));
            if (found is null)
            {
                if (!IsOptional(state))
                {
                    notes.Add($"state '{state}': no animation available");
                }

                continue;
            }

            resolved[state] = found;
            if (found.UsedState != state)
            {
                notes.Add($"state '{state}': shown with '{found.UsedState}' ({found.AnimationId})");
            }

            if (found.FromDefaultLook)
            {
                notes.Add($"state '{state}': default look used ({found.AnimationId})");
            }
            else if (defaultCatalog is not null && found.UsedState == state && SameMapping(catalog, defaultCatalog, state))
            {
                notes.Add($"state '{state}': not mapped by the theme, default mapping used ({found.AnimationId})");
            }
        }

        return new AnimationStateMap(resolved, notes);
    }

    private static ResolvedAnimation? Walk(string state, AssetCatalog catalog, Func<string, bool> available, bool fromDefault, List<string> skipped)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (string? current = state; current is not null && visited.Add(current); current = Parents.GetValueOrDefault(current))
        {
            if (!catalog.States.TryGetValue(current, out IReadOnlyList<string>? candidates))
            {
                continue;
            }

            foreach (string id in candidates)
            {
                if (!catalog.Animations.TryGetValue(id, out AnimationDefinition? animation))
                {
                    skipped.Add($"state '{current}': unknown animation '{id}'");
                }
                else if (!available(animation.Texture))
                {
                    skipped.Add($"state '{current}': animation '{id}' unavailable (texture '{animation.Texture}' missing)");
                }
                else
                {
                    return new ResolvedAnimation(state, id, animation, current, fromDefault);
                }
            }
        }

        return null;
    }

    private static bool IsOptional(string state) => Parents.TryGetValue(state, out string? parent) && parent is null && state != "idle" && state != "ball";

    private static bool SameMapping(AssetCatalog a, AssetCatalog b, string state) =>
        a.States.TryGetValue(state, out IReadOnlyList<string>? x) && b.States.TryGetValue(state, out IReadOnlyList<string>? y) && x.SequenceEqual(y, StringComparer.Ordinal);
}
