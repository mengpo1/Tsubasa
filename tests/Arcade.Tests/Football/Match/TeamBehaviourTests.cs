using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.AI;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football.Match;

/// <summary>Formations, AI, keeper, selection of the human player and player collisions.</summary>
public sealed class TeamBehaviourTests
{
    [Fact]
    public void Formation_targets_are_mirrored_between_teams_and_follow_the_ball()
    {
        var harness = new MatchHarness();
        PitchGeometry pitch = harness.World.Pitch;
        MatchTeam home = harness.Match.Teams[0];
        MatchTeam away = harness.Match.Teams[1];
        Vector2 homeLeftBack = home.FormationTarget(1, Vector2.Zero, false, pitch);
        Assert.True(Vector2.Dot(homeLeftBack, home.AttackDirection) < 0f, "defenders stand in their own half");
        Assert.Equal(home.ToWorld(0.5f, 0.5f, pitch), -away.ToWorld(0.5f, 0.5f, pitch));

        Vector2 ballForward = home.AttackDirection * 30f;
        Vector2 shifted = home.FormationTarget(6, ballForward, false, pitch);
        Assert.True(Vector2.Dot(shifted - home.FormationTarget(6, Vector2.Zero, false, pitch), home.AttackDirection) > 5f, "the block moves up with the ball");
        Assert.True(Vector2.Dot(home.FormationTarget(6, ballForward, true, pitch) - shifted, home.AttackDirection) > 0f, "and further when the team has the ball");
    }

