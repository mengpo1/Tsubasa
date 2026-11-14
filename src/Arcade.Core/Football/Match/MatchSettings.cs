namespace Arcade.Core.Football.Match;

/// <summary>A human player: which input control set drives which team (index 0 = home, 1 = away).</summary>
public readonly record struct HumanSlot(int ControlSet, int TeamIndex);

/// <summary>Everything needed to start a match.</summary>
public sealed record MatchSettings
{
    public required string HomeTeamId { get; init; }

    public required string AwayTeamId { get; init; }

    public required string PitchId { get; init; }

    public string RulesetId { get; init; } = "classic";

    /// <summary>AI level id from <c>config/ai.json</c>; null uses the default level.</summary>
    public string? AiLevel { get; init; }

    /// <summary>Overrides the ruleset half length (s), e.g. from the command line.</summary>
    public float? HalfLengthSeconds { get; init; }

    public IReadOnlyList<HumanSlot> Humans { get; init; } = [];

    public bool Aftertouch { get; init; } = true;

    public ulong Seed { get; init; } = 1;
}

/// <summary>Phases of the match state machine.</summary>
public enum MatchPhase
{
    /// <summary>Players walk to their kick-off positions.</summary>
    KickOffSetup,

    /// <summary>The ball is placed and the taker goes to it.</summary>
    RestartSetup,

    /// <summary>Waiting for the taker to kick (kick-off, throw-in, corner, goal kick).</summary>
    RestartReady,

    Playing,

    /// <summary>The ball left the pitch; short pause before the restart.</summary>
    BallOut,

    GoalCelebration,
    HalfTime,
    FullTime,
}
