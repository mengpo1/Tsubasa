using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football.AI;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Tactics;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Match;

/// <summary>A human player in the match: his input control set, his team and his contextual controller.</summary>
public sealed class HumanControl
{
    internal HumanControl(int controlSet, int teamIndex, ContextualController controller)
    {
        ControlSet = controlSet;
        TeamIndex = teamIndex;
        Controller = controller;
    }

    public int ControlSet { get; }

    public int TeamIndex { get; }

    public ContextualController Controller { get; }

    public Player Player => Controller.Player;

    internal long LastSwitchTick { get; set; } = long.MinValue / 2;
}

/// <summary>
/// A football match on top of <see cref="FootballWorld"/>: teams, clock, score and the state machine of the
/// game (kick-off, open play, ball out, restarts, goals, half-time, full time). Independent of rendering:
/// the desktop app only feeds inputs and reads the state.
/// </summary>
public sealed class FootballMatch
{
    /// <summary>Kick-off positioning ends early when every player is this close to his spot (m).</summary>
    private const float SetupArriveDistance = 1.2f;

    /// <summary>The restart taker is considered at the ball within this distance (m).</summary>
    private const float TakerArriveDistance = 0.4f;

    private readonly MatchTeam[] _teams;
    private readonly TeamAi[] _ai;
    private readonly List<HumanControl> _humans = [];
    private readonly int _firstKickOffTeam;
    private long _phaseTick;
    private HumanControl? _humanTaker;

    public FootballMatch(FootballContent content, MatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(content);
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        RulesetDefinition ruleset = Find(content.Rulesets, settings.RulesetId, "ruleset");
        Ruleset = settings.HalfLengthSeconds is float half ? ruleset with { HalfLengthSeconds = half } : ruleset;
        World = new FootballWorld(content, Find(content.Pitches, settings.PitchId, "pitch"), settings.Seed);
        AiLevel level = content.Ai.Level(settings.AiLevel);
        _teams = [CreateTeam(content, 0, settings.HomeTeamId), CreateTeam(content, 1, settings.AwayTeamId)];
        _ai = [new TeamAi(_teams[0], level, content.Ai), new TeamAi(_teams[1], level, content.Ai)];
        foreach (HumanSlot slot in settings.Humans)
        {
            if (slot.TeamIndex is not (0 or 1))
            {
                throw new ArgumentException($"Human team index must be 0 or 1 (found {slot.TeamIndex}).", nameof(settings));
            }

            Player first = _teams[slot.TeamIndex].KickOffTaker;
            _humans.Add(new HumanControl(slot.ControlSet, slot.TeamIndex, new ContextualController(first, settings.Aftertouch)));
        }

        _teams[0].SetAttackedGoal(GoalEnd.Top);
        _teams[1].SetAttackedGoal(GoalEnd.Bottom);
        _firstKickOffTeam = World.Random.NextFloat() < 0.5f ? 0 : 1;
        Half = 1;
        World.Events.Add(new MatchEvent(MatchEventType.HalfStarted, World.Tick, Player.NoPlayer, Vector3.Zero, 1));
        StartKickOff(_firstKickOffTeam, snap: true);
    }

    public MatchSettings Settings { get; }

    public RulesetDefinition Ruleset { get; }

    public FootballWorld World { get; }

    public IReadOnlyList<MatchTeam> Teams => _teams;

    public IReadOnlyList<HumanControl> Humans => _humans;

    public BallForecast Forecast { get; } = new();

    public MatchPhase Phase { get; private set; }

    /// <summary>1 or 2.</summary>
    public int Half { get; private set; }

    /// <summary>Real seconds played in the current half.</summary>
    public float ClockSeconds { get; private set; }

    /// <summary>Clock as shown to the player, in match minutes (0–90 for the default ruleset).</summary>
    public float DisplayedMinutes =>
        ((Half - 1) * Ruleset.DisplayedHalfMinutes) + (MathF.Min(ClockSeconds / Ruleset.HalfLengthSeconds, 1f) * Ruleset.DisplayedHalfMinutes);

    /// <summary>The restart being prepared or awaited (kick-off included).</summary>
    public Restart? CurrentRestart { get; private set; }

