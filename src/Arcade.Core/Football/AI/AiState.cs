using System.Numerics;

namespace Arcade.Core.Football.AI;

/// <summary>What a computer-controlled player is currently doing (shown in the debug overlay).</summary>
public enum AiState
{
    Idle,
    Positioning,
    Chasing,
    Dribbling,
    Holding,
    TakingRestart,
    Keeping,
    KeeperHolding,
}

/// <summary>Per-player memory of the AI: state, target and timers.</summary>
public sealed class AiBrain
{
    public AiState State { get; internal set; }

    /// <summary>Point the player is moving to (world metres).</summary>
    public Vector2 Target { get; internal set; }

    /// <summary>Short machine-readable label of the last decision (shot, pass, dribble...), for debugging.</summary>
    public string Decision { get; internal set; } = string.Empty;

    internal long StateTick { get; set; }

    internal long NextDecisionTick { get; set; }

    internal Vector2 DribbleDirection { get; set; }

    internal void Enter(AiState state, long tick)
    {
        if (State != state)
        {
            State = state;
            StateTick = tick;
        }
    }
}
