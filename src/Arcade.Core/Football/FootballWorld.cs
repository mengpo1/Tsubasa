using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;
using Arcade.Core.Simulation;
using BallEntity = Arcade.Core.Football.Ball.Ball;

namespace Arcade.Core.Football;

/// <summary>
/// The physical football world: pitch, ball and players, advanced by fixed steps.
/// Controllers (human or AI) write player intents and call the ball actions (<see cref="Kick"/>,
/// <see cref="HoldBall"/>) before each <see cref="Step"/>; the world then moves everybody and resolves
/// ball contacts. Rules of the match (restarts, score, clock) live outside, on top of this class.
/// </summary>
public sealed class FootballWorld
{
    /// <summary>How long the visible "kicking" action lasts (animation only, no gameplay effect).</summary>
    private const float KickActionSeconds = 0.25f;

    /// <summary>Speed above which a player is shown running (animation only).</summary>
    private const float RunningAnimationSpeed = 0.4f;

    /// <summary>Distance in front of a carrier where the ball is held (m).</summary>
    private const float CarryOffset = 0.3f;

    private readonly List<Player> _players = [];
    private readonly BallPhysics _ballPhysics;

    public FootballWorld(FootballContent content, PitchDefinition pitch, ulong seed)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ArgumentNullException.ThrowIfNull(pitch);
        StepSeconds = 1f / content.Physics.StepsPerSecond;
        _ballPhysics = new BallPhysics(content.Physics.Ball, content.Physics.Gravity);
        Random = new GameRandom(seed);
        Pitch = new PitchGeometry(pitch);
        Surface = content.Surfaces[pitch.Surface];
    }

    public FootballContent Content { get; }

    public ControlConfig Controls => Content.Controls;

    public PitchGeometry Pitch { get; private set; }

    public SurfaceDefinition Surface { get; private set; }

    public BallEntity Ball { get; } = new();

    public BallPhysics BallPhysics => _ballPhysics;

    public IReadOnlyList<Player> Players => _players;

    public GameRandom Random { get; }

    public MatchEventBuffer Events { get; } = new();

    /// <summary>Wind velocity (m/s) acting on the ball.</summary>
    public Vector2 Wind { get; set; }

    /// <summary>Number of steps simulated so far. All game time derives from it.</summary>
    public long Tick { get; private set; }

    public float StepSeconds { get; }

    /// <summary>Goal the ball currently sits in, if any.</summary>
    public GoalEnd? BallInGoal { get; private set; }

    public float SecondsSince(long tick) => (Tick - tick) * StepSeconds;

    public Player AddPlayer(int teamIndex, PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var player = new Player(_players.Count, teamIndex, profile, PlayerRatings.From(profile.Attributes, Content.PlayerRules.RatingScale));
        _players.Add(player);
        return player;
    }

    /// <summary>Changes pitch (and therefore surface) between steps, e.g. from the training menu.</summary>
    public void SetPitch(PitchDefinition pitch)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        Pitch = new PitchGeometry(pitch);
        Surface = Content.Surfaces[pitch.Surface];
    }

    public void SetSurface(SurfaceDefinition surface) => Surface = surface ?? throw new ArgumentNullException(nameof(surface));

    /// <summary>Advances the world by one fixed step.</summary>
    public void Step()
    {
        float dt = StepSeconds;
        foreach (Player player in _players)
        {
            player.PreviousPosition = player.Position;
            PlayerMovement.Step(player, Content.Physics.Movement, Content.PlayerRules, Surface, dt);
            UpdateAction(player);
        }

        ResolvePlayerCollisions();
        KeepPlayersOutOfGoals();
        Ball.PreviousPosition = Ball.State.Position;
        switch (Ball.Status)
        {
            case BallStatus.Live:
                StepBall(dt);
                break;
            case BallStatus.Carried:
                FollowCarrier();
                break;
            case BallStatus.Dead:
                break;
        }

        Ball.ControlAcceleration = Vector3.Zero;
        UpdateGoalState();
        Tick++;
    }

    /// <summary>Distance on the ground between a player and the ball centre.</summary>
    public float DistanceToBall(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Vector2.Distance(player.Position, VectorMath.Horizontal(Ball.Position));
    }

    /// <summary>
    /// Kicks the ball: sets its velocity, records the touch and opens the aftertouch window for the kicker.
    /// Callers check the reach conditions; this method only applies the result.
    /// </summary>
    public void Kick(Player player, Vector2 direction, float horizontalSpeed, float lift, KickKind kind, float aftertouchScale)
    {
        ArgumentNullException.ThrowIfNull(player);
        Vector2 unit = VectorMath.SafeNormalize(direction);
        Ball.Status = BallStatus.Live;
        Ball.CarrierId = Player.NoPlayer;
        Ball.State.Velocity = VectorMath.ClampLength(new Vector3(unit * horizontalSpeed, lift), Content.Physics.Ball.MaxSpeed);
        Ball.State.Position.Z = MathF.Max(0f, Ball.State.Position.Z);
        RecordTouch(player, BallTouchKind.Kick);
        Ball.Aftertouch = aftertouchScale > 0f
            ? new AftertouchWindow(player.Id, Tick, unit, aftertouchScale)
            : AftertouchWindow.None;
        player.Action = PlayerAction.Kicking;
        player.ActionTick = Tick;
        Events.Add(new MatchEvent(MatchEventType.BallKicked, Tick, player.Id, Ball.Position, (int)kind, horizontalSpeed));
    }

    /// <summary>
    /// Steers a controlled ball towards a point at the player's feet. The ball keeps moving physically
    /// (no teleport); its velocity is driven towards the hold point.
    /// </summary>
    public void HoldBall(Player player, Vector2 holdPoint)
    {
        ArgumentNullException.ThrowIfNull(player);
        TrapConfig trap = Controls.Trap;
        Vector2 toTarget = holdPoint - VectorMath.Horizontal(Ball.Position);
        Vector2 velocity = VectorMath.ClampLength(player.Velocity + (toTarget * trap.HoldGain), trap.MaxHoldSpeed);
        float vertical = Ball.State.Position.Z > 0f ? Ball.State.Velocity.Z : 0f;
        Ball.State.Velocity = new Vector3(velocity, vertical);
        player.Action = PlayerAction.Controlling;
    }

    /// <summary>Places the ball for a restart: it stays still and untouchable until someone kicks it.</summary>
    public void PlaceDeadBall(Vector2 spot)
    {
        Ball.Status = BallStatus.Dead;
        Ball.CarrierId = Player.NoPlayer;
        Ball.State.Position = new Vector3(spot, 0f);
        Ball.State.Velocity = Vector3.Zero;
        Ball.PreviousPosition = Ball.State.Position;
        Ball.Aftertouch = AftertouchWindow.None;
        BallInGoal = null;
    }

    /// <summary>Puts the ball in a player's hands (goalkeeper, throw-in). It follows him until he kicks it.</summary>
    public void CarryBall(Player player, float height)
    {
        ArgumentNullException.ThrowIfNull(player);
        Ball.Status = BallStatus.Carried;
        Ball.CarrierId = player.Id;
        Ball.CarryHeight = height;
        Ball.Aftertouch = AftertouchWindow.None;
        FollowCarrier();
        Ball.PreviousPosition = Ball.State.Position;
    }

    /// <summary>Adds an aftertouch acceleration for the coming step.</summary>
    public void ApplyBallAcceleration(Vector3 acceleration) => Ball.AddControlAcceleration(acceleration);

    /// <summary>Stops the aftertouch window (e.g. option disabled).</summary>
    public void CancelAftertouch() => Ball.Aftertouch = AftertouchWindow.None;

    private void StepBall(float dt)
    {
        float maxPlayerSpeed = 0f;
        foreach (Player player in _players)
        {
            maxPlayerSpeed = MathF.Max(maxPlayerSpeed, player.Speed);
        }

        int substeps = _ballPhysics.SubstepCount(Ball.State.Velocity.Length() + maxPlayerSpeed, dt);
        float h = dt / substeps;
        float radius = Content.Physics.Ball.Radius;
        for (int i = 1; i <= substeps; i++)
        {
            Vector2 before = VectorMath.Horizontal(Ball.State.Position);
            float impactSpeed = -Ball.State.Velocity.Z;
            BallImpacts impacts = _ballPhysics.Integrate(ref Ball.State, Surface, Wind, Ball.ControlAcceleration, h, Random);
            impacts |= GoalFrameCollider.Resolve(ref Ball.State, Pitch, Content.Physics.GoalFrame, Content.Physics.Boards, radius);
            Ball.DistanceTravelled += Vector2.Distance(before, VectorMath.Horizontal(Ball.State.Position));
            if (impacts != BallImpacts.None)
            {
                ReportImpacts(impacts, impactSpeed);
            }

            float t = (float)i / substeps;
            foreach (Player player in _players)
            {
                if (CanTouch(player))
                {
                    Vector2 position = Vector2.Lerp(player.PreviousPosition, player.Position, t);
                    bool hands = UsesHands(player, position);
                    if (Overlaps(position, hands))
                    {
                        ResolveContact(player, position, hands);
                        if (Ball.Status != BallStatus.Live)
                        {
                            return;
                        }
                    }
                }
            }
        }
    }

    private void FollowCarrier()
    {
        Player carrier = _players[Ball.CarrierId];
        Vector2 hands = carrier.Position + (carrier.Facing.ToVector() * CarryOffset);
        Ball.State.Position = new Vector3(hands, Ball.CarryHeight);
        Ball.State.Velocity = new Vector3(carrier.Velocity, 0f);
    }

    /// <summary>Players are solid: overlapping bodies are pushed apart and their closing speed removed.</summary>
    private void ResolvePlayerCollisions()
    {
        float minDistance = Content.Physics.Movement.BodyRadius * 2f;
        float minSquared = minDistance * minDistance;
        for (int i = 0; i < _players.Count; i++)
        {
            for (int j = i + 1; j < _players.Count; j++)
            {
                Player a = _players[i];
                Player b = _players[j];
                Vector2 offset = b.Position - a.Position;
                float distanceSquared = offset.LengthSquared();
                if (distanceSquared >= minSquared)
                {
                    continue;
                }

                float distance = MathF.Sqrt(distanceSquared);
                Vector2 normal = distance > 1e-4f ? offset / distance : new Vector2(1f, 0f);
                Vector2 push = normal * ((minDistance - distance) / 2f);
                a.Position -= push;
                b.Position += push;
                float closing = Vector2.Dot(b.Velocity - a.Velocity, normal);
                if (closing < 0f)
                {
                    Vector2 impulse = normal * (closing / 2f);
                    a.Velocity += impulse;
                    b.Velocity -= impulse;
                }
            }
        }
    }

    /// <summary>
    /// The goal mouth is a wall for players: nobody walks into the net (a keeper holding the ball can therefore
    /// never carry it over his own line by accident). Players can still run behind the goal line beside the goals.
    /// </summary>
    private void KeepPlayersOutOfGoals()
    {
        float body = Content.Physics.Movement.BodyRadius;
        foreach (Player player in _players)
        {
            if (MathF.Abs(player.Position.X) > Pitch.GoalHalfWidth + Pitch.PostRadius + body)
            {
                continue;
            }

            foreach (GoalEnd end in PitchGeometry.Ends)
            {
                float outward = PitchGeometry.Outward(end);
                float depth = (player.Position.Y - Pitch.GoalLineY(end)) * outward;
                if (depth > -body && depth < Pitch.GoalDepth + body)
                {
                    player.Position = new Vector2(player.Position.X, Pitch.GoalLineY(end) - (outward * body));
                    if (player.Velocity.Y * outward > 0f)
                    {
                        player.Velocity = new Vector2(player.Velocity.X, 0f);
                    }
                }
            }
        }
    }

    private bool CanTouch(Player player)
    {
        if (player.Intent.KeepsBall)
        {
            return false;
        }

        return Ball.LastToucherId != player.Id || SecondsSince(player.LastTouchTick) >= Controls.Contact.TouchCooldown;
    }

    private bool UsesHands(Player player, Vector2 position) =>
        player.IsGoalkeeper && Pitch.IsInPenaltyArea(position, player.DefendsEnd!.Value);

    private bool Overlaps(Vector2 playerPosition, bool hands)
    {
        ContactConfig contact = Controls.Contact;
        float bottom = Ball.State.Position.Z;
        if (hands)
        {
            float keeperReach = Controls.Keeper.Reach;
            return bottom <= Controls.Keeper.MaxHeight
                && Vector2.DistanceSquared(playerPosition, VectorMath.Horizontal(Ball.State.Position)) < keeperReach * keeperReach;
        }

        if (bottom > contact.BodyHeight)
        {
            return false;
        }

        float reach = bottom <= contact.FootHeight ? contact.FootRadius : contact.BodyRadius;
        return Vector2.DistanceSquared(playerPosition, VectorMath.Horizontal(Ball.State.Position)) < reach * reach;
    }

    /// <summary>Applies the outcome of a player/ball contact: catch, trap, dribble push or deflection.</summary>
    private void ResolveContact(Player player, Vector2 playerPosition, bool hands)
    {
        ControlConfig controls = Controls;
        ref BallState ball = ref Ball.State;
        Vector2 ballPosition = VectorMath.Horizontal(ball.Position);
        Vector2 offset = ballPosition - playerPosition;
        Vector2 normal = offset.LengthSquared() > 1e-8f ? Vector2.Normalize(offset) : player.Facing.ToVector();
        Vector2 relative = VectorMath.Horizontal(ball.Velocity) - player.Velocity;
        float relativeSpeed3 = MathF.Sqrt(relative.LengthSquared() + (ball.Velocity.Z * ball.Velocity.Z));
        bool atFeet = ball.Position.Z <= controls.Contact.FootHeight;

        BallTouchKind kind;
        if (hands && relativeSpeed3 <= controls.Keeper.CatchMaxSpeed)
        {
            RecordTouch(player, BallTouchKind.Catch);
            CarryBall(player, controls.Keeper.CarryHeight);
            return;
        }

        if (hands)
        {
            // Parry: the ball rebounds off the keeper's hands, harder than off a body.
            float normalSpeed = MathF.Min(Vector2.Dot(relative, normal), 0f);
            Vector2 tangent = relative - (normal * normalSpeed);
            Vector2 parried = (tangent * controls.Deflection.TangentialKeep) - (normal * normalSpeed * controls.Keeper.ParryRestitution);
            ball.Velocity = new Vector3(player.Velocity + parried, MathF.Abs(ball.Velocity.Z) * controls.Keeper.ParryRestitution);
            kind = BallTouchKind.Deflection;
        }
        else if (player.Intent.TrapReady && ball.Position.Z <= controls.Trap.MaxBallHeight && relativeSpeed3 <= controls.Trap.MaxRelativeSpeed)
        {
            // Stop the ball: it now moves with the player and drops to his feet.
            ball.Velocity = new Vector3(player.Velocity, MathF.Min(ball.Velocity.Z, 0f) * 0.2f);
            kind = BallTouchKind.Trap;
        }
        else if (atFeet && relative.Length() <= controls.Dribble.MaxRelativeSpeed
            && Vector2.Dot(normal, player.Facing.ToVector()) >= controls.Dribble.FrontDot)
        {
            // Dribble: the ball is pushed ahead in the facing direction; faster runs push it further.
            Vector2 facing = player.Facing.ToVector();
            float runSpeed = MathF.Max(0f, Vector2.Dot(player.Velocity, facing));
            float speed = controls.Dribble.BaseSpeed + (controls.Dribble.RunFactor * runSpeed);
            ball.Velocity = new Vector3(facing * speed, 0f);
            ball.Position.Z = 0f;
            kind = BallTouchKind.Dribble;
        }
        else
        {
            float normalSpeed = Vector2.Dot(relative, normal);
            if (normalSpeed >= 0f)
            {
                return; // Already separating: no touch.
            }

            Vector2 tangent = relative - (normal * normalSpeed);
            Vector2 bounced = (tangent * controls.Deflection.TangentialKeep) - (normal * normalSpeed * controls.Deflection.Restitution);
            float vertical = atFeet ? ball.Velocity.Z : ball.Velocity.Z * controls.Deflection.TangentialKeep;
            ball.Velocity = new Vector3(player.Velocity + bounced, vertical);
            kind = BallTouchKind.Deflection;
        }

        RecordTouch(player, kind);
    }

    private void RecordTouch(Player player, BallTouchKind kind)
    {
        player.LastTouchTick = Tick;
        player.LastTouchKind = kind;
        Ball.LastToucherId = player.Id;
        Ball.LastTouchTick = Tick;
        if (kind != BallTouchKind.Kick)
        {
            Ball.Aftertouch = AftertouchWindow.None;
            Events.Add(new MatchEvent(MatchEventType.BallTouched, Tick, player.Id, Ball.Position, (int)kind));
        }
    }

    private void ReportImpacts(BallImpacts impacts, float impactSpeed)
    {
        Vector3 position = Ball.Position;
        if ((impacts & BallImpacts.Bounce) != 0)
        {
            Events.Add(new MatchEvent(MatchEventType.BallBounced, Tick, Player.NoPlayer, position, 0, impactSpeed));
        }

        if ((impacts & BallImpacts.Post) != 0)
        {
            Events.Add(new MatchEvent(MatchEventType.PostHit, Tick, Player.NoPlayer, position));
        }

        if ((impacts & BallImpacts.Crossbar) != 0)
        {
            Events.Add(new MatchEvent(MatchEventType.CrossbarHit, Tick, Player.NoPlayer, position));
        }

        if ((impacts & BallImpacts.Net) != 0)
        {
            Events.Add(new MatchEvent(MatchEventType.NetHit, Tick, Player.NoPlayer, position));
        }

        if ((impacts & BallImpacts.Board) != 0)
        {
            Events.Add(new MatchEvent(MatchEventType.BoardHit, Tick, Player.NoPlayer, position));
        }
    }

    private void UpdateGoalState()
    {
        if (Pitch.IsBallInGoal(Ball.Position, Content.Physics.Ball.Radius, out GoalEnd end))
        {
            if (BallInGoal is null)
            {
                BallInGoal = end;
                Events.Add(new MatchEvent(MatchEventType.Goal, Tick, Ball.LastToucherId, Ball.Position, (int)end));
            }
        }
        else
        {
            BallInGoal = null;
        }
    }

    private void UpdateAction(Player player)
    {
        bool kicking = player.Action == PlayerAction.Kicking && SecondsSince(player.ActionTick) < KickActionSeconds;
        if (player.Intent.KeepsBall)
        {
            player.Action = PlayerAction.Controlling;
        }
        else if (!kicking)
        {
            player.Action = player.Speed > RunningAnimationSpeed ? PlayerAction.Running : PlayerAction.Idle;
        }
    }
}
