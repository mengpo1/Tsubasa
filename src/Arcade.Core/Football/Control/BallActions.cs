using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Control;

/// <summary>
/// The football actions and the conditions that allow them, shared by the human controller and the AI so that
/// both obey exactly the same reach, timing and power rules. Directions are free vectors; the human
/// controller passes 8-way joystick directions.
/// </summary>
public static class BallActions
{
    /// <summary>Fire right after a touch, ball close and low enough: a shot is possible.</summary>
    public static bool CanShoot(FootballWorld world, Player player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ShotConfig shot = world.Controls.Shot;
        return world.Ball.Status == BallStatus.Live
            && world.Ball.LastToucherId == player.Id
            && world.SecondsSince(player.LastTouchTick) <= shot.Window
            && world.DistanceToBall(player) <= shot.Reach
            && world.Ball.Position.Z <= shot.MaxBallHeight;
    }

    /// <summary>Speed of a shot by this player in this direction (shooting rating and run-up included).</summary>
    public static float ShotSpeed(FootballWorld world, Player player, Vector2 direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ShotConfig shot = world.Controls.Shot;
        float power = world.Content.PlayerRules.Effects.ShootingPower.Evaluate(player.Ratings.Shooting);
        float run = MathF.Max(0f, Vector2.Dot(player.Velocity, VectorMath.SafeNormalize(direction)));
        return (shot.BaseSpeed * power) + (shot.RunFactor * run);
    }

    public static void Shoot(FootballWorld world, Player player, Vector2 direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Kick(player, direction, ShotSpeed(world, player, direction), world.Controls.Shot.Lift, KickKind.Shot, world.Controls.Aftertouch.ShotScale);
    }