    /// <summary>Player taking the current restart.</summary>
    public Player? Taker { get; private set; }

    /// <summary>Team of the last player who touched the ball, or -1.</summary>
    public int PossessionTeam { get; private set; } = -1;

    public bool IsOver => Phase == MatchPhase.FullTime;

    /// <summary>Seconds spent in the current phase.</summary>
    public float PhaseSeconds => World.SecondsSince(_phaseTick);

    public TeamAi AiFor(int teamIndex) => _ai[teamIndex];

    public bool IsHumanControlled(Player player) => _humans.Exists(human => ReferenceEquals(human.Player, player));

    /// <summary>Advances the match by one fixed step. <paramref name="inputs"/> is indexed by control set.</summary>
    public void Step(ReadOnlySpan<ControlInput> inputs)
    {
        Forecast.Update(World);
        switch (Phase)
        {
            case MatchPhase.KickOffSetup:
                UpdateKickOffSetup();
                break;
            case MatchPhase.RestartSetup:
                UpdateRestartSetup();
                break;
            case MatchPhase.RestartReady:
                UpdateRestartReady(inputs);
                break;
            case MatchPhase.Playing:
                UpdatePlaying(inputs);
                break;
            case MatchPhase.BallOut:
                PositionForRestart(CurrentRestart!.Value, null);
                if (PhaseSeconds >= Ruleset.BallOutWhistleSeconds)
                {
                    BeginRestartSetup();
                }

                break;
            case MatchPhase.GoalCelebration:
                PositionForKickOff();
                if (PhaseSeconds >= Ruleset.GoalCelebrationSeconds)
                {
                    if (ClockSeconds >= Ruleset.HalfLengthSeconds)
                    {
                        EndHalf();
                    }
                    else
                    {
                        StartKickOff(CurrentRestart!.Value.TeamIndex, snap: false);
                    }
                }

                break;
            case MatchPhase.HalfTime:
                StopEveryone();
                if (PhaseSeconds >= Ruleset.HalfTimeSeconds)
                {
                    StartSecondHalf();
                }

                break;
            case MatchPhase.FullTime:
                StopEveryone();
                break;
        }

        World.Step();
        AfterStep();
    }

    private static T Find<T>(IReadOnlyDictionary<string, T> items, string id, string what) =>
        items.TryGetValue(id, out T? item) ? item : throw new ContentException($"Unknown {what} '{id}' (known: {string.Join(", ", items.Keys)}).");

    private MatchTeam CreateTeam(FootballContent content, int index, string teamId)
    {
        TeamDefinition definition = Find(content.Teams, teamId, "team");
        var tactic = Find(content.Tactics, definition.Tactic, "tactic");
        if (definition.Players.Count < MatchTeam.StartingPlayers)
        {
            throw new ContentException($"Team '{teamId}' has {definition.Players.Count} players; {MatchTeam.StartingPlayers} are needed.");
        }

        return new MatchTeam(index, definition, tactic, definition.Players.Take(MatchTeam.StartingPlayers).Select(profile => World.AddPlayer(index, profile)));
    }

    private void Enter(MatchPhase phase)
    {
        Phase = phase;
        _phaseTick = World.Tick;
    }

    private void StartKickOff(int team, bool snap)
    {
        CurrentRestart = new Restart(SetPieceKind.KickOff, team, Vector2.Zero, Vector2.Zero);
        Taker = _teams[team].KickOffTaker;
        World.PlaceDeadBall(Vector2.Zero);
        foreach (HumanControl human in _humans)
        {
            human.Controller.Reset();
        }

        Enter(MatchPhase.KickOffSetup);
        if (snap)
        {
            SnapToKickOffPositions();
        }
    }

    private void UpdateKickOffSetup()
    {
        bool allArrived = PositionForKickOff();
        if (allArrived || PhaseSeconds >= Ruleset.KickOffSetupSeconds)
        {
            SnapToKickOffPositions();
            BeginReady();
        }
    }

