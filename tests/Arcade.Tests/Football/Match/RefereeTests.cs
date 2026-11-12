using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Pitch;

namespace Arcade.Tests.Football.Match;

public sealed class RefereeTests
{
    private static readonly PitchGeometry Pitch = new(TestPaths.BaseContent.Pitches["normal"]);
    private const float Radius = 0.11f;

    /// <summary>Team 1 defends the top goal, team 0 the bottom one.</summary>
    private static int Defender(GoalEnd end) => end == GoalEnd.Top ? 1 : 0;

    [Fact]
    public void Ball_in_play_and_ball_on_the_line_are_not_out()
    {
        Assert.Null(Referee.CheckOutOfPlay(new Vector3(10f, 10f, 0f), Radius, Pitch, 0, Defender));
        Assert.Null(Referee.CheckOutOfPlay(new Vector3(Pitch.HalfWidth + 0.05f, 0f, 0f), Radius, Pitch, 0, Defender));
    }

    [Fact]
    public void Crossing_a_touchline_gives_a_throw_in_to_the_other_team_where_it_crossed()
    {
        Restart? restart = Referee.CheckOutOfPlay(new Vector3(Pitch.HalfWidth + 0.5f, 12f, 0f), Radius, Pitch, 0, Defender);
        Assert.NotNull(restart);
        Assert.Equal(SetPieceKind.ThrowIn, restart.Value.Kind);
        Assert.Equal(1, restart.Value.TeamIndex);
        Assert.Equal(new Vector2(Pitch.HalfWidth, 12f), restart.Value.Spot);
        Assert.Equal(new Vector2(-1f, 0f), restart.Value.Inward);
    }

    [Fact]
    public void An_attacker_putting_the_ball_over_the_goal_line_gives_a_goal_kick()
    {
        Restart? restart = Referee.CheckOutOfPlay(new Vector3(12f, -Pitch.HalfLength - 0.5f, 0f), Radius, Pitch, 0, Defender);
        Assert.NotNull(restart);
        Assert.Equal(SetPieceKind.GoalKick, restart.Value.Kind);
        Assert.Equal(1, restart.Value.TeamIndex);
        Assert.True(Pitch.IsInPenaltyArea(restart.Value.Spot, GoalEnd.Top));
        Assert.True(restart.Value.Inward.Y > 0f, "the goal kick points away from the goal");
    }

    [Fact]
    public void A_defender_putting_the_ball_over_his_own_goal_line_gives_a_corner_on_that_side()
    {
        Restart? restart = Referee.CheckOutOfPlay(new Vector3(-12f, -Pitch.HalfLength - 0.5f, 2f), Radius, Pitch, 1, Defender);
        Assert.NotNull(restart);
        Assert.Equal(SetPieceKind.Corner, restart.Value.Kind);
        Assert.Equal(0, restart.Value.TeamIndex);
        Assert.True(restart.Value.Spot.X < -Pitch.HalfWidth + 1f && restart.Value.Spot.Y < -Pitch.HalfLength + 1f);
    }

    [Fact]
    public void A_ball_inside_the_goal_is_a_goal_not_an_out()
    {
        Assert.Null(Referee.CheckOutOfPlay(new Vector3(0.5f, -Pitch.HalfLength - 1f, 0.2f), Radius, Pitch, 0, Defender));
    }
}