    /// <summary>Ball on the ground at the feet of its last toucher, ahead in the previous running direction.</summary>
    public static bool CanChip(FootballWorld world, Player player, Vector2 previousDirection)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ChipConfig chip = world.Controls.Chip;
        Vector2 toBall = VectorMath.Horizontal(world.Ball.Position) - player.Position;
        return world.Ball.Status == BallStatus.Live
            && world.Ball.LastToucherId == player.Id
            && world.SecondsSince(player.LastTouchTick) <= chip.PossessionWindow
            && world.Ball.State.IsOnGround
            && toBall.Length() <= chip.Reach
            && Vector2.Dot(toBall, previousDirection) > 0f;
    }

    public static void Chip(FootballWorld world, Player player, Vector2 direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ChipConfig chip = world.Controls.Chip;
        world.Kick(player, direction, chip.Speed, chip.Lift, KickKind.Chip, world.Controls.Aftertouch.ChipScale);
    }

    /// <summary>Pass speed for a charge in 0..1 (aiming time / charge time), passing rating included.</summary>
    public static float PassSpeed(FootballWorld world, Player player, float charge)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        PassConfig pass = world.Controls.Pass;
        float power = world.Content.PlayerRules.Effects.PassingPower.Evaluate(player.Ratings.Passing);
        return VectorMath.Lerp(pass.MinSpeed, pass.MaxSpeed, Math.Clamp(charge, 0f, 1f)) * power;
    }

    /// <summary>Charge (0..1) that gives a ground pass the speed needed to travel <paramref name="distance"/> metres.</summary>
    public static float ChargeForDistance(FootballWorld world, Player player, float distance)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        BallConfig ball = world.Content.Physics.Ball;
        float resistance = ball.RollingResistance * world.Surface.RollingResistanceScale;
        float drag = MathF.Max(1e-3f, ball.RollingDrag * world.Surface.RollingDragScale);
        // Distance of a ball rolling to a stop from v0: d = v0/k - (c/k²)·ln(1 + k·v0/c). Solve for v0 by bisection.
        float low = 0f;
        float high = world.Content.Physics.Ball.MaxSpeed;
        for (int i = 0; i < 30; i++)
        {
            float mid = (low + high) / 2f;
            float travelled = (mid / drag) - (resistance / (drag * drag) * MathF.Log(1f + (drag * mid / MathF.Max(resistance, 1e-3f))));
            if (travelled < distance)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        float minSpeed = PassSpeed(world, player, 0f);
        float maxSpeed = PassSpeed(world, player, 1f);
        return Math.Clamp((high - minSpeed) / MathF.Max(1e-3f, maxSpeed - minSpeed), 0f, 1f);
    }

    public static void Pass(FootballWorld world, Player player, Vector2 direction, float charge)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Kick(player, direction, PassSpeed(world, player, charge), world.Controls.Pass.Lift, KickKind.Pass, world.Controls.Aftertouch.PassScale);
    }

    /// <summary>First touch away from a stopped ball: "the player will turn in the joystick direction and start dribbling".</summary>
    public static void DribbleAway(FootballWorld world, Player player, Vector2 direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        DribbleConfig dribble = world.Controls.Dribble;
        Vector2 unit = VectorMath.SafeNormalize(direction);
        float run = MathF.Max(0f, Vector2.Dot(player.Velocity, unit));
        world.Kick(player, unit, dribble.BaseSpeed + (dribble.RunFactor * run), 0f, KickKind.Dribble, 0f);
    }

    /// <summary>Point where a controlled ball is kept, in front of the player.</summary>
    public static Vector2 HoldPoint(FootballWorld world, Player player, Direction8 facing)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        Direction8 direction = facing != Direction8.None ? facing : player.Facing;
        return player.Position + (direction.ToVector() * world.Controls.Trap.HoldDistance);
    }

    /// <summary>The player stands on the ball: he stops, faces <paramref name="facing"/>, and his ordinary contacts are suspended.</summary>
    public static void KeepAtFeet(FootballWorld world, Player player, Direction8 facing)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        player.Intent.Move = Direction8.None;
        player.Intent.Face = facing;
        player.Intent.KeepsBall = true;
        world.HoldBall(player, HoldPoint(world, player, facing));
    }

    /// <summary>True while the ball is still under the player's control (last touched by him and close to his feet).</summary>
    public static bool HasControl(FootballWorld world, Player player, Direction8 facing)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        float distance = Vector2.Distance(HoldPoint(world, player, facing), VectorMath.Horizontal(world.Ball.Position));
        return world.Ball.Status == BallStatus.Live
            && world.Ball.LastToucherId == player.Id
            && distance <= world.Controls.Trap.LoseDistance;
    }

    /// <summary>Takes a restart (kick-off, throw-in, corner, goal kick) with a charge in 0..1.</summary>
    public static void TakeSetPiece(FootballWorld world, Player player, SetPieceKind kind, Vector2 direction, float charge)
    {
        ArgumentNullException.ThrowIfNull(world);
        SetPieceKickConfig config = world.Controls.SetPieces.For(kind);
        float amount = Math.Clamp(charge, 0f, 1f);
        float speed = VectorMath.Lerp(config.MinSpeed, config.MaxSpeed, amount);
        float lift = VectorMath.Lerp(config.MinLift, config.MaxLift, amount);
        world.Kick(player, direction, speed, lift, KickKind.SetPiece, config.AftertouchScale);
    }

    /// <summary>Charge (0..1) giving a restart kick roughly the right length for <paramref name="distance"/> metres.</summary>
    public static float SetPieceChargeForDistance(FootballWorld world, SetPieceKind kind, float distance)
    {
        ArgumentNullException.ThrowIfNull(world);
        SetPieceKickConfig config = world.Controls.SetPieces.For(kind);
        float gravity = world.Content.Physics.Gravity;
        float best = 0f;
        float bestError = float.MaxValue;
        for (int i = 0; i <= 20; i++)
        {
            float charge = i / 20f;
            float speed = VectorMath.Lerp(config.MinSpeed, config.MaxSpeed, charge);
            float lift = VectorMath.Lerp(config.MinLift, config.MaxLift, charge);
            // Carry of the first flight plus a rough allowance for the bounces and roll.
            float flight = speed * (2f * lift / gravity);
            float estimate = (flight * 1.15f) + (speed * 0.4f);
            float error = MathF.Abs(estimate - distance);
            if (error < bestError)
            {
                bestError = error;
                best = charge;
            }
        }

        return best;
    }
}
