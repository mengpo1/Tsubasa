using Arcade.Core.Content;

namespace Arcade.Core.Football;

/// <summary>
/// Timings, reaches and kick powers of the contextual one-button controls (<c>config/controls.json</c>).
/// None of these values is documented in the original manuals: they are estimates to calibrate by play.
/// </summary>
public sealed record ControlConfig : IContentData
{
    public const string FileName = "config/controls.json";

    public required ContactConfig Contact { get; init; }

    public required DribbleConfig Dribble { get; init; }

    public required DeflectionConfig Deflection { get; init; }

    public required ShotConfig Shot { get; init; }

    public required TrapConfig Trap { get; init; }

    public required PassConfig Pass { get; init; }

    public required ChipConfig Chip { get; init; }

    public required AftertouchConfig Aftertouch { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Nested(problems, "contact", Contact);
        DataChecks.Nested(problems, "dribble", Dribble);
        DataChecks.Nested(problems, "deflection", Deflection);
        DataChecks.Nested(problems, "shot", Shot);
        DataChecks.Nested(problems, "trap", Trap);
        DataChecks.Nested(problems, "pass", Pass);
        DataChecks.Nested(problems, "chip", Chip);
        DataChecks.Nested(problems, "aftertouch", Aftertouch);
    }
}

/// <summary>Where a player can touch the ball. Independent of the sprite size.</summary>
public sealed record ContactConfig : IContentData
{
    /// <summary>Horizontal reach of the feet around the player's ground position (m).</summary>
    public required float FootRadius { get; init; }

    /// <summary>Highest ball bottom (m) still played with the feet.</summary>
    public required float FootHeight { get; init; }

    /// <summary>Horizontal radius of the body for higher balls (m).</summary>
    public required float BodyRadius { get; init; }

    /// <summary>Top of the body (m); higher balls fly over the player.</summary>
    public required float BodyHeight { get; init; }

    /// <summary>Minimum time between two touches by the same player (s).</summary>
    public required float TouchCooldown { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "footRadius", FootRadius);
        DataChecks.Positive(problems, "footHeight", FootHeight);
        DataChecks.Positive(problems, "bodyRadius", BodyRadius);
        DataChecks.Positive(problems, "bodyHeight", BodyHeight);
        DataChecks.NonNegative(problems, "touchCooldown", TouchCooldown);
    }
}

/// <summary>Running into the ball without pressing fire pushes it ahead: speed = baseSpeed + runFactor x player speed.</summary>
public sealed record DribbleConfig : IContentData
{
    public required float BaseSpeed { get; init; }

    public required float RunFactor { get; init; }

    /// <summary>Above this ball/player relative speed the ball cannot be dribbled: it bounces off the player.</summary>
    public required float MaxRelativeSpeed { get; init; }

    /// <summary>Balls behind the player (dot of ball offset and facing below this value) are deflected instead of pushed.</summary>
    public required float FrontDot { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NonNegative(problems, "baseSpeed", BaseSpeed);
        DataChecks.InRange(problems, "runFactor", RunFactor, 1f, 3f);
        DataChecks.Positive(problems, "maxRelativeSpeed", MaxRelativeSpeed);
        DataChecks.InRange(problems, "frontDot", FrontDot, -1f, 1f);
    }
}

/// <summary>Uncontrolled contact: the ball rebounds off the player's body.</summary>
public sealed record DeflectionConfig : IContentData
{
    public required float Restitution { get; init; }

    /// <summary>Fraction of the tangential relative speed kept.</summary>
    public required float TangentialKeep { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "restitution", Restitution, 0f, 1f);
        DataChecks.InRange(problems, "tangentialKeep", TangentialKeep, 0f, 1f);
    }
}

/// <summary>Fire pressed shortly AFTER touching the ball: shot in the facing direction.</summary>
public sealed record ShotConfig : IContentData
{
    /// <summary>Time after a touch during which pressing fire shoots (s).</summary>
    public required float Window { get; init; }

    /// <summary>Maximum distance between player and ball for the shot to connect (m).</summary>
    public required float Reach { get; init; }

    /// <summary>Highest ball bottom (m) that can be shot.</summary>
    public required float MaxBallHeight { get; init; }

    public required float BaseSpeed { get; init; }

    /// <summary>Part of the player's running speed (along the shot) added to the shot.</summary>
    public required float RunFactor { get; init; }

    /// <summary>Initial vertical speed of a shot (m/s).</summary>
    public required float Lift { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "window", Window);
        DataChecks.Positive(problems, "reach", Reach);
        DataChecks.Positive(problems, "maxBallHeight", MaxBallHeight);
        DataChecks.Positive(problems, "baseSpeed", BaseSpeed);
        DataChecks.NonNegative(problems, "runFactor", RunFactor);
        DataChecks.NonNegative(problems, "lift", Lift);
    }
}

