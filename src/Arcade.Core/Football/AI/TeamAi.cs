using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Core.Football.AI;

/// <summary>
/// Elementary team AI (Lot 2): a simple state machine per player plus scored decisions for the ball carrier.
/// <list type="bullet">
/// <item>the ball carrier dribbles, shoots when in range, or stops the ball to pass when pressed;</item>
/// <item>without the ball, the best-placed player(s) chase it (intercept on the predicted path);</item>
/// <item>everybody else holds his formation slot, shifted with the ball;</item>
/// <item>the keeper is handled by <see cref="KeeperAi"/>.</item>
/// </list>
/// The AI only writes player intents and calls <see cref="BallActions"/>, like a human controller.
/// </summary>
public sealed class TeamAi
{
    /// <summary>A ball closer than this to its last toucher is "his" (m).</summary>
    private const float PossessionDistance = 3f;

    /// <summary>Contact reach used to estimate interceptions (m).</summary>
    private const float InterceptReach = 0.5f;

    /// <summary>Distance behind the ball from which a carrier pushes it (m).</summary>
    private const float ApproachOffset = 0.45f;

    /// <summary>An opponent closer than this to the ball contests it: no trapping, run into the ball instead (m).</summary>
    private const float ContestedDistance = 1.2f;

    /// <summary>Shots aim this far inside the post furthest from the keeper (m).</summary>
    private const float ShotPostMargin = 0.55f;

    private static readonly float[] DribbleAngles = [0f, 45f, -45f, 90f, -90f];

    private readonly Dictionary<int, AiBrain> _brains = [];
    private readonly List<(Player Player, float Time)> _ranking = [];

    public TeamAi(MatchTeam team, AiLevel level, AiConfig config)
    {
        Team = team ?? throw new ArgumentNullException(nameof(team));
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        foreach (Player player in team.Players)
        {
            _brains[player.Id] = new AiBrain();
        }
    }

    public MatchTeam Team { get; }

    public AiLevel Level { get; }

    public AiConfig Config { get; }

    public AiBrain Brain(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _brains[player.Id];
    }

    /// <summary>Open play. Players for which <paramref name="isHuman"/> is true are left to their controller.</summary>
    public void UpdatePlaying(FootballMatch match, Func<Player, bool> isHuman)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(isHuman);
        FootballWorld world = match.World;
        Player? carrier = Carrier(world);
        bool humanTeam = Team.Players.Any(isHuman);
        bool inPossession = match.PossessionTeam == Team.Index;
        bool ballLive = world.Ball.Status == BallStatus.Live;
        HashSet<int> chasers = carrier is null && !humanTeam && ballLive ? ChooseChasers(match) : [];