    /// <summary>Moves everybody towards the kick-off positions; returns true when all are close.</summary>
    private bool PositionForKickOff()
    {
        bool allArrived = true;
        int kicking = CurrentRestart?.TeamIndex ?? _firstKickOffTeam;
        foreach (MatchTeam team in _teams)
        {
            foreach (Player player in team.Players)
            {
                Vector2 target = team.KickOffPosition(player, team.Index == kicking, World.Pitch);
                TeamAi.MoveTo(player, target, 0.5f, Vector2.Zero);
                allArrived &= Vector2.Distance(player.Position, target) <= SetupArriveDistance;
            }
        }

        return allArrived;
    }

    private void SnapToKickOffPositions()
    {
        int kicking = CurrentRestart!.Value.TeamIndex;
        foreach (MatchTeam team in _teams)
        {
            foreach (Player player in team.Players)
            {
                Vector2 position = team.KickOffPosition(player, team.Index == kicking, World.Pitch);
                player.PlaceAt(position, Direction8Extensions.FromVector(team.AttackDirection, 1e-3f));
            }
        }
    }

    private void BeginRestartSetup()
    {
        Restart restart = CurrentRestart!.Value;
        World.PlaceDeadBall(restart.Spot);
        Taker = ChooseTaker(restart);
        Enter(MatchPhase.RestartSetup);
    }

    private Player ChooseTaker(Restart restart)
    {
        MatchTeam team = _teams[restart.TeamIndex];
        if (restart.Kind == SetPieceKind.GoalKick)
        {
            return team.Goalkeeper;
        }

        return team.Players.Where(p => !p.IsGoalkeeper).MinBy(p => Vector2.DistanceSquared(p.Position, restart.Spot))!;
    }

    private static Vector2 TakerSpot(Restart restart, MatchTeam team) => restart.Kind switch
    {
        SetPieceKind.KickOff => -team.AttackDirection * 0.5f,
        SetPieceKind.ThrowIn => restart.Spot - (restart.Inward * 0.4f),
        _ => restart.Spot - (restart.Inward * 0.5f),
    };

    private void UpdateRestartSetup()
    {
        Restart restart = CurrentRestart!.Value;
        Player taker = Taker!;
        Vector2 spot = TakerSpot(restart, _teams[restart.TeamIndex]);
        PositionForRestart(restart, taker);
        TeamAi.MoveTo(taker, spot, 0.1f, restart.Spot);
        if (Vector2.Distance(taker.Position, spot) <= TakerArriveDistance || PhaseSeconds >= Ruleset.RestartSetupSeconds)
        {
            Direction8 facing = Direction8Extensions.FromVector(restart.Inward, 1e-3f);
            taker.PlaceAt(spot, facing);
            BeginReady();
        }
    }

    private void BeginReady()
    {
        Restart restart = CurrentRestart!.Value;
        Player taker = Taker!;
        MatchTeam team = _teams[restart.TeamIndex];
        Vector2 inward = restart.Kind == SetPieceKind.KickOff ? team.AttackDirection : restart.Inward;
        taker.Facing = Direction8Extensions.FromVector(inward, 1e-3f);
        if (restart.Kind == SetPieceKind.ThrowIn)
        {
            World.CarryBall(taker, World.Controls.SetPieces.ThrowInHeight);
        }

        // Goal kicks are always taken by the computer keeper in this version (KO2: "the keeper takes them automatically").
        _humanTaker = restart.Kind == SetPieceKind.GoalKick ? null : _humans.Find(h => h.TeamIndex == restart.TeamIndex);
        if (_humanTaker is not null)
        {
            _humanTaker.Controller.SetPlayer(taker);
            _humanTaker.Controller.BeginSetPiece(restart.Kind, restart.Kind == SetPieceKind.KickOff ? Vector2.Zero : restart.Inward);
        }

        Enter(MatchPhase.RestartReady);
    }

    private void UpdateRestartReady(ReadOnlySpan<ControlInput> inputs)
    {
        Restart restart = CurrentRestart!.Value;
        Player taker = Taker!;
        PositionForRestart(restart, taker);
        if (_humanTaker is not null)
        {
            ContextualController controller = _humanTaker.Controller;
            controller.Update(Input(inputs, _humanTaker.ControlSet), World);
            bool reacted = controller.LastFirePressTick >= _phaseTick;
            if (!reacted && PhaseSeconds >= Ruleset.HumanRestartAutoSeconds)
            {
                controller.Reset();
                _ai[restart.TeamIndex].TakeRestart(this, restart, taker);
            }
        }
        else
        {
            taker.Intent = new PlayerIntent { Move = Direction8.None, Face = taker.Facing };
            if (PhaseSeconds >= _ai[restart.TeamIndex].Level.RestartDelaySeconds)
            {
                _ai[restart.TeamIndex].TakeRestart(this, restart, taker);
            }
        }
    }

