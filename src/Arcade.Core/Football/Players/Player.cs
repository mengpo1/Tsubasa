using System.Numerics;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Players;

/// <summary>How a player last touched the ball.</summary>
public enum BallTouchKind
{
    None,
    Dribble,
    Trap,
    Deflection,
    Kick,

    /// <summary>A goalkeeper caught the ball in his hands.</summary>
    Catch,
}

/// <summary>Visible action, used by animation and debug display. Never drives the rules.</summary>
public enum PlayerAction
{
    Idle,
    Running,
    Kicking,
    Controlling,
}

/// <summary>
/// What the player's controller (human or, later, AI) asks for during the current step.
/// Both kinds of controller go through the same intents and the same physics.
/// </summary>
public struct PlayerIntent
{
    /// <summary>Requested running direction (None = stop).</summary>
    public Direction8 Move;

    /// <summary>Requested facing (None = keep the current facing).</summary>
    public Direction8 Face;

    /// <summary>Fire is held before contact: the next contact stops and controls the ball.</summary>
    public bool TrapReady;

    /// <summary>The player is keeping a controlled ball: his ordinary contacts are suspended.</summary>
    public bool KeepsBall;
}

/// <summary>Ratings normalised to 0..1 from the team file values.</summary>
public readonly record struct PlayerRatings(
    float Pace,
    float Stamina,
    float Aggression,
    float Resilience,
    float Passing,
    float Shooting,
    float Tackling,
    float Flair)
{
    public static PlayerRatings Average => new(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);

    public static PlayerRatings From(PlayerAttributeValues values, RatingScale scale)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(scale);
        return new PlayerRatings(
            scale.Normalize(values.Pace),
            scale.Normalize(values.Stamina),
            scale.Normalize(values.Aggression),
            scale.Normalize(values.Resilience),
            scale.Normalize(values.Passing),
            scale.Normalize(values.Shooting),
            scale.Normalize(values.Tackling),
            scale.Normalize(values.Flair));
    }
}

/// <summary>A footballer on the pitch: kinematic state plus identity. Position is on the ground (metres).</summary>
public sealed class Player
{
    public const int NoPlayer = -1;

    public Player(int id, int teamIndex, PlayerProfile profile, PlayerRatings ratings)
    {
        Id = id;
        TeamIndex = teamIndex;
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Ratings = ratings;
    }

    public int Id { get; }

    public int TeamIndex { get; }

    public PlayerProfile Profile { get; }

    public PlayerRatings Ratings { get; }

    /// <summary>Goal this player's team defends (null outside matches). Set by the match at each kick-off.</summary>
    public Pitch.GoalEnd? DefendsEnd { get; set; }

    /// <summary>True while a human controller drives this player (some original rules differ for computer players).</summary>
    public bool ControlledByHuman { get; internal set; }

    /// <summary>True for the goalkeeper of a team in a match: he may use his hands in his own penalty area.</summary>
    public bool IsGoalkeeper => Profile.Role == PlayerRole.Goalkeeper && DefendsEnd is not null;

    public Vector2 Position { get; set; }

    public Vector2 PreviousPosition { get; internal set; }

    public Vector2 Velocity { get; set; }

    public Direction8 Facing { get; set; } = Direction8.Up;

    /// <summary>1 = fresh, 0 = exhausted.</summary>
    public float Energy { get; internal set; } = 1f;

    /// <summary>Total distance run (m); drives the running animation.</summary>
    public float DistanceRun { get; internal set; }

    /// <summary>Intent written by the player's controller for the current step.</summary>
    internal PlayerIntent Intent;

    public PlayerIntent CurrentIntent => Intent;

    public long LastTouchTick { get; internal set; } = long.MinValue / 2;

    public BallTouchKind LastTouchKind { get; internal set; }

    public PlayerAction Action { get; internal set; }

    public long ActionTick { get; internal set; }

    public float Speed => Velocity.Length();

    public void PlaceAt(Vector2 position, Direction8 facing)
    {
        Position = position;
        PreviousPosition = position;
        Velocity = Vector2.Zero;
        Facing = facing == Direction8.None ? Facing : facing;
        Intent = default;
    }
}