        foreach (Player player in Team.Players)
        {
            if (isHuman(player))
            {
                continue;
            }

            AiBrain brain = _brains[player.Id];
            if (player.IsGoalkeeper)
            {
                KeeperAi.Update(match, this, player, brain);
            }
            else if (ReferenceEquals(player, carrier))
            {
                UpdateCarrier(match, player, brain);
            }
            else if (chasers.Contains(player.Id))
            {
                UpdateChaser(match, player, brain);
            }
            else
            {
                Vector2 ball = VectorMath.Horizontal(world.Ball.Position);
                UpdatePositioning(match, player, brain, Team.FormationTarget(Team.SlotOf(player), ball, inPossession, world.Pitch), ball);
            }
        }
    }

    /// <summary>Moves a player towards a target (8-way, like a joystick) and faces <paramref name="lookAt"/> once arrived.</summary>
    public void UpdatePositioning(FootballMatch match, Player player, AiBrain brain, Vector2 target, Vector2 lookAt)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(brain);
        FootballWorld world = match.World;
        brain.Enter(AiState.Positioning, world.Tick);
        if (world.Tick >= brain.NextDecisionTick || brain.Target == Vector2.Zero)
        {
            brain.Target = target;
            brain.NextDecisionTick = world.Tick + Ticks(world, Level.DecisionInterval);
        }

        MoveTo(player, brain.Target, Config.Positioning.ArriveRadius, lookAt);
    }

    /// <summary>Takes a restart for the team: chooses a target and kicks with the shared set-piece action.</summary>
    public void TakeRestart(FootballMatch match, Restart restart, Player taker)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(taker);
        FootballWorld world = match.World;
        Vector2 from = restart.Spot;
        Vector2 target;
        switch (restart.Kind)
        {
            case SetPieceKind.KickOff:
                target = Team.Players.Where(p => p != taker && !p.IsGoalkeeper)
                    .OrderBy(p => Vector2.DistanceSquared(p.Position, from)).First().Position;
                break;
            case SetPieceKind.Corner:
                float line = world.Pitch.GoalLineY(Team.Attacks);
                float inward = -Pitch.PitchGeometry.Outward(Team.Attacks);
                target = new Vector2(world.Random.Range(-5f, 5f), line + (inward * world.Random.Range(6f, 11f)));
                break;
            case SetPieceKind.GoalKick:
                target = BestLongTarget(match, from) ?? (from + (restart.Inward * 40f));
                break;
            default:
                target = BestPass(match, taker, from, restart.Inward, 4f, 22f)?.Target ?? (from + (restart.Inward * 10f));
                break;
        }

        Vector2 direction = Rotate(Vector2.Normalize(target - from), Level.PassErrorDegrees * world.Random.NextSigned());
        float charge = BallActions.SetPieceChargeForDistance(world, restart.Kind, Vector2.Distance(from, target));
        BallActions.TakeSetPiece(world, taker, restart.Kind, direction, charge);
        _brains[taker.Id].Decision = $"restart {restart.Kind}";
    }

    /// <summary>The team player who has the ball at his feet, if any.</summary>
    public Player? Carrier(FootballWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Ball.Status != BallStatus.Live)
        {
            return null;
        }

        foreach (Player player in Team.Players)
        {
            if (world.Ball.LastToucherId == player.Id && world.DistanceToBall(player) < PossessionDistance && !player.IsGoalkeeper)
            {
                return player;
            }
        }

        return null;
    }

    internal static void MoveTo(Player player, Vector2 target, float arriveRadius, Vector2 lookAt)
    {
        Vector2 offset = target - player.Position;
        if (offset.Length() > arriveRadius)
        {
            Direction8 direction = Direction8Extensions.FromVector(offset, 1e-3f);
            player.Intent = new PlayerIntent { Move = direction, Face = direction };
        }
        else
        {
            Vector2 look = lookAt - player.Position;
            Direction8 face = look.LengthSquared() > 0.04f ? Direction8Extensions.FromVector(look, 1e-3f) : Direction8.None;
            player.Intent = new PlayerIntent { Move = Direction8.None, Face = face };
        }
    }

    internal static long Ticks(FootballWorld world, float seconds) => (long)MathF.Ceiling(seconds / world.StepSeconds);

    internal static Vector2 Rotate(Vector2 v, float degrees) => VectorMath.Rotate(v, degrees * MathF.PI / 180f);

    internal static float TopSpeed(FootballWorld world, Player player) =>
        PlayerMovement.TopSpeed(player, world.Content.Physics.Movement, world.Content.PlayerRules);

    private HashSet<int> ChooseChasers(FootballMatch match)
    {
        FootballWorld world = match.World;
        _ranking.Clear();
        foreach (Player player in Team.Players)
        {
            // A player who has just lost the ball needs a moment to recover before chasing again.
            bool beaten = world.Ball.LastToucherId != player.Id && world.SecondsSince(player.LastTouchTick) < Level.ReactionSeconds * 2f;
            if (!player.IsGoalkeeper && !beaten)
            {
                (float time, _) = match.Forecast.Intercept(player, TopSpeed(world, player), Level.ReactionSeconds, InterceptReach);
                _ranking.Add((player, time));
            }
        }

        _ranking.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        int count = match.PossessionTeam == Team.Index ? 1 : Level.Chasers;
        return [.. _ranking.Take(count).Select(entry => entry.Player.Id)];
    }

    private void UpdateChaser(FootballMatch match, Player player, AiBrain brain)
    {
        FootballWorld world = match.World;
        brain.Enter(AiState.Chasing, world.Tick);
        (_, Vector2 point) = match.Forecast.Intercept(player, TopSpeed(world, player), Level.ReactionSeconds, InterceptReach);
        Vector2 ball = VectorMath.Horizontal(world.Ball.Position);
        Vector2 toBall = ball - player.Position;
        bool behindBall = Vector2.Dot(toBall, Team.AttackDirection) > 0.3f;
        bool slow = world.Ball.Velocity.Length() < 4f && world.Ball.Position.Z < world.Controls.Contact.FootHeight;
        brain.Target = slow && behindBall ? ball - (Team.AttackDirection * ApproachOffset) : point;
        MoveTo(player, brain.Target, 0.2f, ball);

        // Control a free ball rather than knock it on when it arrives fast, high, or from the wrong side.
        // A ball at an opponent's feet is not trapped but run into: the contact pushes it away (block tackle).
        if (toBall.Length() < 3.5f && !(slow && behindBall) && !OpponentControls(match, ball))
        {
            player.Intent.TrapReady = true;
            brain.Decision = "chase (control)";
        }
        else
        {
            brain.Decision = "chase";
        }
    }

    private void UpdateCarrier(FootballMatch match, Player player, AiBrain brain)
    {
        FootballWorld world = match.World;
        Vector2 ball = VectorMath.Horizontal(world.Ball.Position);
        Vector2 goal = world.Pitch.GoalCentre(Team.Attacks);
        if (player.LastTouchKind == BallTouchKind.Trap && brain.State != AiState.Holding && world.SecondsSince(player.LastTouchTick) < 0.5f)
        {
            brain.Enter(AiState.Holding, world.Tick);
        }

        if (brain.State == AiState.Holding)
        {
            Direction8 facing = Direction8Extensions.FromVector(goal - player.Position, 1e-3f);
            if (!BallActions.HasControl(world, player, facing))
            {
                brain.Enter(AiState.Dribbling, world.Tick);
            }
            else if (world.SecondsSince(brain.StateTick) < Level.HoldSeconds)
            {
                player.Intent = default;
                BallActions.KeepAtFeet(world, player, facing);
                brain.Decision = "hold";
                return;
            }
            else
            {
                ActWithStoppedBall(match, player, brain, goal);
                return;
            }
        }

        brain.Enter(AiState.Dribbling, world.Tick);
        if (BallActions.CanShoot(world, player) && InShootingPosition(world, ball, goal))
        {
            Shoot(match, player, goal);
            brain.Decision = "shot";
            return;
        }

        if (world.Tick >= brain.NextDecisionTick || brain.DribbleDirection == Vector2.Zero)
        {
            brain.DribbleDirection = ChooseDribbleDirection(match, player);
            brain.NextDecisionTick = world.Tick + Ticks(world, Level.DecisionInterval);
        }

        Vector2 direction = brain.DribbleDirection;
        Vector2 toBall = ball - player.Position;
        bool ballAhead = Vector2.Dot(toBall, direction) > 0.15f && MathF.Abs(Vector2.Dot(toBall, VectorMath.Perpendicular(direction))) < 0.45f;
        brain.Target = ballAhead ? player.Position + (direction * 5f) : ball - (direction * ApproachOffset);
        MoveTo(player, brain.Target, 0.05f, ball);
        if (ballAhead)
        {
            Direction8 run = Direction8Extensions.FromVector(direction, 1e-3f);
            player.Intent = new PlayerIntent { Move = run, Face = run };
        }

        // A good option (under pressure, or a clearly better placed teammate ahead): stop the ball at the next touch, then pass.
        PassOption? option = BestPass(match, player, player.Position, Vector2.Zero, Config.Positioning.MinPassDistance, Config.Positioning.MaxPassDistance);
        bool pressed = UnderPressure(match, player);
        if (option is { } pass && !OpponentControls(match, ball)
            && (pressed ? pass.Score >= PassThreshold(player) : pass.Score >= PassThreshold(player) * 1.6f))
        {
            player.Intent.TrapReady = true;
            brain.Decision = "stop to pass";
        }
        else
        {
            brain.Decision = "dribble";
        }
    }

    private void ActWithStoppedBall(FootballMatch match, Player player, AiBrain brain, Vector2 goal)
    {
        FootballWorld world = match.World;
        Vector2 ball = VectorMath.Horizontal(world.Ball.Position);
        player.Intent = default;
        if (InShootingPosition(world, ball, goal))
        {
            Shoot(match, player, goal);
            brain.Decision = "shot from stop";
        }
        else if (BestPass(match, player, player.Position, Vector2.Zero, Config.Positioning.MinPassDistance, Config.Positioning.MaxPassDistance) is { } option
            && option.Score >= PassThreshold(player) * 0.6f)
        {
            float distance = Vector2.Distance(player.Position, option.Target);
            Vector2 direction = Rotate(Vector2.Normalize(option.Target - player.Position), Level.PassErrorDegrees * world.Random.NextSigned());
            BallActions.Pass(world, player, direction, BallActions.ChargeForDistance(world, player, distance * 1.15f));
            brain.Decision = "pass";
        }
        else
        {
            BallActions.DribbleAway(world, player, ChooseDribbleDirection(match, player));
            brain.Decision = "dribble away";
        }

        brain.Enter(AiState.Dribbling, world.Tick);
    }

    private void Shoot(FootballMatch match, Player player, Vector2 goal)
    {
        FootballWorld world = match.World;
        MatchTeam opponents = match.Teams[1 - Team.Index];
        float side = opponents.Goalkeeper.Position.X > goal.X ? -1f : 1f;
        var aim = new Vector2(goal.X + (side * (world.Pitch.GoalHalfWidth - ShotPostMargin)), goal.Y);
        float error = Level.ShotErrorDegrees * (1f - (0.5f * player.Ratings.Shooting)) * world.Random.NextSigned();
        Vector2 direction = Rotate(Vector2.Normalize(aim - player.Position), error);
        BallActions.Shoot(world, player, direction);
    }

    private bool InShootingPosition(FootballWorld world, Vector2 ball, Vector2 goal)
    {
        Vector2 toGoal = goal - ball;
        float distance = toGoal.Length();
        return distance <= Level.ShootRange && Vector2.Dot(toGoal / distance, Team.AttackDirection) > 0.55f
            && MathF.Abs(ball.X) < world.Pitch.HalfWidth - 2f;
    }

    /// <summary>True when an opponent is right on the ball (it is his to keep or to lose).</summary>
    private bool OpponentControls(FootballMatch match, Vector2 ball)
    {
        foreach (Player opponent in match.Teams[1 - Team.Index].Players)
        {
            if (Vector2.DistanceSquared(opponent.Position, ball) < ContestedDistance * ContestedDistance)
            {
                return true;
            }
        }

        return false;
    }

    private bool UnderPressure(FootballMatch match, Player player)
    {
        float radius = Config.Positioning.PressureRadius;
        return match.Teams[1 - Team.Index].Players.Any(o => Vector2.DistanceSquared(o.Position, player.Position) < radius * radius);
    }

    /// <summary>Players with flair keep the ball longer: they need a better option before passing.</summary>
    private float PassThreshold(Player player) => Config.Positioning.PassScoreThreshold * (0.7f + (0.6f * player.Ratings.Flair));

    /// <summary>Scores teammates as pass targets: forward progress, free space around them and a clear lane.</summary>
    private PassOption? BestPass(FootballMatch match, Player passer, Vector2 from, Vector2 halfPlane, float minDistance, float maxDistance)
    {
        PassOption? best = null;
        AiPositioning p = Config.Positioning;
        MatchTeam opponents = match.Teams[1 - Team.Index];
        foreach (Player mate in Team.Players)
        {
            if (mate == passer || mate.IsGoalkeeper)
            {
                continue;
            }

            Vector2 target = mate.Position + (mate.Velocity * 0.4f);
            Vector2 offset = target - from;
            float distance = offset.Length();
            if (distance < minDistance || distance > maxDistance || (halfPlane != Vector2.Zero && Vector2.Dot(offset, halfPlane) < 0f))
            {
                continue;
            }

            float space = float.MaxValue;
            float lane = float.MaxValue;
            foreach (Player opponent in opponents.Players)
            {
                space = MathF.Min(space, Vector2.Distance(opponent.Position, target));
                lane = MathF.Min(lane, DistanceToSegment(opponent.Position, from, target));
            }

            if (lane < p.LaneClearance)
            {
                continue;
            }

            float progress = Vector2.Dot(offset, Team.AttackDirection);
            float score = (progress * 0.35f) + (MathF.Min(space, 8f) * 0.6f) + (MathF.Min(lane, 6f) * 0.3f);
            if (best is null || score > best.Value.Score)
            {
                best = new PassOption(mate, target, score);
            }
        }

        return best;
    }

    /// <summary>For long kicks: the most advanced teammate within reach, or null.</summary>
    internal Vector2? LongTarget(FootballMatch match, Vector2 from) => BestLongTarget(match, from);

    private Vector2? BestLongTarget(FootballMatch match, Vector2 from)
    {
        Player? best = null;
        float bestProgress = float.MinValue;
        foreach (Player mate in Team.Players)
        {
            float distance = Vector2.Distance(mate.Position, from);
            if (mate.IsGoalkeeper || distance < 15f || distance > 40f)
            {
                continue;
            }

            float space = float.MaxValue;
            foreach (Player opponent in match.Teams[1 - Team.Index].Players)
            {
                space = MathF.Min(space, Vector2.Distance(opponent.Position, mate.Position));
            }

            if (space < 3f)
            {
                continue;
            }

            float progress = Vector2.Dot(mate.Position - from, Team.AttackDirection);
            if (progress > bestProgress)
            {
                bestProgress = progress;
                best = mate;
            }
        }

        return best?.Position;
    }

    private Vector2 ChooseDribbleDirection(FootballMatch match, Player player)
    {
        FootballWorld world = match.World;
        AiPositioning p = Config.Positioning;
        MatchTeam opponents = match.Teams[1 - Team.Index];
        Vector2 best = Team.AttackDirection;
        float bestScore = float.MinValue;
        foreach (float angle in DribbleAngles)
        {
            Vector2 direction = Rotate(Team.AttackDirection, angle);
            Vector2 probe = player.Position + (direction * p.DribbleProbe);
            float score = Vector2.Dot(direction, Team.AttackDirection) * 2f;
            float nearest = float.MaxValue;
            foreach (Player opponent in opponents.Players)
            {
                nearest = MathF.Min(nearest, Vector2.Distance(opponent.Position, probe));
            }

            score += MathF.Min(nearest, 6f) * 0.5f;
            if (MathF.Abs(probe.X) > world.Pitch.HalfWidth - p.TouchlineMargin)
            {
                score -= 4f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Math.Clamp(Vector2.Dot(point - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return Vector2.Distance(point, a + (ab * t));
    }

    private readonly record struct PassOption(Player Mate, Vector2 Target, float Score);
}