    /// <summary>Everybody except the taker takes his formation position for the restart; opponents keep their distance.</summary>
    private void PositionForRestart(Restart restart, Player? taker)
    {
        float distance = restart.Kind == SetPieceKind.ThrowIn ? Ruleset.ThrowInDistance : Ruleset.RestartDistance;
        foreach (MatchTeam team in _teams)
        {
            TeamAi ai = _ai[team.Index];
            bool taking = team.Index == restart.TeamIndex;
            foreach (Player player in team.Players)
            {
                if (ReferenceEquals(player, taker))
                {
                    continue;
                }

                if (player.IsGoalkeeper)
                {
                    KeeperAi.Update(this, ai, player, ai.Brain(player));
                    continue;
                }

                Vector2 target = restart.Kind switch
                {
                    SetPieceKind.GoalKick => team.SituationTarget(player, taking ? TacticSituation.GoalKickOwn : TacticSituation.GoalKickOpponent, World.Pitch),
                    SetPieceKind.Corner => team.SituationTarget(player, team.CornerSituation(restart.Spot, World.Pitch), World.Pitch),
                    _ => team.TacticTarget(player, restart.Spot, taking, World.Pitch),
                };
                if (team.Index != restart.TeamIndex && Vector2.Distance(target, restart.Spot) < distance)
                {
                    Vector2 away = target - restart.Spot;
                    away = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : -_teams[restart.TeamIndex].AttackDirection;
                    target = restart.Spot + (away * distance);
                }

                ai.UpdatePositioning(this, player, ai.Brain(player), ClampToPitch(target), restart.Spot);
            }
        }
    }

    private void UpdatePlaying(ReadOnlySpan<ControlInput> inputs)
    {
        foreach (HumanControl human in _humans)
        {
            UpdateSelection(human);
            human.Controller.Update(Input(inputs, human.ControlSet), World);
        }

        _ai[0].UpdatePlaying(this, IsHumanControlled);
        _ai[1].UpdatePlaying(this, IsHumanControlled);
    }

    /// <summary>"Playing as a team": control goes to the outfield player nearest to where the ball is going.</summary>
    private void UpdateSelection(HumanControl human)
    {
        ContextualController controller = human.Controller;
        SelectionConfig selection = World.Controls.Selection;
        if (controller.Phase is ControlPhase.Holding or ControlPhase.AtFeet or ControlPhase.SetPiece
            || World.SecondsSince(human.LastSwitchTick) < selection.MinSwitchInterval)
        {
            return;
        }

        Vector2 ball = VectorMath.Horizontal(World.Ball.Position) + (VectorMath.Horizontal(World.Ball.Velocity) * selection.Anticipation);
        Player current = controller.Player;
        Player? best = null;
        float bestDistance = float.MaxValue;
        foreach (Player player in _teams[human.TeamIndex].Players)
        {
            if (player.IsGoalkeeper || (!ReferenceEquals(player, current) && IsHumanControlled(player)))
            {
                continue;
            }

            float distance = Vector2.Distance(player.Position, ball);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = player;
            }
        }

