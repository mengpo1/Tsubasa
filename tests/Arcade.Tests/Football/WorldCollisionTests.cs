using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football;

public sealed class WorldCollisionTests
{
    private static void Launch(Scenario scenario, Vector2 from, Vector3 velocity)
    {
        scenario.World.Ball.PlaceAt(from);
        scenario.World.Kick(scenario.World.Players[0], new Vector2(velocity.X, velocity.Y), new Vector2(velocity.X, velocity.Y).Length(), velocity.Z, KickKind.Shot, 0f);
    }

    private static Scenario EmptyPitch()
    {
        var scenario = new Scenario();
        scenario.Player.PlaceAt(new Vector2(-25f, 40f), Direction8.Up); // far from the action
        return scenario;
    }

    [Fact]
    public void A_very_fast_ball_rebounds_off_the_post_instead_of_going_through()
    {
        Scenario scenario = EmptyPitch();
        PitchGeometry pitch = scenario.World.Pitch;
        float postX = pitch.GoalHalfWidth + pitch.PostRadius;
        Launch(scenario, new Vector2(postX, -pitch.HalfLength + 12f), new Vector3(0f, -40f, 0f));

        scenario.Run(0.6f, Scenario.Stick(Direction8.None));

        Assert.Contains(scenario.Events, e => e.Type == MatchEventType.PostHit);
        Assert.True(scenario.World.Ball.Velocity.Y >= 0f, "ball should be moving back into the pitch");
        Assert.Null(scenario.World.BallInGoal);
    }

    [Fact]
    public void A_shot_between_the_posts_is_a_goal_and_stays_in_the_net()
    {
        Scenario scenario = EmptyPitch();
        PitchGeometry pitch = scenario.World.Pitch;
        Launch(scenario, new Vector2(1f, -pitch.HalfLength + 16f), new Vector3(0f, -32f, 2f));

        scenario.Run(2f, Scenario.Stick(Direction8.None));

        Assert.Contains(scenario.Events, e => e.Type == MatchEventType.Goal && e.Detail == (int)GoalEnd.Top);
        Assert.Equal(GoalEnd.Top, scenario.World.BallInGoal);
        float depth = -pitch.HalfLength - scenario.World.Ball.Position.Y;
        Assert.InRange(depth, 0f, pitch.GoalDepth);
    }

    [Fact]
    public void A_shot_over_the_bar_is_not_a_goal()
    {
        Scenario scenario = EmptyPitch();
        PitchGeometry pitch = scenario.World.Pitch;
        Launch(scenario, new Vector2(0f, -pitch.HalfLength + 10f), new Vector3(0f, -25f, 9f));

        scenario.Run(2f, Scenario.Stick(Direction8.None));

        Assert.DoesNotContain(scenario.Events, e => e.Type == MatchEventType.Goal);
    }

    [Fact]
    public void A_shot_wide_of_the_post_hits_the_side_netting_from_outside()
    {
        Scenario scenario = EmptyPitch();
        PitchGeometry pitch = scenario.World.Pitch;
        Launch(scenario, new Vector2(pitch.GoalHalfWidth + 1.5f, -pitch.HalfLength + 3f), new Vector3(-6f, -12f, 0f));

        scenario.Run(1.5f, Scenario.Stick(Direction8.None));

        Assert.DoesNotContain(scenario.Events, e => e.Type == MatchEventType.Goal);
        Assert.True(MathF.Abs(scenario.World.Ball.Position.X) >= pitch.GoalHalfWidth - 0.01f || scenario.World.Ball.Position.Y > -pitch.HalfLength);
    }

    [Fact]
    public void The_ball_stays_inside_the_boards()
    {
        Scenario scenario = EmptyPitch();
        PitchGeometry pitch = scenario.World.Pitch;
        Launch(scenario, new Vector2(0f, 0f), new Vector3(40f, 0f, 0f));

        scenario.Run(3f, Scenario.Stick(Direction8.None));

        Assert.Contains(scenario.Events, e => e.Type == MatchEventType.BoardHit);
        Assert.True(MathF.Abs(scenario.World.Ball.Position.X) <= pitch.BoardsHalfWidth);
    }

    [Fact]
    public void A_fast_ball_does_not_pass_through_a_standing_player()
    {
        var scenario = new Scenario();
        scenario.Player.PlaceAt(new Vector2(0f, 0f), Direction8.Down);
        var kicker = scenario.World.AddPlayer(1, scenario.Content.Teams["blues"].Players[9]);
        kicker.PlaceAt(new Vector2(20f, 20f), Direction8.Up);
        scenario.World.Ball.PlaceAt(new Vector2(0f, -15f));
        scenario.World.Kick(kicker, new Vector2(0f, 1f), 38f, 0f, KickKind.Shot, 0f);

        scenario.Run(0.6f, Scenario.Stick(Direction8.None));

        Assert.Contains(scenario.Touches(BallTouchKind.Deflection), e => e.PlayerId == scenario.Player.Id);
        Assert.True(scenario.World.Ball.Position.Y < 0f, "the ball must rebound before the player");
    }
}
