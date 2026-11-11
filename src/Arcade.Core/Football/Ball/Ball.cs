using System.Numerics;
using Arcade.Core.Common;

namespace Arcade.Core.Football.Ball;

/// <summary>
/// Raw physical state of the ball. <see cref="Position"/>.Z is the height of the ball's lowest point
/// (0 = on the ground). Mutable struct with fields on purpose: it is copied for trajectory prediction.
/// </summary>
public struct BallState
{
    public Vector3 Position;
    public Vector3 Velocity;

    public readonly bool IsOnGround => Position.Z <= 0f && Velocity.Z <= 0f;

    public readonly float HorizontalSpeed => VectorMath.Horizontal(Velocity).Length();
}

/// <summary>Who may still steer the ball after a kick, and how.</summary>
public readonly record struct AftertouchWindow(int PlayerId, long StartTick, Vector2 Direction, float Scale)
{
    public static AftertouchWindow None => new(Players.Player.NoPlayer, 0, Vector2.Zero, 0f);

    public bool IsActive => PlayerId != Players.Player.NoPlayer;
}

/// <summary>
/// The ball: an independent physical object. Players never carry it; they only change its velocity
/// through contacts and kicks resolved by <see cref="FootballWorld"/>.
/// </summary>
public sealed class Ball
{
    /// <summary>Physical state, updated by reference by the world. Read it through <see cref="Current"/>.</summary>
    internal BallState State;

    /// <summary>Copy of the physical state (e.g. for trajectory prediction).</summary>
    public BallState Current => State;

    public Vector3 Position => State.Position;

    public Vector3 Velocity => State.Velocity;

    /// <summary>Position at the start of the last simulation step (for render interpolation).</summary>
    public Vector3 PreviousPosition { get; internal set; }

    public int LastToucherId { get; internal set; } = Players.Player.NoPlayer;

    public long LastTouchTick { get; internal set; } = long.MinValue / 2;

    public AftertouchWindow Aftertouch { get; internal set; } = AftertouchWindow.None;

    /// <summary>Total horizontal distance rolled or flown (m); drives the rolling animation.</summary>
    public float DistanceTravelled { get; internal set; }

    /// <summary>Extra acceleration applied during the next step only (aftertouch). Cleared after each step.</summary>
    internal Vector3 ControlAcceleration { get; set; }

    internal void AddControlAcceleration(Vector3 acceleration) => ControlAcceleration += acceleration;

    /// <summary>Places the ball at rest (training resets, set pieces).</summary>
    public void PlaceAt(Vector2 position)
    {
        State.Position = new Vector3(position, 0f);
        State.Velocity = Vector3.Zero;
        PreviousPosition = State.Position;
        Aftertouch = AftertouchWindow.None;
        ControlAcceleration = Vector3.Zero;
    }
}
