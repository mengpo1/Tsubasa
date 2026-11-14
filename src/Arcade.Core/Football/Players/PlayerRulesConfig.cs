using Arcade.Core.Content;

namespace Arcade.Core.Football.Players;

/// <summary>How attributes and fatigue affect play (<c>config/players.json</c>).</summary>
public sealed record PlayerRulesConfig : IContentData
{
    public const string FileName = "config/players.json";

    /// <summary>Range of the attribute values written in team files (e.g. 0..100). Values are normalised to 0..1.</summary>
    public required RatingScale RatingScale { get; init; }

    public required AttributeEffects Effects { get; init; }

    public required FatigueConfig Fatigue { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Nested(problems, "ratingScale", RatingScale);
        DataChecks.Nested(problems, "effects", Effects);
        DataChecks.Nested(problems, "fatigue", Fatigue);
    }
}

public sealed record RatingScale : IContentData
{
    public required float Min { get; init; }

    public required float Max { get; init; }

    public float Normalize(float value) => Math.Clamp((value - Min) / (Max - Min), 0f, 1f);

    public void Validate(ICollection<string> problems)
    {
        if (!(Max > Min))
        {
            problems.Add("max must be greater than min");
        }
    }
}

/// <summary>A multiplier interpolated between its value for the worst rating and for the best rating.</summary>
public sealed record RatingRange : IContentData
{
    public required float AtMin { get; init; }

    public required float AtMax { get; init; }

    public float Evaluate(float normalizedRating) => Common.VectorMath.FromRating(normalizedRating, AtMin, AtMax);

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NonNegative(problems, "atMin", AtMin);
        DataChecks.NonNegative(problems, "atMax", AtMax);
    }
}

public sealed record AttributeEffects : IContentData
{
    /// <summary>Pace → top speed multiplier.</summary>
    public required RatingRange PaceSpeed { get; init; }

    /// <summary>Pace → acceleration multiplier.</summary>
    public required RatingRange PaceAcceleration { get; init; }

    /// <summary>Shooting → shot power multiplier.</summary>
    public required RatingRange ShootingPower { get; init; }

    /// <summary>Passing → pass power multiplier.</summary>
    public required RatingRange PassingPower { get; init; }

    /// <summary>Stamina → fatigue drain multiplier (high stamina drains slower).</summary>
    public required RatingRange StaminaDrain { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Nested(problems, "paceSpeed", PaceSpeed);
        DataChecks.Nested(problems, "paceAcceleration", PaceAcceleration);
        DataChecks.Nested(problems, "shootingPower", ShootingPower);
        DataChecks.Nested(problems, "passingPower", PassingPower);
        DataChecks.Nested(problems, "staminaDrain", StaminaDrain);
    }
}

/// <summary>
/// Energy (1 = fresh, 0 = exhausted) drains while running fast and recovers while walking.
/// Low energy reduces top speed and acceleration. Disable with <see cref="Enabled"/>.
/// </summary>
public sealed record FatigueConfig : IContentData
{
    public required bool Enabled { get; init; }

    /// <summary>Energy lost per second at top speed, for an average stamina.</summary>
    public required float DrainPerSecond { get; init; }

    /// <summary>Energy regained per second below <see cref="RunningThreshold"/>.</summary>
    public required float RecoveryPerSecond { get; init; }

    /// <summary>Fraction of top speed above which the player is considered running.</summary>
    public required float RunningThreshold { get; init; }

    /// <summary>Top speed multiplier when energy is 0.</summary>
    public required float SpeedWhenExhausted { get; init; }

    /// <summary>Acceleration multiplier when energy is 0.</summary>
    public required float AccelerationWhenExhausted { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NonNegative(problems, "drainPerSecond", DrainPerSecond);
        DataChecks.NonNegative(problems, "recoveryPerSecond", RecoveryPerSecond);
        DataChecks.InRange(problems, "runningThreshold", RunningThreshold, 0f, 1f);
        DataChecks.InRange(problems, "speedWhenExhausted", SpeedWhenExhausted, 0f, 1f);
        DataChecks.InRange(problems, "accelerationWhenExhausted", AccelerationWhenExhausted, 0f, 1f);
    }
}