/// <summary>Fire held BEFORE the contact: the ball is stopped and controlled.</summary>
public sealed record TrapConfig : IContentData
{
    public required float MaxBallHeight { get; init; }

    /// <summary>Faster balls (relative to the player) cannot be trapped and rebound.</summary>
    public required float MaxRelativeSpeed { get; init; }

    /// <summary>Distance in front of the player where a controlled ball is kept (m).</summary>
    public required float HoldDistance { get; init; }

    /// <summary>Speed at which the controlled ball is steered towards the hold point (1/s).</summary>
    public required float HoldGain { get; init; }

    /// <summary>Maximum steering speed of a controlled ball (m/s).</summary>
    public required float MaxHoldSpeed { get; init; }

    /// <summary>A controlled ball further than this from its hold point is lost (m).</summary>
    public required float LoseDistance { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "maxBallHeight", MaxBallHeight);
        DataChecks.Positive(problems, "maxRelativeSpeed", MaxRelativeSpeed);
        DataChecks.Positive(problems, "holdDistance", HoldDistance);
        DataChecks.Positive(problems, "holdGain", HoldGain);
        DataChecks.Positive(problems, "maxHoldSpeed", MaxHoldSpeed);
        DataChecks.Positive(problems, "loseDistance", LoseDistance);
    }
}

/// <summary>
/// Pass from a controlled ball: hold fire, aim with the joystick, release.
/// The longer the direction is held before release, the stronger the pass (design choice, see GAMEPLAY.md).
/// </summary>
public sealed record PassConfig : IContentData
{
    public required float MinSpeed { get; init; }

    public required float MaxSpeed { get; init; }

    /// <summary>Aiming time (s) to reach <see cref="MaxSpeed"/>. 0 = always <see cref="MinSpeed"/>.</summary>
    public required float ChargeTime { get; init; }

    public required float Lift { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "minSpeed", MinSpeed);
        DataChecks.Positive(problems, "maxSpeed", MaxSpeed);
        if (MaxSpeed < MinSpeed)
        {
            problems.Add("maxSpeed must be greater than or equal to minSpeed");
        }

        DataChecks.NonNegative(problems, "chargeTime", ChargeTime);
        DataChecks.NonNegative(problems, "lift", Lift);
    }
}

/// <summary>Reversing the joystick while the ball is on the ground at the player's feet chips it.</summary>
public sealed record ChipConfig : IContentData
{
    public required float Reach { get; init; }

    /// <summary>Time after the player's last touch during which a reversal still chips (s).</summary>
    public required float PossessionWindow { get; init; }

    /// <summary>The new direction must satisfy dot(new, previous) &lt;= this value (-1 = exact reversal).</summary>
    public required float ReversalDot { get; init; }

    /// <summary>A reversal through the centre position counts if the previous direction was held this recently (s).</summary>
    public required float ReversalWindow { get; init; }

    public required float Speed { get; init; }

    public required float Lift { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "reach", Reach);
        DataChecks.Positive(problems, "possessionWindow", PossessionWindow);
        DataChecks.InRange(problems, "reversalDot", ReversalDot, -1f, 0f);
        DataChecks.NonNegative(problems, "reversalWindow", ReversalWindow);
        DataChecks.Positive(problems, "speed", Speed);
        DataChecks.Positive(problems, "lift", Lift);
    }
}

/// <summary>
/// Joystick influence on the ball just after a kick. Documented: sideways bend using the forward joystick
/// positions only, for a very short time. Dip/lift strengths are set to 0 for open play by default.
/// </summary>
public sealed record AftertouchConfig : IContentData
{
    public required bool Enabled { get; init; }

    /// <summary>Length of the aftertouch window after the kick (s).</summary>
    public required float Duration { get; init; }

    /// <summary>Sideways acceleration at full strength (m/s²), fading linearly to 0 over the window.</summary>
    public required float CurlAcceleration { get; init; }

    /// <summary>Downward acceleration for "forward" input (m/s²). 0 disables dipping.</summary>
    public required float DipAcceleration { get; init; }

    /// <summary>Upward acceleration for "backward" input (m/s²). 0 = backward positions do nothing (as documented).</summary>
    public required float LiftAcceleration { get; init; }

    public required float ShotScale { get; init; }

    public required float PassScale { get; init; }

    public required float ChipScale { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "duration", Duration);
        DataChecks.NonNegative(problems, "curlAcceleration", CurlAcceleration);
        DataChecks.NonNegative(problems, "dipAcceleration", DipAcceleration);
        DataChecks.NonNegative(problems, "liftAcceleration", LiftAcceleration);
        DataChecks.NonNegative(problems, "shotScale", ShotScale);
        DataChecks.NonNegative(problems, "passScale", PassScale);
        DataChecks.NonNegative(problems, "chipScale", ChipScale);
    }
}
