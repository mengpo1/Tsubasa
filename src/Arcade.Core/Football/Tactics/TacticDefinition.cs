using Arcade.Core.Content;

namespace Arcade.Core.Football.Tactics;

/// <summary>Set positions that do not depend on the ball zone (Kick Off 2 tactic files store eight of them).</summary>
public enum TacticSituation
{
    KickOffOwn,
    KickOffOpponent,
    GoalKickOwn,
    GoalKickOpponent,
    CornerOwnLeft,
    CornerOwnRight,
    CornerOpponentLeft,
    CornerOpponentRight,
}

/// <summary>
/// A tactic in the Kick Off 2 model (<c>tactics/&lt;id&gt;.json</c>, or an original <c>.tac</c> file):
/// for each of the 10 outfield players, one position per <b>ball zone</b> (12 zones: 3 columns × 4 rows) plus
/// 8 set situations. The keeper is handled separately. Players go to the position of the zone the ball is in;
/// positions are not interpolated (as in both reference implementations, see REFERENCE_ANALYSIS.md).
/// <para>
/// Coordinates are in tactic units (<see cref="Units"/>; 912 × 1392 = 12 units per yard in Kick Off 2), origin at the
/// team's own left corner flag, x towards the right touchline, y towards the opponent goal.
/// The optional <see cref="TacticPlayer.Attack"/> set (Final Whistle) is used while the team has the ball.
/// </para>
/// </summary>
public sealed record TacticDefinition : IContentData
{
    public const string Folder = "tactics";
    public const int OutfieldPlayers = 10;
    public const int ZoneColumns = 3;
    public const int ZoneRows = 4;
    public const int Zones = ZoneColumns * ZoneRows;

    public required string Id { get; init; }

    /// <summary>Localisation key of the name; imported files have none and show <see cref="Id"/>.</summary>
    public string? NameKey { get; init; }

    public required TacticUnits Units { get; init; }

    public required IReadOnlyList<TacticPlayer> Players { get; init; }

    /// <summary>
    /// Zone of a point given as fractions of the pitch in the team frame (0..1 from the left touchline,
    /// 0..1 from the own goal line). Zones are numbered by column (left, centre, right), then by row from the own goal.
    /// </summary>
    public static int ZoneOf(float acrossFraction, float upFraction)
    {
        int column = Math.Clamp((int)MathF.Floor(acrossFraction * ZoneColumns), 0, ZoneColumns - 1);
        int row = Math.Clamp((int)MathF.Floor(upFraction * ZoneRows), 0, ZoneRows - 1);
        return (column * ZoneRows) + row;
    }

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.Nested(problems, "units", Units);
        if (Players.Count != OutfieldPlayers)
        {
            problems.Add($"players must contain the {OutfieldPlayers} outfield players (found {Players.Count})");
        }

        for (int i = 0; i < Players.Count; i++)
        {
            var inner = new List<string>();
            Players[i].Validate(inner, Units);
            foreach (string problem in inner)
            {
                problems.Add($"players[{i}].{problem}");
            }
        }
    }
}

public sealed record TacticUnits : IContentData
{
    /// <summary>Size of the coordinate space across the pitch (touchline to touchline).</summary>
    public required float Width { get; init; }

    /// <summary>Size of the coordinate space along the pitch (goal line to goal line).</summary>
    public required float Length { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "width", Width);
        DataChecks.Positive(problems, "length", Length);
    }
}

/// <summary>Positions of one outfield player: [x, y] pairs in tactic units.</summary>
public sealed record TacticPlayer
{
    /// <summary>Position for each ball zone (Kick Off 2: regardless of possession; Final Whistle: when defending).</summary>
    public required IReadOnlyList<IReadOnlyList<float>> Defend { get; init; }

    /// <summary>Optional positions while the team has the ball (Final Whistle). Defaults to <see cref="Defend"/>.</summary>
    public IReadOnlyList<IReadOnlyList<float>>? Attack { get; init; }

    public required IReadOnlyList<float> KickOffOwn { get; init; }

    public required IReadOnlyList<float> KickOffOpponent { get; init; }

    public required IReadOnlyList<float> GoalKickOwn { get; init; }

    public required IReadOnlyList<float> GoalKickOpponent { get; init; }

    public required IReadOnlyList<float> CornerOwnLeft { get; init; }

    public required IReadOnlyList<float> CornerOwnRight { get; init; }

    public required IReadOnlyList<float> CornerOpponentLeft { get; init; }

    public required IReadOnlyList<float> CornerOpponentRight { get; init; }

    public IReadOnlyList<float> ZonePosition(int zone, bool attacking) => (attacking && Attack is not null ? Attack : Defend)[zone];

    public IReadOnlyList<float> Situation(TacticSituation situation) => situation switch
    {
        TacticSituation.KickOffOwn => KickOffOwn,
        TacticSituation.KickOffOpponent => KickOffOpponent,
        TacticSituation.GoalKickOwn => GoalKickOwn,
        TacticSituation.GoalKickOpponent => GoalKickOpponent,
        TacticSituation.CornerOwnLeft => CornerOwnLeft,
        TacticSituation.CornerOwnRight => CornerOwnRight,
        TacticSituation.CornerOpponentLeft => CornerOpponentLeft,
        _ => CornerOpponentRight,
    };

    internal void Validate(ICollection<string> problems, TacticUnits units)
    {
        CheckZones(problems, "defend", Defend, units);
        if (Attack is not null)
        {
            CheckZones(problems, "attack", Attack, units);
        }

        foreach (TacticSituation situation in Enum.GetValues<TacticSituation>())
        {
            CheckPoint(problems, char.ToLowerInvariant(situation.ToString()[0]) + situation.ToString()[1..], Situation(situation), units);
        }
    }

    private static void CheckZones(ICollection<string> problems, string name, IReadOnlyList<IReadOnlyList<float>> zones, TacticUnits units)
    {
        if (zones.Count != TacticDefinition.Zones)
        {
            problems.Add($"{name} must contain {TacticDefinition.Zones} positions (found {zones.Count})");
            return;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            CheckPoint(problems, $"{name}[{i}]", zones[i], units);
        }
    }

    private static void CheckPoint(ICollection<string> problems, string name, IReadOnlyList<float>? point, TacticUnits units)
    {
        if (point is null || point.Count != 2 || point[0] < 0f || point[0] > units.Width || point[1] < 0f || point[1] > units.Length)
        {
            problems.Add($"{name} must be [x, y] inside 0..{units.Width} × 0..{units.Length}");
        }
    }
}
