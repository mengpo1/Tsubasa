using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Control;

/// <summary>Phase of the one-button control of a human player.</summary>
public enum ControlPhase
{
    /// <summary>Running and dribbling. Fire shoots right after a touch, otherwise arms a trap.</summary>
    Free,

    /// <summary>Fire held before contact: the next contact stops the ball.</summary>
    TrapArmed,

    /// <summary>Ball stopped and controlled, fire still held: the joystick aims, releasing passes.</summary>
    Holding,

    /// <summary>Fire released with the joystick centred: ball at the feet, joystick dribbles away, fire shoots.</summary>
    AtFeet,
}

/// <summary>Last decision taken, for the debug overlay and tests.</summary>
public enum ControlDecision
{
    None,
    Shot,
    TrapArmed,
    Trapped,
    Pass,
    BallAtFeet,
    DribbleAway,
    Chip,
    BallLost,
}

/// <summary>
/// Kick Off 2 style contextual control: one 8-way joystick and one fire button whose meaning depends on
/// timing relative to the ball contact (see GAMEPLAY.md). Translates <see cref="ControlInput"/> into player
/// intents and ball actions on the <see cref="FootballWorld"/>; it never moves the ball directly.
/// </summary>
public sealed class ContextualController
{
    private bool _previousFire;
    private Direction8 _previousDirection;
    private Direction8 _lastHeldDirection;
    private long _lastHeldTick = long.MinValue / 2;
    private long _armedTick;
    private Direction8 _aimDirection;
    private long _aimStartTick;

