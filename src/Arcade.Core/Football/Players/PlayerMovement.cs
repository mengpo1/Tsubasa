using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Players;

/// <summary>
/// Arcade locomotion. The velocity is split along/across the requested direction: the sideways part is
/// cancelled by "grip" while the forward part accelerates towards the top speed. Players therefore react
/// instantly (facing and acceleration start on the same step) but keep momentum through turns and reversals.
/// </summary>
public static class PlayerMovement
{
    public static void Step(Player player, MovementConfig movement, PlayerRulesConfig rules, SurfaceDefinition surface, float dt)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(surface);

        if (player.Intent.Face != Direction8.None)
        {
            player.Facing = player.Intent.Face;
        }

        float topSpeed = TopSpeed(player, movement, rules);
        float fatigueAcceleration = rules.Fatigue.Enabled
            ? VectorMath.Lerp(rules.Fatigue.AccelerationWhenExhausted, 1f, player.Energy)
            : 1f;
        float grip = surface.PlayerGripScale * fatigueAcceleration;
        float acceleration = movement.Acceleration * rules.Effects.PaceAcceleration.Evaluate(player.Ratings.Pace) * grip;
        float braking = movement.Braking * grip;
        float turnGrip = movement.TurnGrip * grip;

        Vector2 velocity = player.Velocity;
        Vector2 direction = player.Intent.Move.ToVector();
        if (direction == Vector2.Zero)
        {
            velocity = VectorMath.MoveTowards(velocity, Vector2.Zero, braking * dt);
        }
        else
        {
            float along = Vector2.Dot(velocity, direction);
            Vector2 across = velocity - (direction * along);
            across = VectorMath.MoveTowards(across, Vector2.Zero, turnGrip * dt);
            if (along < 0f)
            {
                // Reversing: brake first, any leftover time accelerates the other way.
                along += braking * dt;
                if (along > 0f)
                {
                    along *= acceleration / braking;
                }
            }
            else if (along > topSpeed)
            {
                along = MathF.Max(topSpeed, along - (braking * dt));
            }
            else
            {
                along = MathF.Min(topSpeed, along + (acceleration * dt));
            }

            velocity = (direction * along) + across;
        }

        player.Velocity = velocity;
        player.Position += velocity * dt;
        float speed = velocity.Length();
        player.DistanceRun += speed * dt;
        UpdateEnergy(player, rules.Fatigue, surface, rules.Effects.StaminaDrain.Evaluate(player.Ratings.Stamina), speed / topSpeed, dt);
    }

    /// <summary>Current top speed, including pace and fatigue (m/s).</summary>
    public static float TopSpeed(Player player, MovementConfig movement, PlayerRulesConfig rules)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(rules);
        float fatigue = rules.Fatigue.Enabled ? VectorMath.Lerp(rules.Fatigue.SpeedWhenExhausted, 1f, player.Energy) : 1f;
        return movement.MaxSpeed * rules.Effects.PaceSpeed.Evaluate(player.Ratings.Pace) * fatigue;
    }

    private static void UpdateEnergy(Player player, FatigueConfig fatigue, SurfaceDefinition surface, float drainScale, float speedRatio, float dt)
    {
        if (!fatigue.Enabled)
        {
            return;
        }

        float energy = player.Energy;
        if (speedRatio >= fatigue.RunningThreshold)
        {
            energy -= fatigue.DrainPerSecond * drainScale * surface.StaminaDrainScale * speedRatio * dt;
        }
        else
        {
            energy += fatigue.RecoveryPerSecond * dt;
        }

        player.Energy = Math.Clamp(energy, 0f, 1f);
    }
}
