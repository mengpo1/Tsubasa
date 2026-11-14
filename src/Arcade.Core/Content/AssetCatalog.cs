using System.Text.Json.Serialization;

namespace Arcade.Core.Content;

/// <summary>
/// Maps logical asset identifiers (e.g. <c>ball.default</c>) to files and describes animations.
/// Gameplay code only ever uses identifiers; the desktop layer resolves them to textures.
/// Loaded from <c>assets.json</c>, merged across packs key by key.
/// </summary>
public sealed record AssetCatalog : IContentData
{
    public const string FileName = "assets.json";

    [JsonRequired]
    public IReadOnlyDictionary<string, string> Textures { get; init; } = new Dictionary<string, string>();

    [JsonRequired]
    public IReadOnlyDictionary<string, string> Fonts { get; init; } = new Dictionary<string, string>();

    [JsonRequired]
    public IReadOnlyDictionary<string, AnimationDefinition> Animations { get; init; } = new Dictionary<string, AnimationDefinition>();

    /// <summary>
    /// Engine animation states (e.g. <c>run</c>, <c>kick</c>, <c>keeper.run</c>) mapped to candidate animation ids, in
    /// order of preference. The renderer uses the first one available and reports the missing ones.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> States { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        foreach ((string id, string path) in Textures)
        {
            if (!ContentPath.TryNormalize(path, out _, out string? error))
            {
                problems.Add($"textures.{id}: invalid path '{path}' ({error})");
            }
        }

        foreach ((string id, string path) in Fonts)
        {
            if (!ContentPath.TryNormalize(path, out _, out string? error))
            {
                problems.Add($"fonts.{id}: invalid path '{path}' ({error})");
            }
        }

        foreach ((string id, AnimationDefinition animation) in Animations)
        {
            DataChecks.Nested(problems, $"animations.{id}", animation);
            if (!Textures.ContainsKey(animation.Texture))
            {
                problems.Add($"animations.{id}.texture: unknown texture id '{animation.Texture}'");
            }
        }

        foreach ((string state, IReadOnlyList<string> candidates) in States)
        {
            if (candidates.Count == 0)
            {
                problems.Add($"states.{state}: list at least one animation id");
            }
        }
    }
}

/// <summary>How the current frame of an animation is chosen.</summary>
public enum AnimationDriver
{
    /// <summary>Distance when <see cref="AnimationDefinition.ReferenceSpeed"/> is set, otherwise time.</summary>
    Auto,
    Time,
    Distance,

    /// <summary>By the displayed height of the entity (ball sprites of the original game).</summary>
    Height,
}

/// <summary>
/// A sprite-sheet animation. The sheet is a grid of frames of <see cref="FrameWidth"/> x <see cref="FrameHeight"/> pixels.
/// Two layouts are supported:
/// <list type="bullet">
/// <item>rows (default): when <see cref="Directions"/> is 8, row <c>r</c> holds the frames for direction <c>r</c>
/// (0 = up, then clockwise: up-right, right, down-right, down, down-left, left, up-left), and <see cref="Frames"/> are columns;</item>
/// <item>indexed (<see cref="DirectionStride"/> &gt; 0, as in the original Kick Off 2 sheets): frames are numbered across
/// the grid, row by row, and the frame of direction <c>d</c> is <c>frame + d × DirectionStride</c>.</item>
/// </list>
/// </summary>
public sealed record AnimationDefinition : IContentData
{
    [JsonRequired]
    public string Texture { get; init; } = string.Empty;

    [JsonRequired]
    public int FrameWidth { get; init; }

    [JsonRequired]
    public int FrameHeight { get; init; }

    /// <summary>1 (no direction) or 8.</summary>
    [JsonRequired]
    public int Directions { get; init; }

    /// <summary>Column indices of the frames, in playing order.</summary>
    [JsonRequired]
    public IReadOnlyList<int> Frames { get; init; } = [];

    /// <summary>Seconds per frame at the reference speed.</summary>
    [JsonRequired]
    public float FrameDuration { get; init; }

    /// <summary>
    /// Optional: movement speed (m/s) at which <see cref="FrameDuration"/> applies; the animation then plays
    /// proportionally to the entity speed so that feet match the ground. 0 = fixed rate.
    /// </summary>
    public float ReferenceSpeed { get; init; }

