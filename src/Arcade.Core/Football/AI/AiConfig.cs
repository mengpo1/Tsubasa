using Arcade.Core.Content;

namespace Arcade.Core.Football.AI;

/// <summary>Settings of the computer players (<c>config/ai.json</c>).</summary>
public sealed record AiConfig : IContentData
{
    public const string FileName = "config/ai.json";

    public required string DefaultLevel { get; init; }

    public required IReadOnlyDictionary<string, AiLevel> Levels { get; init; }

    public required AiPositioning Positioning { get; init; }

    public required AiKeeper Keeper { get; init; }

    public AiLevel Level(string? id) =>
        Levels.TryGetValue(id ?? DefaultLevel, out AiLevel? level)
            ? level
            : throw new ContentException($"Unknown AI level '{id}' (known: {string.Join(", ", Levels.Keys)}).");

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (!Levels.ContainsKey(DefaultLevel))
        {
            problems.Add($"defaultLevel '{DefaultLevel}' is not one of the levels");
        }

        foreach ((string id, AiLevel level) in Levels)
        {
            DataChecks.Nested(problems, $"levels.{id}", level);
        }

        DataChecks.Nested(problems, "positioning", Positioning);
        DataChecks.Nested(problems, "keeper", Keeper);
    }
}

/// <summary>A difficulty level: reaction and accuracy, never physics bonuses.</summary>
public sealed record AiLevel : IContentData
{
    /// <summary>Time between two re-evaluations of a player's target (s).</summary>
    public required float DecisionInterval { get; init; }

    /// <summary>Delay before reacting to a kick (s).</summary>
    public required float ReactionSeconds { get; init; }

    /// <summary>Number of players who chase the ball when the team does not have it.</summary>
    public required int Chasers { get; init; }

    /// <summary>Maximum distance to the goal centre for a shot (m).</summary>
    public required float ShootRange { get; init; }

    /// <summary>Maximum aiming error of a shot by the worst shooter (degrees); the best shooters halve it.</summary>
    public required float ShotErrorDegrees { get; init; }

    public required float PassErrorDegrees { get; init; }

    /// <summary>Time a player keeps a stopped ball before acting (s).</summary>
    public required float HoldSeconds { get; init; }

    /// <summary>Time before the computer takes a restart (s).</summary>
    public required float RestartDelaySeconds { get; init; }

    public required float KeeperReactionSeconds { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "decisionInterval", DecisionInterval);
        DataChecks.NonNegative(problems, "reactionSeconds", ReactionSeconds);
        DataChecks.InRange(problems, "chasers", Chasers, 1, 4);
        DataChecks.Positive(problems, "shootRange", ShootRange);
        DataChecks.InRange(problems, "shotErrorDegrees", ShotErrorDegrees, 0f, 45f);
        DataChecks.InRange(problems, "passErrorDegrees", PassErrorDegrees, 0f, 45f);
        DataChecks.NonNegative(problems, "holdSeconds", HoldSeconds);
        DataChecks.NonNegative(problems, "restartDelaySeconds", RestartDelaySeconds);
        DataChecks.NonNegative(problems, "keeperReactionSeconds", KeeperReactionSeconds);
    }
}

public sealed record AiPositioning : IContentData
{
    /// <summary>A player within this distance of his target stops (m).</summary>
    public required float ArriveRadius { get; init; }

    /// <summary>An opponent closer than this puts the ball carrier under pressure (m).</summary>
    public required float PressureRadius { get; init; }

    public required float MinPassDistance { get; init; }

    public required float MaxPassDistance { get; init; }

    /// <summary>Opponents closer than this to a pass line make it unsafe (m).</summary>
    public required float LaneClearance { get; init; }

    /// <summary>Minimum score of a pass option (higher = fewer, safer passes).</summary>
    public required float PassScoreThreshold { get; init; }

    /// <summary>Length of the look-ahead when choosing a dribbling direction (m).</summary>
    public required float DribbleProbe { get; init; }

    /// <summary>Dribbling directions closer than this to a touchline are avoided (m).</summary>
    public required float TouchlineMargin { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "arriveRadius", ArriveRadius);
        DataChecks.Positive(problems, "pressureRadius", PressureRadius);
        DataChecks.Positive(problems, "minPassDistance", MinPassDistance);
        DataChecks.InRange(problems, "maxPassDistance", MaxPassDistance, MinPassDistance, 100f);
        DataChecks.NonNegative(problems, "laneClearance", LaneClearance);
        DataChecks.NonNegative(problems, "passScoreThreshold", PassScoreThreshold);
        DataChecks.Positive(problems, "dribbleProbe", DribbleProbe);
        DataChecks.NonNegative(problems, "touchlineMargin", TouchlineMargin);
    }
}

/// <summary>Elementary goalkeeper behaviour (Lot 2).</summary>
public sealed record AiKeeper : IContentData
{
    /// <summary>Closest distance to the goal line (m).</summary>
    public required float MinLineDistance { get; init; }

    /// <summary>Furthest distance from the goal line (m).</summary>
    public required float MaxLineDistance { get; init; }

    /// <summary>How far the keeper comes out per metre of ball distance.</summary>
    public required float AdvanceFactor { get; init; }

    /// <summary>A loose ball slower than this in the penalty area is collected by the keeper (m/s).</summary>
    public required float RushBallSpeed { get; init; }

    public required float HoldSeconds { get; init; }

    public required float KickSpeed { get; init; }

    public required float KickLift { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NonNegative(problems, "minLineDistance", MinLineDistance);
        DataChecks.InRange(problems, "maxLineDistance", MaxLineDistance, MinLineDistance, 20f);
        DataChecks.NonNegative(problems, "advanceFactor", AdvanceFactor);
        DataChecks.NonNegative(problems, "rushBallSpeed", RushBallSpeed);
        DataChecks.NonNegative(problems, "holdSeconds", HoldSeconds);
        DataChecks.Positive(problems, "kickSpeed", KickSpeed);
        DataChecks.NonNegative(problems, "kickLift", KickLift);
    }
}
