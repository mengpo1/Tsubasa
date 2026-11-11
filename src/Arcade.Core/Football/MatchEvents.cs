using System.Numerics;

namespace Arcade.Core.Football;

public enum MatchEventType
{
    /// <summary>A player touched the ball (Detail = <see cref="Players.BallTouchKind"/>).</summary>
    BallTouched,

    /// <summary>A player kicked the ball (Detail = <see cref="KickKind"/>, Value = speed m/s).</summary>
    BallKicked,

    /// <summary>The ball bounced on the ground (Value = impact speed).</summary>
    BallBounced,

    PostHit,
    CrossbarHit,
    NetHit,
    BoardHit,

    /// <summary>The ball is in a goal (Detail = <see cref="Pitch.GoalEnd"/>).</summary>
    Goal,
}

public enum KickKind
{
    Shot,
    Pass,
    Chip,
    Dribble,
}

/// <summary>
/// Something notable that happened during a simulation step. Consumed by sound, effects, UI and replays.
/// A plain value: producing events allocates nothing.
/// </summary>
public readonly record struct MatchEvent(MatchEventType Type, long Tick, int PlayerId, Vector3 Position, int Detail = 0, float Value = 0f);

/// <summary>Events produced since the consumer last cleared the buffer. Bounded to avoid unbounded growth.</summary>
public sealed class MatchEventBuffer
{
    private readonly List<MatchEvent> _events = new(64);
    private readonly int _capacity;

    public MatchEventBuffer(int capacity = 1024)
    {
        _capacity = capacity;
    }

    public IReadOnlyList<MatchEvent> Items => _events;

    public int Count => _events.Count;

    public void Add(MatchEvent matchEvent)
    {
        if (_events.Count >= _capacity)
        {
            _events.RemoveRange(0, _capacity / 4);
        }

        _events.Add(matchEvent);
    }

    public void Clear() => _events.Clear();
}
