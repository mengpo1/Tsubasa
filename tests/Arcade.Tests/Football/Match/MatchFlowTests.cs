using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football.Match;

public sealed class MatchFlowTests
{
    [Fact]
    public void Kick_off_positions_respect_the_halves_and_the_centre_circle()
    {
        var harness = new MatchHarness();
        FootballMatch match = harness.Match;
        int kicking = match.CurrentRestart!.Value.TeamIndex;
        float circle = harness.World.Pitch.Definition.CentreCircleRadius;
        foreach (MatchTeam team in match.Teams)
        {
            foreach (Player player in team.Players)
            {
                float ownHalf = Vector2.Dot(player.Position, team.AttackDirection);
                Assert.True(ownHalf <= 0.01f, $"{player.Profile.Name} of team {team.Index} must be in his own half");
                if (team.Index != kicking)
                {
                    Assert.True(player.Position.Length() >= circle, $"{player.Profile.Name} must stay outside the centre circle");
                }
            }
        }

        Assert.Equal(BallStatus.Dead, harness.World.Ball.Status);
        Assert.Equal(Vector2.Zero, new Vector2(harness.World.Ball.Position.X, harness.World.Ball.Position.Y));
    }

    [Fact]
    public void A_computer_match_is_played_to_full_time_with_half_time_and_ends_switched()
    {
        var harness = new MatchHarness(halfSeconds: 25f);
        FootballMatch match = harness.Match;
        GoalEnd firstHalfAttack = match.Teams[0].Attacks;
        int firstKickOff = match.CurrentRestart!.Value.TeamIndex;
        Assert.True(harness.RunUntil(() => match.Phase == MatchPhase.HalfTime, 60f));
        Assert.True(harness.RunUntil(() => match.Half == 2, 10f));
        Assert.NotEqual(firstHalfAttack, match.Teams[0].Attacks);
        Assert.Equal(1 - firstKickOff, match.CurrentRestart!.Value.TeamIndex);
        Assert.True(harness.RunUntil(() => match.IsOver, 60f));

        Assert.Single(harness.Of(MatchEventType.HalfTime));
        Assert.Single(harness.Of(MatchEventType.FullTime));
        Assert.Equal(90f, match.DisplayedMinutes, 1);
        Assert.Contains(harness.Events, e => e.Type == MatchEventType.BallKicked);
        Assert.Contains(harness.Events, e => e.Type == MatchEventType.PossessionChanged);
        Assert.Equal(match.Teams[0].Goals + match.Teams[1].Goals, harness.Of(MatchEventType.GoalScored).Count());
        Vector3 ball = harness.World.Ball.Position;
        Assert.True(float.IsFinite(ball.X) && float.IsFinite(ball.Y) && float.IsFinite(ball.Z));
    }

    [Fact]
    public void Matches_are_reproducible_from_their_seed()
    {
        (int, int, long, Vector3) Play(ulong seed)
        {
            var harness = new MatchHarness(halfSeconds: 15f, seed: seed);
            harness.RunUntil(() => harness.Match.IsOver, 60f);
            return (harness.Match.Teams[0].Goals, harness.Match.Teams[1].Goals, harness.World.Tick, harness.World.Ball.Position);
        }

        Assert.Equal(Play(11), Play(11));
    }

