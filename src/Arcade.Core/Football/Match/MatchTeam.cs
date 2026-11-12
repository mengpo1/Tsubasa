using System.Numerics;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Tactics;
using Arcade.Core.Football.Teams;

namespace Arcade.Core.Football.Match;

/// <summary>
/// A team during a match: its eleven players (slot i = i-th player of the team file), its tactic, its score and
/// the goal it attacks. Converts between the normalised tactic frame and world coordinates.
/// </summary>
public sealed class MatchTeam
{
    /// <summary>Normalised margin kept between formation targets and the touchlines.</summary>
    private const float MaxNormalizedX = 0.94f;

    private readonly List<Player> _players;

    public MatchTeam(int index, TeamDefinition definition, TacticDefinition tactic, IEnumerable<Player> players)
    {
        Index = index;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Tactic = tactic ?? throw new ArgumentNullException(nameof(tactic));
        _players = [.. players];
        if (_players.Count != tactic.Slots.Count)
        {
            throw new ArgumentException($"Team '{definition.Id}' needs {tactic.Slots.Count} players for tactic '{tactic.Id}'.", nameof(players));
        }

        Goalkeeper = _players[tactic.Slots.ToList().FindIndex(slot => slot.Role == PlayerRole.Goalkeeper)];
    }

    public int Index { get; }

    public TeamDefinition Definition { get; }

    public TacticDefinition Tactic { get; }

    public IReadOnlyList<Player> Players => _players;

    public Player Goalkeeper { get; }

    public int Goals { get; internal set; }

    /// <summary>Goal this team attacks during the current half.</summary>
    public GoalEnd Attacks { get; private set; }

    public GoalEnd Defends => Attacks == GoalEnd.Top ? GoalEnd.Bottom : GoalEnd.Top;

    /// <summary>Unit vector towards the attacked goal.</summary>
    public Vector2 AttackDirection => Attacks == GoalEnd.Top ? new Vector2(0f, -1f) : new Vector2(0f, 1f);

    public bool Owns(Player player) => player is not null && player.TeamIndex == Index;

    public int SlotOf(Player player) => _players.IndexOf(player);

    public void SetAttackedGoal(GoalEnd end)
    {
        Attacks = end;
        foreach (Player player in _players)
        {
            player.DefendsEnd = Defends;
        }
    }

    /// <summary>World position of a normalised tactic point for this team.</summary>
    public Vector2 ToWorld(float x, float y, PitchGeometry pitch)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        float a = Attacks == GoalEnd.Top ? 1f : -1f;
        return new Vector2(a * x * pitch.HalfWidth, -a * y * pitch.HalfLength);
    }

    public Vector2 ToNormalized(Vector2 world, PitchGeometry pitch)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        float a = Attacks == GoalEnd.Top ? 1f : -1f;
        return new Vector2(world.X / (a * pitch.HalfWidth), -world.Y / (a * pitch.HalfLength));
    }

    /// <summary>Where a slot stands for a given ball position: home position shifted with the ball, inside its depth limits.</summary>
    public Vector2 FormationTarget(int slot, Vector2 ball, bool inPossession, PitchGeometry pitch)
    {
        TacticSlot s = Tactic.Slots[slot];
        Vector2 b = ToNormalized(ball, pitch);
        float x = s.Home[0] + (b.X * Tactic.BallFollow.X);
        float y = s.Home[1] + (b.Y * Tactic.BallFollow.Y) + (inPossession ? Tactic.AttackShift : 0f);
        return ToWorld(Math.Clamp(x, -MaxNormalizedX, MaxNormalizedX), Math.Clamp(y, s.MinY, s.MaxY), pitch);
    }

    /// <summary>Kick-off position of a slot. The defending team stays outside the centre circle.</summary>
    public Vector2 KickOffPosition(int slot, bool kicking, PitchGeometry pitch)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        TacticSlot s = Tactic.Slots[slot];
        if (kicking && s.KickOffTaker)
        {
            return -AttackDirection * 0.5f;
        }

        Vector2 position = ToWorld(s.KickOff[0], s.KickOff[1], pitch);
        float clearance = pitch.Definition.CentreCircleRadius + 1f;
        if (!kicking && position.Length() < clearance)
        {
            Vector2 away = position.LengthSquared() > 1e-4f ? Vector2.Normalize(position) : -AttackDirection;
            position = away * clearance;
        }

        return position;
    }

    public Player KickOffTaker => _players[Tactic.Slots.ToList().FindIndex(slot => slot.KickOffTaker)];
}
