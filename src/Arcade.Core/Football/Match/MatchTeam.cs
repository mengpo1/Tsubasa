using System.Numerics;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Tactics;
using Arcade.Core.Football.Teams;

namespace Arcade.Core.Football.Match;

/// <summary>
/// A team during a match: its eleven starters, its tactic, its score and the goal it attacks.
/// The goalkeeper is the starter with the goalkeeper role; the other ten are the outfield players of the tactic,
/// in team-file order (as the shirts 2 to 11 of a Kick Off 2 tactic file).
/// </summary>
public sealed class MatchTeam
{
    public const int StartingPlayers = 11;

    /// <summary>Distance of the keeper from his goal line at kick-off (m).</summary>
    private const float KeeperKickOffDepth = 1.5f;

    private readonly List<Player> _players;
    private readonly List<Player> _outfield;

    public MatchTeam(int index, TeamDefinition definition, TacticDefinition tactic, IEnumerable<Player> players)
    {
        Index = index;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Tactic = tactic ?? throw new ArgumentNullException(nameof(tactic));
        _players = [.. players];
        List<Player> keepers = _players.FindAll(p => p.Profile.Role == PlayerRole.Goalkeeper);
        if (_players.Count != StartingPlayers || keepers.Count != 1)
        {
            throw new ArgumentException($"Team '{definition.Id}' must start with {StartingPlayers} players including exactly one goalkeeper.", nameof(players));
        }

        Goalkeeper = keepers[0];
        _outfield = _players.FindAll(p => p != Goalkeeper);
        KickOffTaker = _outfield.MinBy(p => Distance(Tactic.Players[_outfield.IndexOf(p)].KickOffOwn, Tactic.Units.Width / 2f, Tactic.Units.Length / 2f))!;
    }

    public int Index { get; }

    public TeamDefinition Definition { get; }

    public TacticDefinition Tactic { get; }

    public IReadOnlyList<Player> Players => _players;

    /// <summary>The ten outfield players, in tactic order.</summary>
    public IReadOnlyList<Player> Outfield => _outfield;

    public Player Goalkeeper { get; }

    /// <summary>Outfield player whose own kick-off position is nearest to the centre spot.</summary>
    public Player KickOffTaker { get; }

    public int Goals { get; internal set; }

    /// <summary>Goal this team attacks during the current half.</summary>
    public GoalEnd Attacks { get; private set; }

    public GoalEnd Defends => Attacks == GoalEnd.Top ? GoalEnd.Bottom : GoalEnd.Top;

    /// <summary>Unit vector towards the attacked goal.</summary>
    public Vector2 AttackDirection => Attacks == GoalEnd.Top ? new Vector2(0f, -1f) : new Vector2(0f, 1f);

    public bool Owns(Player player) => player is not null && player.TeamIndex == Index;

    public void SetAttackedGoal(GoalEnd end)
    {
        Attacks = end;
        foreach (Player player in _players)
        {
            player.DefendsEnd = Defends;
        }
    }

    /// <summary>World position of a normalised point of the team frame (x -1..1 from the left touchline, y -1..1 from the own goal line).</summary>
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

    /// <summary>World position of a tactic point (tactic units, team frame).</summary>
    public Vector2 FromTacticUnits(IReadOnlyList<float> point, PitchGeometry pitch)
    {
        ArgumentNullException.ThrowIfNull(point);
        return ToWorld(((point[0] / Tactic.Units.Width) * 2f) - 1f, ((point[1] / Tactic.Units.Length) * 2f) - 1f, pitch);
    }

    /// <summary>Tactic zone (0..11) of a ball position, seen from this team.</summary>
    public int BallZone(Vector2 ball, PitchGeometry pitch)
    {
        Vector2 n = ToNormalized(ball, pitch);
        return TacticDefinition.ZoneOf((n.X + 1f) / 2f, (n.Y + 1f) / 2f);
    }

    /// <summary>Where an outfield player stands for the zone the ball is in (attack set while in possession, if the tactic has one).</summary>
    public Vector2 TacticTarget(Player player, Vector2 ball, bool inPossession, PitchGeometry pitch) =>
        FromTacticUnits(PositionsOf(player).ZonePosition(BallZone(ball, pitch), inPossession), pitch);

    public Vector2 SituationTarget(Player player, TacticSituation situation, PitchGeometry pitch) =>
        FromTacticUnits(PositionsOf(player).Situation(situation), pitch);

    /// <summary>Corner situation for a corner taken at <paramref name="spot"/>, from this team's point of view.</summary>
    public TacticSituation CornerSituation(Vector2 spot, PitchGeometry pitch)
    {
        bool ownEnd = Vector2.Dot(spot, AttackDirection) < 0f;
        bool left = ToNormalized(spot, pitch).X < 0f;
        return (ownEnd, left) switch
        {
            (true, true) => TacticSituation.CornerOwnLeft,
            (true, false) => TacticSituation.CornerOwnRight,
            (false, true) => TacticSituation.CornerOpponentLeft,
            _ => TacticSituation.CornerOpponentRight,
        };
    }

    /// <summary>
    /// Kick-off position from the tactic, made legal whatever the data: everybody in his own half, the taker behind
    /// the ball, the defending team outside the centre circle.
    /// </summary>
    public Vector2 KickOffPosition(Player player, bool kicking, PitchGeometry pitch)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(pitch);
        if (player == Goalkeeper)
        {
            return pitch.GoalCentre(Defends) + (AttackDirection * KeeperKickOffDepth);
        }

        if (kicking && player == KickOffTaker)
        {
            return -AttackDirection * 0.5f;
        }

        Vector2 position = SituationTarget(player, kicking ? TacticSituation.KickOffOwn : TacticSituation.KickOffOpponent, pitch);
        float forward = Vector2.Dot(position, AttackDirection);
        if (forward > -0.5f)
        {
            position -= AttackDirection * (forward + 0.5f);
        }

        float clearance = pitch.Definition.CentreCircleRadius + 1f;
        if (!kicking && position.Length() < clearance)
        {
            Vector2 away = position.LengthSquared() > 1e-4f ? Vector2.Normalize(position) : -AttackDirection;
            position = away * clearance;
        }

        return position;
    }

    private TacticPlayer PositionsOf(Player player)
    {
        int index = _outfield.IndexOf(player);
        return index >= 0
            ? Tactic.Players[index]
            : throw new ArgumentException($"{player.Profile.Name} is not an outfield player of team {Index}.", nameof(player));
    }

    private static float Distance(IReadOnlyList<float> point, float x, float y) =>
        MathF.Sqrt(((point[0] - x) * (point[0] - x)) + ((point[1] - y) * (point[1] - y)));
}
