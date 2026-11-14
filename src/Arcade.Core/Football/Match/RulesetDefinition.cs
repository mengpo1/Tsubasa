using Arcade.Core.Content;

namespace Arcade.Core.Football.Match;

/// <summary>Match flow settings (<c>rulesets/&lt;id&gt;.json</c>). Times are real simulation seconds.</summary>
public sealed record RulesetDefinition : IContentData
{
    public const string Folder = "rulesets";

    public required string Id { get; init; }

    public required string NameKey { get; init; }

    /// <summary>Real length of a half (s). Kick Off 2 offered 3, 5, 10 and 20 minutes.</summary>
    public required float HalfLengthSeconds { get; init; }

    /// <summary>Minutes shown on the clock for a half (45 for a 90-minute match display).</summary>
    public required float DisplayedHalfMinutes { get; init; }

    public required float GoalCelebrationSeconds { get; init; }

    /// <summary>Delay between the ball leaving the pitch and the restart being set up.</summary>
    public required float BallOutWhistleSeconds { get; init; }

    public required float HalfTimeSeconds { get; init; }

    /// <summary>Time given to players to reach their kick-off positions before stragglers are placed.</summary>
    public required float KickOffSetupSeconds { get; init; }

    /// <summary>Time given to the taker to reach the ball before he is placed.</summary>
    public required float RestartSetupSeconds { get; init; }

    /// <summary>A human who has not pressed fire after this time lets the computer take the restart.</summary>
    public required float HumanRestartAutoSeconds { get; init; }

    /// <summary>Minimum distance of opponents at kick-offs, corners and goal kicks (m).</summary>
    public required float RestartDistance { get; init; }

    /// <summary>Minimum distance of opponents at throw-ins (m).</summary>
    public required float ThrowInDistance { get; init; }

    public required bool SwitchEnds { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.NotEmpty(problems, "nameKey", NameKey);
        DataChecks.InRange(problems, "halfLengthSeconds", HalfLengthSeconds, 10f, 7200f);
        DataChecks.InRange(problems, "displayedHalfMinutes", DisplayedHalfMinutes, 1f, 90f);
        DataChecks.NonNegative(problems, "goalCelebrationSeconds", GoalCelebrationSeconds);
        DataChecks.NonNegative(problems, "ballOutWhistleSeconds", BallOutWhistleSeconds);
        DataChecks.NonNegative(problems, "halfTimeSeconds", HalfTimeSeconds);
        DataChecks.NonNegative(problems, "kickOffSetupSeconds", KickOffSetupSeconds);
        DataChecks.NonNegative(problems, "restartSetupSeconds", RestartSetupSeconds);
        DataChecks.Positive(problems, "humanRestartAutoSeconds", HumanRestartAutoSeconds);
        DataChecks.NonNegative(problems, "restartDistance", RestartDistance);
        DataChecks.NonNegative(problems, "throwInDistance", ThrowInDistance);
    }
}