    [Fact]
    public void A_goal_is_awarded_to_the_attacking_team_and_the_other_team_kicks_off()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        MatchTeam attackers = harness.Match.Teams[0];
        ScoreFor(harness, attackers);

        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.GoalCelebration, 3f));
        Assert.Equal(1, attackers.Goals);
        MatchEvent goalEvent = Assert.Single(harness.Of(MatchEventType.GoalScored));
        Assert.Equal(0, goalEvent.Detail);
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.RestartReady, 10f));
        Assert.Equal(SetPieceKind.KickOff, harness.Match.CurrentRestart!.Value.Kind);
        Assert.Equal(1, harness.Match.CurrentRestart!.Value.TeamIndex);
    }

    [Fact]
    public void A_ball_kicked_over_the_touchline_leads_to_a_throw_in_taken_from_the_hands()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        float edge = harness.World.Pitch.HalfWidth;
        harness.KickFrom(0, new Vector2(edge - 1f, 5f), new Vector2(15f, 0f));

        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.BallOut, 2f));
        Restart restart = harness.Match.CurrentRestart!.Value;
        Assert.Equal(SetPieceKind.ThrowIn, restart.Kind);
        Assert.Equal(1, restart.TeamIndex);
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.RestartReady, 8f));
        Assert.Equal(BallStatus.Carried, harness.World.Ball.Status);
        Assert.Equal(1, harness.Match.Taker!.TeamIndex);
        Assert.True(harness.World.Ball.Position.Z > 1.5f, "the ball is held above the head");
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.Playing, 5f));
        Assert.Contains(harness.Events, e => e.Type == MatchEventType.RestartTaken && e.Detail == (int)SetPieceKind.ThrowIn);
    }

    [Fact]
    public void The_clock_stops_while_the_teams_set_up_after_a_goal()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        ScoreFor(harness, harness.Match.Teams[0]);
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.GoalCelebration, 3f));
        float clock = harness.Match.ClockSeconds;
        harness.Run(2f);
        Assert.Equal(clock, harness.Match.ClockSeconds);
    }

    [Fact]
    public void A_human_takes_his_team_restart_by_aiming_and_releasing_fire()
    {
        var harness = new MatchHarness(humans: 1);
        HumanControl human = harness.Match.Humans[0];
        ReachHumanRestart(harness);
        Assert.Equal(ControlPhase.SetPiece, human.Controller.Phase);
        Assert.Same(harness.Match.Taker, human.Player);
        Vector2 inward = harness.Match.CurrentRestart!.Value.Inward;
        Direction8 aim = inward == Vector2.Zero ? Direction8.Left : Direction8Extensions.FromVector(inward);
        harness.Inputs[0] = new ControlInput(aim, true);
        harness.Run(0.4f);
        Assert.NotNull(human.Controller.SetPieceCharge);
        harness.Inputs[0] = new ControlInput(aim, false);
        harness.Step();
        harness.Inputs[0] = ControlInput.Neutral;
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.Playing, 1f));
        Assert.Contains(harness.Events, e => e.Type == MatchEventType.RestartTaken && harness.World.Players[e.PlayerId].TeamIndex == 0);
    }

    [Fact]
    public void A_human_who_does_not_react_lets_the_computer_take_the_restart()
    {
        var harness = new MatchHarness(humans: 1);
        ReachHumanRestart(harness);
        float auto = harness.Match.Ruleset.HumanRestartAutoSeconds;
        harness.Run(auto - 0.5f);
        Assert.Equal(MatchPhase.RestartReady, harness.Match.Phase);
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.Playing, 1.5f));
    }

    /// <summary>Shoots into the goal attacked by <paramref name="attackers"/>, with the opposing keeper out of the way.</summary>
    private static void ScoreFor(MatchHarness harness, MatchTeam attackers)
    {
        Player keeper = harness.Match.Teams[1 - attackers.Index].Goalkeeper;
        keeper.PlaceAt(new Vector2(-30f, 0f), Direction8.None);
        Vector2 goal = harness.World.Pitch.GoalCentre(attackers.Attacks);
        harness.KickFrom(attackers.Index, goal - (attackers.AttackDirection * 6f), attackers.AttackDirection * 30f);
    }

    /// <summary>Gets to a restart of the human team (team 0): its kick-off, or a throw-in after team 1 kicks the ball out.</summary>
    private static void ReachHumanRestart(MatchHarness harness)
    {
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.RestartReady, 10f));
        if (harness.Match.CurrentRestart!.Value.TeamIndex == 0)
        {
            return;
        }

        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.Playing, 10f));
        float edge = harness.World.Pitch.HalfWidth;
        harness.KickFrom(1, new Vector2(edge - 1f, 0f), new Vector2(15f, 0f));
        Assert.True(harness.RunUntil(() => harness.Match.Phase == MatchPhase.RestartReady, 10f));
        Assert.Equal(0, harness.Match.CurrentRestart!.Value.TeamIndex);
    }
}
