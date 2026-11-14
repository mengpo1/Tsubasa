using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Simulation;

namespace Arcade.Core.Football.Ball;

[Flags]
public enum BallImpacts
{
    None = 0,
    Bounce = 1,
    Post = 2,
    Crossbar = 4,
    Net = 8,
    Board = 16,
}

/// <summary>
/// Free motion of the ball over one (sub-)step: gravity, air drag, wind, bounces and rolling resistance,
/// all scaled by the surface. Pure function of its inputs (plus the random generator for irregular surfaces),
/// so it can also be used to predict trajectories.
/// </summary>
public sealed class BallPhysics
{
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float MaxRestitution = 0.95f;

    public BallPhysics(BallConfig config, float gravity)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Gravity = gravity;
    }

    public BallConfig Config { get; }

    public float Gravity { get; }

    /// <summary>Number of sub-steps so that the ball (relative to the fastest mover) never travels more than the configured distance per sub-step.</summary>
    public int SubstepCount(float relativeSpeed, float stepSeconds)
    {
        float distance = relativeSpeed * stepSeconds;
        int count = (int)MathF.Ceiling(distance / Config.MaxSubstepDistance);
        return Math.Clamp(count, 1, Config.MaxSubsteps);
    }

    /// <param name="random">Null disables surface irregularities (used for predictions).</param>
    public BallImpacts Integrate(ref BallState state, SurfaceDefinition surface, Vector2 wind, Vector3 extraAcceleration, float h, GameRandom? random)
    {
        ArgumentNullException.ThrowIfNull(surface);
        BallImpacts impacts = BallImpacts.None;
        bool airborne = state.Position.Z > 0f || state.Velocity.Z > 0f;
        if (airborne)
        {
            Vector3 velocity = state.Velocity;
            velocity.Z -= Gravity * h;
            velocity += extraAcceleration * h;
            velocity += new Vector3(wind * (Config.WindAirborneFactor * h), 0f);
            velocity *= MathF.Max(0f, 1f - (Config.AirDrag * surface.AirDragScale * h));
            state.Velocity = velocity;
            state.Position += velocity * h;
            if (state.Position.Z <= 0f)
            {
                impacts |= Land(ref state, surface, random);
            }
        }
        else
        {
            Roll(ref state, surface, wind, extraAcceleration, h, random);
        }

        state.Velocity = VectorMath.ClampLength(state.Velocity, Config.MaxSpeed);
        return impacts;
    }

    private BallImpacts Land(ref BallState state, SurfaceDefinition surface, GameRandom? random)
    {
        state.Position.Z = 0f;
        float impactSpeed = -state.Velocity.Z;
        if (impactSpeed < Config.MinBounceSpeed)
        {
            state.Velocity.Z = 0f;
            return BallImpacts.None;
        }

        float restitution = MathF.Min(Config.BounceRestitution * surface.BounceRestitutionScale, MaxRestitution);
        float friction = MathF.Min(Config.BounceFriction * surface.BounceFrictionScale, 1f);
        Vector2 horizontal = VectorMath.Horizontal(state.Velocity) * friction;
        if (random is not null && surface.BounceDeviationDegrees > 0f)
        {
            horizontal = VectorMath.Rotate(horizontal, random.NextSigned() * surface.BounceDeviationDegrees * DegreesToRadians);
        }

        state.Velocity = new Vector3(horizontal, impactSpeed * restitution);
        return BallImpacts.Bounce;
    }

    private void Roll(ref BallState state, SurfaceDefinition surface, Vector2 wind, Vector3 extraAcceleration, float h, GameRandom? random)
    {
        state.Position.Z = 0f;
        Vector2 velocity = VectorMath.Horizontal(state.Velocity);
        velocity += new Vector2(extraAcceleration.X, extraAcceleration.Y) * h;
        velocity += wind * (Config.WindGroundFactor * h);
        float speed = velocity.Length();
        if (speed > 0f)
        {
            float deceleration = (Config.RollingResistance * surface.RollingResistanceScale)
                + (Config.RollingDrag * surface.RollingDragScale * speed);
            float newSpeed = MathF.Max(0f, speed - (deceleration * h));
            if (newSpeed < Config.StopSpeed && extraAcceleration == Vector3.Zero)
            {
                newSpeed = 0f;
            }

            velocity *= newSpeed / speed;
            if (random is not null && surface.RollingDeviationDegreesPerMetre > 0f && newSpeed > 0f)
            {
                float metres = newSpeed * h;
                velocity = VectorMath.Rotate(velocity, random.NextSigned() * surface.RollingDeviationDegreesPerMetre * metres * DegreesToRadians);
            }
        }

        state.Velocity = new Vector3(velocity, 0f);
        state.Position += state.Velocity * h;
    }
}