    /// <summary>Pixel inside a frame that is placed on the entity's ground position (usually between the feet).</summary>
    [JsonRequired]
    public IReadOnlyList<int> Anchor { get; init; } = [];

    /// <summary>Optional extra display offset in pixels.</summary>
    public IReadOnlyList<int> Offset { get; init; } = [0, 0];

    /// <summary>
    /// Sprite pixels per metre of the sheet (0 = the display's <c>pixelsPerMetre</c>). Lets sprites drawn for another
    /// scale (e.g. the default look shown as a replacement inside a theme) keep their size relative to the pitch.
    /// </summary>
    public float PixelsPerMetre { get; init; }

    /// <summary>Indexed layout: frame-number offset between two directions (0 = rows layout).</summary>
    public int DirectionStride { get; init; }

    /// <summary>Indexed layout: frames per sheet row (0 = texture width / frame width).</summary>
    public int SheetColumns { get; init; }

    public AnimationDriver Driver { get; init; }

    /// <summary>Height driver: displayed height in pixels per frame step.</summary>
    public float HeightStep { get; init; } = 1f;

    /// <summary>
    /// Optional palette swap: marker colours (hex, e.g. "#FF0000") replaced at load time by a kit colour slot
    /// ("shirt", "shorts", "socks"), with an optional brightness factor. Lets one sheet serve every team.
    /// </summary>
    public IReadOnlyList<PaletteSwap> Palette { get; init; } = [];

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (FrameWidth <= 0 || FrameHeight <= 0)
        {
            problems.Add("frameWidth and frameHeight must be positive");
        }

        if (Directions is not (1 or 8))
        {
            problems.Add("directions must be 1 or 8");
        }

        if (Frames.Count == 0 || Frames.Any(frame => frame < 0))
        {
            problems.Add("frames must list at least one non-negative column index");
        }

        DataChecks.Positive(problems, "frameDuration", FrameDuration);
        DataChecks.NonNegative(problems, "referenceSpeed", ReferenceSpeed);
        DataChecks.NonNegative(problems, "pixelsPerMetre", PixelsPerMetre);
        DataChecks.NonNegative(problems, "directionStride", DirectionStride);
        DataChecks.NonNegative(problems, "sheetColumns", SheetColumns);
        DataChecks.Positive(problems, "heightStep", HeightStep);
        if (Anchor.Count != 2)
        {
            problems.Add("anchor must be [x, y]");
        }

        if (Offset.Count != 2)
        {
            problems.Add("offset must be [x, y]");
        }

        foreach (PaletteSwap swap in Palette)
        {
            if (!ColorRgb.TryParse(swap.Marker, out _))
            {
                problems.Add($"palette marker '{swap.Marker}' is not a #RRGGBB colour");
            }

            if (swap.Slot is not ("shirt" or "shorts" or "socks"))
            {
                problems.Add($"palette slot '{swap.Slot}' must be shirt, shorts or socks");
            }
        }
    }
}

public sealed record PaletteSwap
{
    [JsonRequired]
    public string Marker { get; init; } = string.Empty;

    [JsonRequired]
    public string Slot { get; init; } = string.Empty;

    public float Brightness { get; init; } = 1f;
}

/// <summary>Colour parsed from "#RRGGBB" data strings. Kept here so data validation does not depend on the renderer.</summary>
public readonly record struct ColorRgb(byte R, byte G, byte B)
{
    public static bool TryParse(string? text, out ColorRgb color)
    {
        color = default;
        if (text is null || text.Length != 7 || text[0] != '#')
        {
            return false;
        }

        if (!uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint value))
        {
            return false;
        }

        color = new ColorRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static ColorRgb Parse(string text) =>
        TryParse(text, out ColorRgb color) ? color : throw new ContentException($"'{text}' is not a #RRGGBB colour.");

    public ColorRgb Scale(float factor) => new(
        (byte)Math.Clamp(R * factor, 0f, 255f),
        (byte)Math.Clamp(G * factor, 0f, 255f),
        (byte)Math.Clamp(B * factor, 0f, 255f));
}
