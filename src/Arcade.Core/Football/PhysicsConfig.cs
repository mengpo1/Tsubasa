using Arcade.Core.Content;

namespace Arcade.Core.Football;

/// <summary>Physical constants of the simulation (<c>config/physics.json</c>). Units: metres, seconds, m/s, m/s².</summary>
public sealed record PhysicsConfig : IContentData
{
    public const string FileName = "config/physics.json";

    public required int StepsPerSecond { get; init; }

    public required float Gravity { get; init; }

    public required BallConfig Ball { get; init; }

    public required GoalFrameConfig GoalFrame { get; init; }

    public required BoardsConfig Boards { get; init; }

    public required MovementConfig Movement { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "stepsPerSecond", StepsPerSecond, 30, 240);
        DataChecks.Positive(problems, "gravity", Gravity);
        DataChecks.Nested(problems, "ball", Ball);
        DataChecks.Nested(problems, "goalFrame", GoalFrame);
        DataChecks.Nested(problems, "boards", Boards);
        DataChecks.Nested(problems, "movement", Movement);
    }
}

public sealed record BallConfig : IContentData
{
    public required float Radius { get; init; }

    /// <summary>Hard cap on the ball speed, keeps the simulation numerically safe.</summary>
    public required float MaxSpeed { get; init; }

    /// <summary>Linear air drag, fraction of the velocity lost per second while airborne.</summary>
    public required float AirDrag { get; init; }

    /// <summary>Constant rolling deceleration (m/s²) on a normal surface.</summary>
    public required float RollingResistance { get; init; }

    /// <summary>Speed-proportional rolling deceleration (1/s) on a normal surface.</summary>
    public required float RollingDrag { get; init; }

    /// <summary>Below this speed a rolling ball stops.</summary>
    public required float StopSpeed { get; init; }

    /// <summary>Vertical speed kept after a bounce (coefficient of restitution).</summary>
    public required float BounceRestitution { get; init; }

    /// <summary>Horizontal speed kept after a bounce.</summary>
    public required float BounceFriction { get; init; }

    /// <summary>Impact speed under which the ball stops bouncing and rolls.</summary>
    public required float MinBounceSpeed { get; init; }

    /// <summary>Acceleration (m/s² per m/s of wind) applied to an airborne ball by the wind.</summary>
    public required float WindAirborneFactor { get; init; }

    /// <summary>Same for a rolling ball (usually much smaller).</summary>
    public required float WindGroundFactor { get; init; }

    /// <summary>Maximum distance travelled by the ball in one collision sub-step (prevents tunnelling).</summary>
    public required float MaxSubstepDistance { get; init; }

    public required int MaxSubsteps { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "radius", Radius, 0.01f, 2f);
        DataChecks.Positive(problems, "maxSpeed", MaxSpeed);
        DataChecks.InRange(problems, "airDrag", AirDrag, 0f, 5f);
        DataChecks.NonNegative(problems, "rollingResistance", RollingResistance);
        DataChecks.InRange(problems, "rollingDrag", RollingDrag, 0f, 10f);
        DataChecks.NonNegative(problems, "stopSpeed", StopSpeed);
        DataChecks.InRange(problems, "bounceRestitution", BounceRestitution, 0f, 1f);
        DataChecks.InRange(problems, "bounceFriction", BounceFriction, 0f, 1f);
        DataChecks.NonNegative(problems, "minBounceSpeed", MinBounceSpeed);
        DataChecks.NonNegative(problems, "windAirborneFactor", WindAirborneFactor);
        DataChecks.NonNegative(problems, "windGroundFactor", WindGroundFactor);
        DataChecks.InRange(problems, "maxSubstepDistance", MaxSubstepDistance, 0.005f, Radius);
        DataChecks.InRange(problems, "maxSubsteps", MaxSubsteps, 1, 64);
    }
}

public sealed record GoalFrameConfig : IContentData
{
    public required float PostRestitution { get; init; }

    public required float PostFriction { get; init; }

    /// <summary>Restitution of the net seen from inside the goal (low: the net absorbs the ball).</summary>
    public required float NetRestitution { get; init; }

    /// <summary>Speed kept along the net when the ball hits it from inside.</summary>
    public required float NetFriction { get; init; }

    /// <summary>Restitution of the net seen from outside (side netting, roof).</summary>
    public required float OuterNetRestitution { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "postRestitution", PostRestitution, 0f, 1f);
        DataChecks.InRange(problems, "postFriction", PostFriction, 0f, 1f);
        DataChecks.InRange(problems, "netRestitution", NetRestitution, 0f, 1f);
        DataChecks.InRange(problems, "netFriction", NetFriction, 0f, 1f);
        DataChecks.InRange(problems, "outerNetRestitution", OuterNetRestitution, 0f, 1f);
    }
}

/// <summary>Advertising boards around the playing area (keep the ball in the arena).</summary>
public sealed record BoardsConfig : IContentData
{
    public required float Restitution { get; init; }

    public required float Friction { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "restitution", Restitution, 0f, 1f);
        DataChecks.InRange(problems, "friction", Friction, 0f, 1f);
    }
}

/// <summary>Player locomotion. Effective values are modulated by attributes, fatigue and surface grip.</summary>
public sealed record MovementConfig : IContentData
{
    /// <summary>Top running speed of an average player (m/s).</summary>
    public required float MaxSpeed { get; init; }

    /// <summary>Acceleration along the desired direction (m/s²).</summary>
    public required float Acceleration { get; init; }

    /// <summary>Deceleration when the joystick is centred or reversed (m/s²).</summary>
    public required float Braking { get; init; }

    /// <summary>How fast sideways velocity is cancelled when changing direction (m/s²). Higher = sharper turns.</summary>
    public required float TurnGrip { get; init; }

    /// <summary>Radius of the player's body for player/player collisions (m).</summary>
    public required float BodyRadius { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.Positive(problems, "maxSpeed", MaxSpeed);
        DataChecks.Positive(problems, "acceleration", Acceleration);
        DataChecks.Positive(problems, "braking", Braking);
        DataChecks.Positive(problems, "turnGrip", TurnGrip);
        DataChecks.InRange(problems, "bodyRadius", BodyRadius, 0.05f, 2f);
    }
}