        if (best is not null && !ReferenceEquals(best, current) && bestDistance + selection.SwitchMargin < Vector2.Distance(current.Position, ball))
        {
            controller.SetPlayer(best);
            human.LastSwitchTick = World.Tick;
        }
    }

    private void AfterStep()
    {
        TrackPossession();
        if (Phase == MatchPhase.RestartReady && World.Ball.Status == BallStatus.Live)
        {
            World.Events.Add(new MatchEvent(MatchEventType.RestartTaken, World.Tick, Taker?.Id ?? Player.NoPlayer, World.Ball.Position, (int)CurrentRestart!.Value.Kind));
            Enter(MatchPhase.Playing);
        }
        else if (Phase == MatchPhase.Playing)
        {
            if (World.BallInGoal is GoalEnd end)
            {
                ScoreGoal(end);
            }
            else if (World.Ball.Status == BallStatus.Live && PossessionTeam >= 0
                && Referee.CheckOutOfPlay(World.Ball.Position, World.Content.Physics.Ball.Radius, World.Pitch, PossessionTeam, DefendingTeamOf) is Restart restart)
            {
                CurrentRestart = restart;
                World.Events.Add(new MatchEvent(MatchEventType.BallOut, World.Tick, World.Ball.LastToucherId, World.Ball.Position, (int)restart.Kind, restart.TeamIndex));
                foreach (HumanControl human in _humans)
                {
                    human.Controller.Reset();
                }

                Enter(MatchPhase.BallOut);
            }
        }

        if (ClockRuns())
        {
            ClockSeconds += World.StepSeconds;
            if (ClockSeconds >= Ruleset.HalfLengthSeconds)
            {
                EndHalf();
            }
        }
    }

    private bool ClockRuns() => Phase switch
    {
        MatchPhase.Playing or MatchPhase.BallOut or MatchPhase.RestartSetup => true,
        MatchPhase.RestartReady => CurrentRestart?.Kind != SetPieceKind.KickOff,
        _ => false,
    };

    private int DefendingTeamOf(GoalEnd end) => _teams[0].Defends == end ? 0 : 1;

    private void TrackPossession()
    {
        int toucher = World.Ball.LastToucherId;
        int team = toucher == Player.NoPlayer ? -1 : World.Players[toucher].TeamIndex;
        if (team != PossessionTeam && team >= 0)
        {
            PossessionTeam = team;
            World.Events.Add(new MatchEvent(MatchEventType.PossessionChanged, World.Tick, toucher, World.Ball.Position, team));
        }
    }

    private void ScoreGoal(GoalEnd end)
    {
        MatchTeam scorers = _teams[0].Attacks == end ? _teams[0] : _teams[1];
        scorers.Goals++;
        World.Events.Add(new MatchEvent(MatchEventType.GoalScored, World.Tick, World.Ball.LastToucherId, World.Ball.Position, scorers.Index));
        CurrentRestart = new Restart(SetPieceKind.KickOff, 1 - scorers.Index, Vector2.Zero, Vector2.Zero);
        foreach (HumanControl human in _humans)
        {
            human.Controller.Reset();
        }

        Enter(MatchPhase.GoalCelebration);
    }

    private void EndHalf()
    {
        foreach (HumanControl human in _humans)
        {
            human.Controller.Reset();
        }

        if (Half == 1)
        {
            World.Events.Add(new MatchEvent(MatchEventType.HalfTime, World.Tick, Player.NoPlayer, World.Ball.Position));
            Enter(MatchPhase.HalfTime);
        }
        else
        {
            World.Events.Add(new MatchEvent(MatchEventType.FullTime, World.Tick, Player.NoPlayer, World.Ball.Position));
            Enter(MatchPhase.FullTime);
        }
    }

    private void StartSecondHalf()
    {
        Half = 2;
        ClockSeconds = 0f;
        if (Ruleset.SwitchEnds)
        {
            GoalEnd first = _teams[0].Attacks;
            _teams[0].SetAttackedGoal(_teams[1].Attacks);
            _teams[1].SetAttackedGoal(first);
        }

        World.Events.Add(new MatchEvent(MatchEventType.HalfStarted, World.Tick, Player.NoPlayer, Vector3.Zero, 2));
        StartKickOff(1 - _firstKickOffTeam, snap: true);
    }

    private void StopEveryone()
    {
        foreach (Player player in World.Players)
        {
            player.Intent = new PlayerIntent { Move = Direction8.None, Face = Direction8.None };
        }
    }

    private Vector2 ClampToPitch(Vector2 target) => new(
        Math.Clamp(target.X, -World.Pitch.HalfWidth + 0.5f, World.Pitch.HalfWidth - 0.5f),
        Math.Clamp(target.Y, -World.Pitch.HalfLength + 0.5f, World.Pitch.HalfLength - 0.5f));

    private static ControlInput Input(ReadOnlySpan<ControlInput> inputs, int controlSet) =>
        controlSet < inputs.Length ? inputs[controlSet] : ControlInput.Neutral;
}
