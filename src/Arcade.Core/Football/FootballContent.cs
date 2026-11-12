using Arcade.Core.Content;
using Arcade.Core.Football.AI;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Tactics;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Teams;

namespace Arcade.Core.Football;

/// <summary>All football data, loaded and validated from the layered content (base + mods).</summary>
public sealed class FootballContent
{
    private FootballContent(
        PhysicsConfig physics,
        ControlConfig controls,
        PlayerRulesConfig playerRules,
        IReadOnlyDictionary<string, SurfaceDefinition> surfaces,
        IReadOnlyDictionary<string, PitchDefinition> pitches,
        IReadOnlyDictionary<string, TeamDefinition> teams,
        IReadOnlyDictionary<string, TacticDefinition> tactics,
        IReadOnlyDictionary<string, RulesetDefinition> rulesets,
        AiConfig ai)
    {
        Tactics = tactics;
        Rulesets = rulesets;
        Ai = ai;
        Physics = physics;
        Controls = controls;
        PlayerRules = playerRules;
        Surfaces = surfaces;
        Pitches = pitches;
        Teams = teams;
    }

    public PhysicsConfig Physics { get; }

    public ControlConfig Controls { get; }

    public PlayerRulesConfig PlayerRules { get; }

    public IReadOnlyDictionary<string, SurfaceDefinition> Surfaces { get; }

    public IReadOnlyDictionary<string, PitchDefinition> Pitches { get; }

    public IReadOnlyDictionary<string, TeamDefinition> Teams { get; }

    public IReadOnlyDictionary<string, TacticDefinition> Tactics { get; }

    public IReadOnlyDictionary<string, RulesetDefinition> Rulesets { get; }

    public AiConfig Ai { get; }

    /// <summary>
    /// Returns a copy with some configuration replaced: match options (e.g. fatigue off) and tests use it.
    /// </summary>
    public FootballContent With(PhysicsConfig? physics = null, ControlConfig? controls = null, PlayerRulesConfig? playerRules = null) =>
        new(physics ?? Physics, controls ?? Controls, playerRules ?? PlayerRules, Surfaces, Pitches, Teams, Tactics, Rulesets, Ai);

    /// <summary>Copy with other match flow settings (tests, quick matches).</summary>
    public FootballContent WithRuleset(RulesetDefinition ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var rulesets = new Dictionary<string, RulesetDefinition>(Rulesets, StringComparer.Ordinal) { [ruleset.Id] = ruleset };
        return new(Physics, Controls, PlayerRules, Surfaces, Pitches, Teams, Tactics, rulesets, Ai);
    }

    public static FootballContent Load(ContentRepository content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var physics = content.LoadData<PhysicsConfig>(PhysicsConfig.FileName);
        var controls = content.LoadData<ControlConfig>(ControlConfig.FileName);
        var playerRules = content.LoadData<PlayerRulesConfig>(PlayerRulesConfig.FileName);
        var surfaces = LoadCollection<SurfaceDefinition>(content, SurfaceDefinition.Folder, surface => surface.Id);
        var pitches = LoadCollection<PitchDefinition>(content, PitchDefinition.Folder, pitch => pitch.Id);
        var teams = LoadCollection<TeamDefinition>(content, TeamDefinition.Folder, team => team.Id);
        var tactics = LoadCollection<TacticDefinition>(content, TacticDefinition.Folder, tactic => tactic.Id);
        var rulesets = LoadCollection<RulesetDefinition>(content, RulesetDefinition.Folder, ruleset => ruleset.Id);
        var ai = content.LoadData<AiConfig>(AiConfig.FileName);

        foreach (PitchDefinition pitch in pitches.Values)
        {
            if (!surfaces.ContainsKey(pitch.Surface))
            {
                throw new ContentException($"Pitch '{pitch.Id}' uses unknown surface '{pitch.Surface}' (known: {string.Join(", ", surfaces.Keys)}).");
            }
        }

        foreach (TeamDefinition team in teams.Values)
        {
            if (!tactics.ContainsKey(team.Tactic))
            {
                throw new ContentException($"Team '{team.Id}' uses unknown tactic '{team.Tactic}' (known: {string.Join(", ", tactics.Keys)}).");
            }
        }

        if (teams.Count == 0 || pitches.Count == 0 || rulesets.Count == 0)
        {
            throw new ContentException("At least one team, one pitch and one ruleset are required.");
        }

        return new FootballContent(physics, controls, playerRules, surfaces, pitches, teams, tactics, rulesets, ai);
    }

    private static Dictionary<string, T> LoadCollection<T>(ContentRepository content, string folder, Func<T, string> idOf)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (string file in content.ListFiles(folder, ".json"))
        {
            T item = content.LoadData<T>(file);
            string id = idOf(item);
            string expected = Path.GetFileNameWithoutExtension(file);
            if (!string.Equals(id, expected, StringComparison.Ordinal))
            {
                throw new ContentException($"'{file}' declares id '{id}' but its file name requires '{expected}'.");
            }

            result[id] = item;
        }

        return result;
    }
}
