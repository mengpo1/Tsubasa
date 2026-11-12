using Arcade.Core.Content;
using Arcade.Core.Football.Teams;

namespace Arcade.Core.Football.Tactics;

/// <summary>
/// A formation (<c>tactics/&lt;id&gt;.json</c>). Positions are normalised in the attacking frame of the team:
/// x from -1 (left touchline) to 1 (right touchline) when attacking upwards, y from -1 (own goal line)
/// to 1 (opponent goal line). Slot i is played by the i-th player of the team file.
/// </summary>
public sealed record TacticDefinition : IContentData
{
    public const string Folder = "tactics";

    public required string Id { get; init; }

    public required string NameKey { get; init; }

    /// <summary>How much the whole block follows the ball (fraction of the ball offset, per axis).</summary>
    public required BallFollow BallFollow { get; init; }

    /// <summary>Extra forward shift (normalised y) of the block while the team has the ball.</summary>
    public required float AttackShift { get; init; }

    public required IReadOnlyList<TacticSlot> Slots { get; init; }

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.NotEmpty(problems, "nameKey", NameKey);
        DataChecks.Nested(problems, "ballFollow", BallFollow);
        DataChecks.InRange(problems, "attackShift", AttackShift, -0.5f, 0.5f);
        if (Slots.Count != 11)
        {
            problems.Add($"slots must contain 11 positions (found {Slots.Count})");
        }

        if (Slots.Count(slot => slot.Role == PlayerRole.Goalkeeper) != 1)
        {
            problems.Add("exactly one slot must have the goalkeeper role");
        }

        if (Slots.Count(slot => slot.KickOffTaker) != 1)
        {
            problems.Add("exactly one slot must be the kick-off taker");
        }

        for (int i = 0; i < Slots.Count; i++)
        {
            DataChecks.Nested(problems, $"slots[{i}]", Slots[i]);
        }
    }
}

public sealed record BallFollow : IContentData
{
    public required float X { get; init; }

    public required float Y { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "x", X, 0f, 1f);
        DataChecks.InRange(problems, "y", Y, 0f, 1f);
    }
}

public sealed record TacticSlot : IContentData
{
    public required PlayerRole Role { get; init; }

    /// <summary>Position with the ball on the centre spot: [x, y].</summary>
    public required IReadOnlyList<float> Home { get; init; }

    /// <summary>Position at kick-off (own half): [x, y].</summary>
    public required IReadOnlyList<float> KickOff { get; init; }

    /// <summary>The slot never goes deeper than this (normalised y).</summary>
    public required float MinY { get; init; }

    /// <summary>The slot never goes further forward than this (normalised y).</summary>
    public required float MaxY { get; init; }

    /// <summary>This slot takes the kick-off.</summary>
    public bool KickOffTaker { get; init; }

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        CheckPoint(problems, "home", Home);
        CheckPoint(problems, "kickOff", KickOff);
        if (KickOff.Count == 2 && KickOff[1] > 0f)
        {
            problems.Add("kickOff must be in the own half (y <= 0)");
        }

        DataChecks.InRange(problems, "minY", MinY, -1f, 1f);
        DataChecks.InRange(problems, "maxY", MaxY, MinY, 1f);
    }

    private static void CheckPoint(ICollection<string> problems, string name, IReadOnlyList<float> point)
    {
        if (point.Count != 2 || point.Any(value => value is < -1f or > 1f))
        {
            problems.Add($"{name} must be [x, y] with values between -1 and 1");
        }
    }
}
