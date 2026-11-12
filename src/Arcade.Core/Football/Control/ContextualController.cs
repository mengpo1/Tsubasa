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

    /// <summary>Taking a restart: the joystick aims, holding fire charges, releasing kicks.</summary>
    SetPiece,
}

/// <summary>Last decision taken, for the debug overlay, the HUD and tests.</summary>
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
    SetPiece,
}

/// <summary>
/// Kick Off 2 style contextual control: one 8-way joystick and one fire button whose meaning depends on
/// timing relative to the ball contact (see GAMEPLAY.md). Translates <see cref="ControlInput"/> into player
/// intents and <see cref="BallActions"/>; it never moves the ball directly. In a match the controlled player
/// changes (nearest to the ball): the button history and an armed trap carry over to the new player.
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
    private long _chargeStartTick = -1;
    private int _aftertouchKickerId = Player.NoPlayer;
    private SetPieceKind _setPiece;
    private Vector2 _setPieceInward;

    public ContextualController(Player player, bool aftertouchEnabled)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        AftertouchEnabled = aftertouchEnabled;
    }

    public Player Player { get; private set; }

    public ControlPhase Phase { get; private set; }

    public ControlDecision LastDecision { get; private set; }

    public long LastDecisionTick { get; private set; }

    /// <summary>Option: when false, the joystick has no influence on the ball after a kick.</summary>
    public bool AftertouchEnabled { get; set; }

    /// <summary>Aiming time of the pending pass (s), for the power gauge.</summary>
    public float AimSeconds { get; private set; }

    /// <summary>Charge (0..1) of the pending restart kick, or null when not charging.</summary>
    public float? SetPieceCharge { get; private set; }

    /// <summary>Tick of the last fire press, used by the match to know whether the human reacted.</summary>
    public long LastFirePressTick { get; private set; } = long.MinValue / 2;

    /// <summary>Reads one step of input and updates intents. Call once per step, before <see cref="FootballWorld.Step"/>.</summary>
    public void Update(ControlInput input, FootballWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        bool pressed = input.Fire && !_previousFire;
        if (pressed)
        {
            LastFirePressTick = world.Tick;
        }

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
            case ControlPhase.SetPiece:
                UpdateSetPiece(world, input, pressed);
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

    /// <summary>
    /// Hands control to another player. An armed trap (fire held while a pass travels) carries over so that
    /// the receiver stops the ball; a ball kept at the feet does not (the new player does not have it).
    /// </summary>
    public void SetPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (ReferenceEquals(player, Player))
        {
            return;
        }

        Player.Intent = default;
        Player = player;
        if (Phase is ControlPhase.Holding or ControlPhase.AtFeet or ControlPhase.SetPiece)
        {
            Phase = ControlPhase.Free;
        }

        AimSeconds = 0f;
        SetPieceCharge = null;
    }

    /// <summary>Starts a restart taken by the controlled player. <paramref name="inward"/> points into the pitch (zero = any direction).</summary>
    public void BeginSetPiece(SetPieceKind kind, Vector2 inward)
    {
        Phase = ControlPhase.SetPiece;
        _setPiece = kind;
        _setPieceInward = inward;
        _aimDirection = Direction8.None;
        _chargeStartTick = -1;
        SetPieceCharge = null;
    }

    /// <summary>Returns to the neutral phase (after a reset of the ball, a restart taken by the computer...).</summary>
    public void Reset()
    {
        Phase = ControlPhase.Free;
        Player.Intent = default;
        AimSeconds = 0f;
        SetPieceCharge = null;
        _chargeStartTick = -1;
    }

    private void UpdateFree(FootballWorld world, bool pressed, Direction8 direction, Direction8 reversedFrom)
    {
        if (reversedFrom != Direction8.None && BallActions.CanChip(world, Player, reversedFrom.ToVector()))
        {
            BallActions.Chip(world, Player, reversedFrom.ToVector());
            Kicked(world, ControlDecision.Chip);
            return;
        }

        if (!pressed)
        {
            return;
        }

        if (BallActions.CanShoot(world, Player))
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
                PassConfig pass = world.Controls.Pass;
                float charge = pass.ChargeTime > 0f ? AimSeconds / pass.ChargeTime : 0f;
                BallActions.Pass(world, Player, direction.ToVector(), charge);
                Player.Facing = direction;
                Kicked(world, ControlDecision.Pass);
            }
            else
            {
                Phase = ControlPhase.AtFeet;
                BallActions.KeepAtFeet(world, Player, Player.Facing);
                Decide(world, ControlDecision.BallAtFeet);
            }

            AimSeconds = 0f;
            return;
        }

        BallActions.KeepAtFeet(world, Player, direction);
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
            BallActions.DribbleAway(world, Player, direction.ToVector());
            Phase = ControlPhase.Free;
            Decide(world, ControlDecision.DribbleAway);
            return;
        }

        BallActions.KeepAtFeet(world, Player, Direction8.None);
    }

    private void UpdateSetPiece(FootballWorld world, ControlInput input, bool pressed)
    {
        Player.Intent.Move = Direction8.None;
        Direction8 direction = input.Direction;
        if (direction != Direction8.None && (_setPieceInward == Vector2.Zero || Vector2.Dot(direction.ToVector(), _setPieceInward) >= -0.01f))
        {
            _aimDirection = direction;
        }

        Direction8 aim = _aimDirection != Direction8.None ? _aimDirection : Player.Facing;
        Player.Intent.Face = aim;
        if (pressed)
        {
            _chargeStartTick = world.Tick;
        }

        if (_chargeStartTick >= 0)
        {
            float chargeTime = world.Controls.SetPieces.For(_setPiece).ChargeTime;
            SetPieceCharge = Math.Clamp(world.SecondsSince(_chargeStartTick) / chargeTime, 0f, 1f);
            if (!input.Fire)
            {
                BallActions.TakeSetPiece(world, Player, _setPiece, aim.ToVector(), SetPieceCharge.Value);
                Player.Facing = aim;
                SetPieceCharge = null;
                _chargeStartTick = -1;
                Kicked(world, ControlDecision.SetPiece);
            }
        }
    }

    private bool LostControl(FootballWorld world)
    {
        Direction8 facing = Player.Intent.Face != Direction8.None ? Player.Intent.Face : Player.Facing;
        if (BallActions.HasControl(world, Player, facing))
        {
            return false;
        }

        Phase = ControlPhase.Free;
        AimSeconds = 0f;
        Decide(world, ControlDecision.BallLost);
        return true;
    }

    private void Shoot(FootballWorld world, Direction8 direction)
    {
        Direction8 shotDirection = direction != Direction8.None ? direction : Player.Facing;
        BallActions.Shoot(world, Player, shotDirection.ToVector());
        Player.Facing = shotDirection;
        Kicked(world, ControlDecision.Shot);
    }

    private void Kicked(FootballWorld world, ControlDecision decision)
    {
        _aftertouchKickerId = Player.Id;
        Phase = ControlPhase.Free;
        Decide(world, decision);
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
    /// Aftertouch: during a short window after a kick made under this controller, the joystick bends the ball
    /// sideways (relative to the kick direction), even if control has moved to another player since.
    /// Forward input dips, backward input lifts, each only if its strength is configured above zero; with the
    /// default data backward positions do nothing, as documented.
    /// </summary>
    private void ApplyAftertouch(FootballWorld world, Direction8 direction)
    {
        var window = world.Ball.Aftertouch;
        if (!window.IsActive || window.PlayerId != _aftertouchKickerId)
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