    public ContextualController(Player player, bool aftertouchEnabled)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        AftertouchEnabled = aftertouchEnabled;
    }

    public Player Player { get; }

    public ControlPhase Phase { get; private set; }

    public ControlDecision LastDecision { get; private set; }

    public long LastDecisionTick { get; private set; }

    /// <summary>Option: when false, the joystick has no influence on the ball after a kick.</summary>
    public bool AftertouchEnabled { get; set; }

    /// <summary>Aiming time of the pending pass (s), for the power gauge.</summary>
    public float AimSeconds { get; private set; }

    /// <summary>Reads one step of input and updates intents. Call once per step, before <see cref="FootballWorld.Step"/>.</summary>
    public void Update(ControlInput input, FootballWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        bool pressed = input.Fire && !_previousFire;
        Direction8 direction = input.Direction;
        Direction8 reversedFrom = DetectReversal(direction, world);

        Player.Intent = new PlayerIntent { Move = direction, Face = direction };
        switch (Phase)
        {
            case ControlPhase.Free:
                UpdateFree(world, pressed, direction, reversedFrom);
                break;
            case ControlPhase.TrapArmed:
                UpdateTrapArmed(world, input);
                break;
            case ControlPhase.Holding:
                UpdateHolding(world, input);
                break;
            case ControlPhase.AtFeet:
                UpdateAtFeet(world, pressed, direction);
                break;
        }

        ApplyAftertouch(world, direction);

        if (direction != Direction8.None)
        {
            _lastHeldDirection = direction;
            _lastHeldTick = world.Tick;
        }

        _previousDirection = direction;
        _previousFire = input.Fire;
    }

    /// <summary>Returns to the neutral phase (after a reset of the ball, a restart...).</summary>
    public void Reset()
    {
        Phase = ControlPhase.Free;
        Player.Intent = default;
        AimSeconds = 0f;
    }

    private void UpdateFree(FootballWorld world, bool pressed, Direction8 direction, Direction8 reversedFrom)
    {
        if (reversedFrom != Direction8.None && CanChip(world, reversedFrom))
        {
            ChipConfig chip = world.Controls.Chip;
            world.Kick(Player, reversedFrom.ToVector(), chip.Speed, chip.Lift, KickKind.Chip, world.Controls.Aftertouch.ChipScale);
            Decide(world, ControlDecision.Chip);
            return;
        }

        if (!pressed)
        {
            return;
        }

        if (CanShoot(world))
        {
            Shoot(world, direction);
        }
        else
        {
            Phase = ControlPhase.TrapArmed;
            _armedTick = world.Tick;
            Player.Intent.TrapReady = true;
            Decide(world, ControlDecision.TrapArmed);
        }
    }

    private void UpdateTrapArmed(FootballWorld world, ControlInput input)
    {
        if (Player.LastTouchKind == BallTouchKind.Trap && Player.LastTouchTick >= _armedTick && world.Ball.LastToucherId == Player.Id)
        {
            Phase = ControlPhase.Holding;
            _aimDirection = Direction8.None;
            _aimStartTick = world.Tick;
            Decide(world, ControlDecision.Trapped);
            UpdateHolding(world, input);
            return;
        }

        if (!input.Fire)
        {
            Phase = ControlPhase.Free;
            return;
        }

        Player.Intent.TrapReady = true;
    }

    private void UpdateHolding(FootballWorld world, ControlInput input)
    {
        if (LostControl(world))
        {
            return;
        }

        Direction8 direction = input.Direction;
        if (direction != _aimDirection)
        {
            _aimDirection = direction;
            _aimStartTick = world.Tick;
        }

        AimSeconds = direction == Direction8.None ? 0f : world.SecondsSince(_aimStartTick);
        if (!input.Fire)
        {
            if (direction != Direction8.None)
            {
                Pass(world, direction, AimSeconds);
            }
            else
            {
                Phase = ControlPhase.AtFeet;
                KeepAtFeet(world);
                Decide(world, ControlDecision.BallAtFeet);
            }

            AimSeconds = 0f;
            return;
        }

        KeepAtFeet(world);
    }

    private void UpdateAtFeet(FootballWorld world, bool pressed, Direction8 direction)
    {
        if (LostControl(world))
        {
            return;
        }

        if (pressed)
        {
            Shoot(world, direction);
            return;
        }

        if (direction != Direction8.None)
        {
            // "The player will turn in the joystick direction and start dribbling in that direction."
            DribbleConfig dribble = world.Controls.Dribble;
            float runSpeed = MathF.Max(0f, Vector2.Dot(Player.Velocity, direction.ToVector()));
            world.Kick(Player, direction.ToVector(), dribble.BaseSpeed + (dribble.RunFactor * runSpeed), 0f, KickKind.Dribble, 0f);
            Phase = ControlPhase.Free;
            Decide(world, ControlDecision.DribbleAway);
            return;
        }

        KeepAtFeet(world);
    }

    /// <summary>The player stands on the ball: he stops running, faces the joystick, and his contacts are suspended.</summary>
    private void KeepAtFeet(FootballWorld world)
    {
        Player.Intent.Move = Direction8.None;
        Player.Intent.KeepsBall = true;
        Direction8 facing = Player.Intent.Face != Direction8.None ? Player.Intent.Face : Player.Facing;
        Vector2 holdPoint = Player.Position + (facing.ToVector() * world.Controls.Trap.HoldDistance);
        world.HoldBall(Player, holdPoint);
    }

    private bool LostControl(FootballWorld world)
    {
        Direction8 facing = Player.Intent.Face != Direction8.None ? Player.Intent.Face : Player.Facing;
        Vector2 holdPoint = Player.Position + (facing.ToVector() * world.Controls.Trap.HoldDistance);
        float distance = Vector2.Distance(holdPoint, VectorMath.Horizontal(world.Ball.Position));
        if (world.Ball.LastToucherId == Player.Id && distance <= world.Controls.Trap.LoseDistance)
        {
            return false;
        }

        Phase = ControlPhase.Free;
        AimSeconds = 0f;
        Decide(world, ControlDecision.BallLost);
        return true;
    }

    private bool CanShoot(FootballWorld world)
    {
        ShotConfig shot = world.Controls.Shot;
        return world.Ball.LastToucherId == Player.Id
            && world.SecondsSince(Player.LastTouchTick) <= shot.Window
            && world.DistanceToBall(Player) <= shot.Reach
            && world.Ball.Position.Z <= shot.MaxBallHeight;
    }

    private bool CanChip(FootballWorld world, Direction8 previousDirection)
    {
        ChipConfig chip = world.Controls.Chip;
        Vector2 toBall = VectorMath.Horizontal(world.Ball.Position) - Player.Position;
        return world.Ball.LastToucherId == Player.Id
            && world.SecondsSince(Player.LastTouchTick) <= chip.PossessionWindow
            && world.Ball.State.IsOnGround
            && toBall.Length() <= chip.Reach
            && Vector2.Dot(toBall, previousDirection.ToVector()) > 0f;
    }

    private void Shoot(FootballWorld world, Direction8 direction)
    {
        Direction8 shotDirection = direction != Direction8.None ? direction : Player.Facing;
        ShotConfig shot = world.Controls.Shot;
        Vector2 unit = shotDirection.ToVector();
        float power = world.Content.PlayerRules.Effects.ShootingPower.Evaluate(Player.Ratings.Shooting);
        float run = MathF.Max(0f, Vector2.Dot(Player.Velocity, unit));
        float speed = (shot.BaseSpeed * power) + (shot.RunFactor * run);
        world.Kick(Player, unit, speed, shot.Lift, KickKind.Shot, world.Controls.Aftertouch.ShotScale);
        Player.Facing = shotDirection;
        Phase = ControlPhase.Free;
        Decide(world, ControlDecision.Shot);
    }

    private void Pass(FootballWorld world, Direction8 direction, float aimSeconds)
    {
        PassConfig pass = world.Controls.Pass;
        float charge = pass.ChargeTime > 0f ? Math.Clamp(aimSeconds / pass.ChargeTime, 0f, 1f) : 0f;
        float power = world.Content.PlayerRules.Effects.PassingPower.Evaluate(Player.Ratings.Passing);
        float speed = VectorMath.Lerp(pass.MinSpeed, pass.MaxSpeed, charge) * power;
        world.Kick(Player, direction.ToVector(), speed, pass.Lift, KickKind.Pass, world.Controls.Aftertouch.PassScale);
        Player.Facing = direction;
        Phase = ControlPhase.Free;
        Decide(world, ControlDecision.Pass);
    }

    /// <summary>
    /// Detects a reversal of the joystick (directly, or through the centre within a short window) and returns
    /// the direction that was reversed, or None.
    /// </summary>
    private Direction8 DetectReversal(Direction8 direction, FootballWorld world)
    {
        if (direction == Direction8.None || direction == _previousDirection)
        {
            return Direction8.None;
        }

        ChipConfig chip = world.Controls.Chip;
        Direction8 reference = _previousDirection != Direction8.None
            ? _previousDirection
            : world.SecondsSince(_lastHeldTick) <= chip.ReversalWindow ? _lastHeldDirection : Direction8.None;
        if (reference == Direction8.None)
        {
            return Direction8.None;
        }

        return Vector2.Dot(direction.ToVector(), reference.ToVector()) <= chip.ReversalDot ? reference : Direction8.None;
    }

    /// <summary>
    /// Aftertouch: during a short window after his kick, the kicker's joystick bends the ball sideways
    /// (relative to the kick direction). Forward input dips, backward input lifts, each only if its strength
    /// is configured above zero; with the default data backward positions do nothing, as documented.
    /// </summary>
    private void ApplyAftertouch(FootballWorld world, Direction8 direction)
    {
        var window = world.Ball.Aftertouch;
        if (!window.IsActive || window.PlayerId != Player.Id)
        {
            return;
        }

        AftertouchConfig config = world.Controls.Aftertouch;
        float elapsed = world.SecondsSince(window.StartTick);
        if (!AftertouchEnabled || !config.Enabled || elapsed >= config.Duration)
        {
            world.CancelAftertouch();
            return;
        }

        if (direction == Direction8.None)
        {
            return;
        }

        Vector2 stick = direction.ToVector();
        float forward = Vector2.Dot(stick, window.Direction);
        float sideways = Vector2.Dot(stick, VectorMath.Perpendicular(window.Direction));
        if (forward < -0.01f && config.LiftAcceleration <= 0f)
        {
            return;
        }

        float strength = (1f - (elapsed / config.Duration)) * window.Scale;
        Vector2 travel = VectorMath.SafeNormalize(VectorMath.Horizontal(world.Ball.Velocity));
        if (travel == Vector2.Zero)
        {
            travel = window.Direction;
        }

        Vector2 lateral = VectorMath.Perpendicular(travel) * (sideways * config.CurlAcceleration * strength);
        float vertical = 0f;
        if (!world.Ball.State.IsOnGround)
        {
            vertical = forward > 0f
                ? -forward * config.DipAcceleration * strength
                : -forward * config.LiftAcceleration * strength;
        }

        world.ApplyBallAcceleration(new Vector3(lateral, vertical));
    }

    private void Decide(FootballWorld world, ControlDecision decision)
    {
        LastDecision = decision;
        LastDecisionTick = world.Tick;
    }
}