    [Fact]
    public void The_nearest_computer_player_chases_a_loose_ball()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        var spot = new Vector2(20f, 0f);
        harness.World.Ball.PlaceAt(spot);
        harness.Step();
        Player nearest = harness.World.Players.Where(p => !p.IsGoalkeeper).MinBy(p => Vector2.Distance(p.Position, spot))!;
        float before = Vector2.Distance(nearest.Position, spot);
        harness.Run(0.6f);
        Assert.True(Vector2.Distance(nearest.Position, spot) < before - 2f || harness.World.Ball.LastToucherId != Player.NoPlayer);
    }

    [Fact]
    public void A_computer_player_with_the_ball_near_goal_shoots()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        MatchTeam team = harness.Match.Teams[0];
        Player striker = team.Players[9];
        Vector2 goal = harness.World.Pitch.GoalCentre(team.Attacks);
        striker.PlaceAt(goal - (team.AttackDirection * 15f), Direction8Extensions.FromVector(team.AttackDirection));
        harness.World.Ball.PlaceAt(striker.Position + (team.AttackDirection * 0.6f));
        harness.Events.Clear();
        Assert.True(harness.RunUntil(() => harness.Events.Any(e => e.Type == MatchEventType.BallKicked && e.Detail == (int)KickKind.Shot), 3f));
    }

    [Fact]
    public void The_keeper_stands_between_the_ball_and_his_goal()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        MatchTeam defenders = harness.Match.Teams[1];
        Vector2 goal = harness.World.Pitch.GoalCentre(defenders.Defends);
        // Freeze the ball on the right wing of the defending team's half, far from everybody.
        harness.World.PlaceDeadBall(goal + (defenders.AttackDirection * 25f) + new Vector2(20f, 0f));
        harness.Run(2f);
        Player keeper = defenders.Goalkeeper;
        Assert.True(keeper.Position.X > 0.3f, $"keeper should move towards the ball side (x = {keeper.Position.X})");
        float depth = Vector2.Dot(keeper.Position - goal, defenders.AttackDirection);
        Assert.InRange(depth, 0.3f, 5f);
    }

    [Fact]
    public void The_keeper_catches_a_slow_shot_and_clears_it()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        MatchTeam attackers = harness.Match.Teams[0];
        MatchTeam defenders = harness.Match.Teams[1];
        Player keeper = defenders.Goalkeeper;
        Vector2 goal = harness.World.Pitch.GoalCentre(attackers.Attacks);
        keeper.PlaceAt(goal - (attackers.AttackDirection * 1.5f), Direction8.None);
        harness.KickFrom(0, goal - (attackers.AttackDirection * 12f), attackers.AttackDirection * 14f);

        Assert.True(harness.RunUntil(() => harness.World.Ball.Status == BallStatus.Carried, 2f));
        Assert.Equal(keeper.Id, harness.World.Ball.CarrierId);
        Assert.Contains(harness.Events, e => e.Type == MatchEventType.BallTouched && e.Detail == (int)BallTouchKind.Catch);
        Assert.True(harness.RunUntil(() => harness.Events.Any(e => e.Type == MatchEventType.BallKicked && e.Detail == (int)KickKind.KeeperKick), 4f));
        Assert.True(Vector2.Dot(new Vector2(harness.World.Ball.Velocity.X, harness.World.Ball.Velocity.Y), defenders.AttackDirection) > 0f, "cleared upfield");
    }

    [Fact]
    public void A_very_fast_shot_is_parried_rather_than_caught()
    {
        var harness = new MatchHarness();
        harness.StartPlaying();
        MatchTeam attackers = harness.Match.Teams[0];
        Player keeper = harness.Match.Teams[1].Goalkeeper;
        Vector2 goal = harness.World.Pitch.GoalCentre(attackers.Attacks);
        keeper.PlaceAt(goal - (attackers.AttackDirection * 1.5f), Direction8.None);
        harness.KickFrom(0, goal - (attackers.AttackDirection * 10f), attackers.AttackDirection * 32f);
        Assert.True(harness.RunUntil(() => harness.World.Ball.LastToucherId == keeper.Id, 1f));
        Assert.Equal(BallStatus.Live, harness.World.Ball.Status);
        Assert.Equal(BallTouchKind.Deflection, keeper.LastTouchKind);
    }

    [Fact]
    public void The_human_controls_the_teammate_nearest_to_the_ball_but_keeps_a_ball_he_holds()
    {
        var harness = new MatchHarness(humans: 1);
        harness.StartPlaying();
        HumanControl human = harness.Match.Humans[0];
        Player target = harness.Match.Teams[0].Players[1];
        harness.World.Ball.PlaceAt(target.Position + new Vector2(0.8f, 0f));
        harness.Run(0.5f);
        Assert.Same(target, human.Player);
        Assert.False(human.Player.IsGoalkeeper);
    }

    [Fact]
    public void Players_do_not_walk_through_each_other()
    {
        var harness = new MatchHarness(humans: 2);
        harness.StartPlaying();
        Player a = harness.Match.Humans[0].Player;
        Player b = harness.Match.Humans[1].Player;
        harness.World.PlaceDeadBall(new Vector2(30f, 40f));
        a.PlaceAt(new Vector2(0f, 10f), Direction8.Right);
        b.PlaceAt(new Vector2(6f, 10f), Direction8.Left);
        float min = float.MaxValue;
        for (int i = 0; i < harness.Ticks(1.5f); i++)
        {
            harness.Inputs[0] = new ControlInput(Direction8.Right, false);
            harness.Inputs[1] = new ControlInput(Direction8.Left, false);
            harness.Step();
            min = MathF.Min(min, Vector2.Distance(a.Position, b.Position));
        }

        float body = harness.World.Content.Physics.Movement.BodyRadius * 2f;
        Assert.True(min >= body - 0.05f, $"closest distance {min}");
    }

    [Fact]
    public void Nobody_can_stand_inside_a_goal()
    {
        var harness = new MatchHarness(humans: 1);
        harness.StartPlaying();
        Player player = harness.Match.Humans[0].Player;
        harness.World.PlaceDeadBall(new Vector2(30f, 0f));
        GoalEnd end = harness.Match.Teams[0].Attacks;
        player.PlaceAt(harness.World.Pitch.GoalCentre(end) - (harness.Match.Teams[0].AttackDirection * 2f), Direction8.None);
        Direction8 run = Direction8Extensions.FromVector(harness.Match.Teams[0].AttackDirection);
        harness.Inputs[0] = new ControlInput(run, false);
        harness.Run(1.5f);
        float depth = Vector2.Dot(player.Position - harness.World.Pitch.GoalCentre(end), harness.Match.Teams[0].AttackDirection);
        Assert.True(depth <= 0f, $"player went {depth} m into the goal");
    }

    [Fact]
    public void Ai_settings_are_valid_and_levels_differ_in_reaction_not_physics()
    {
        AiConfig ai = TestPaths.BaseContent.Ai;
        Assert.True(ai.Level("easy").ReactionSeconds > ai.Level("hard").ReactionSeconds);
        Assert.True(ai.Level("easy").ShotErrorDegrees > ai.Level("hard").ShotErrorDegrees);
        Assert.Throws<Arcade.Core.Content.ContentException>(() => ai.Level("impossible"));
    }
}
