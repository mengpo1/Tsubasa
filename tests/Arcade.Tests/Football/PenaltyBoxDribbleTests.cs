using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;

namespace Arcade.Tests.Football;

/// <summary>Penalty Box Dribble, as documented by the published disassembly (see REFERENCE_ANALYSIS.md).</summary>
public sealed class PenaltyBoxDribbleTests
{
    private const float RunSpeed = 10f;

    private static (FootballWorld World, Player Player) Setup(FootballContent? content = null)
    {
        var scenario = new Scenario(content: content);
        scenario.Player.DefendsEnd = GoalEnd.Bottom; // attacks the top goal
        return (scenario.World, scenario.Player);
    }

    private static void BallAt(FootballWorld world, float distanceFromTopGoalLine) =>
        world.Ball.PlaceAt(new Vector2(5f, -world.Pitch.HalfLength + distanceFromTopGoalLine));

    [Fact]
    public void The_dribble_formula_is_run_factor_times_speed_plus_base_outside_the_box()
    {
        (FootballWorld world, Player player) = Setup();
        BallAt(world, 40f);
        DribbleConfig dribble = world.Controls.Dribble;
        Assert.Equal(dribble.BaseSpeed + (dribble.RunFactor * RunSpeed), world.DribbleSpeed(player, RunSpeed), 4);
        Assert.Equal(1.125f, dribble.RunFactor);
    }

    [Fact]
    public void Computer_players_dribble_shorter_in_the_band_before_the_goal_they_attack()
    {
        (FootballWorld world, Player player) = Setup();
        player.ControlledByHuman = false;
        BallAt(world, 40f);
        float outside = world.DribbleSpeed(player, RunSpeed);
        BallAt(world, 10f);
        float inside = world.DribbleSpeed(player, RunSpeed);
        Assert.Equal(world.Controls.Dribble.PenaltyBoxDribble.ComputerReduction, outside - inside, 4);
        Assert.True(inside < world.Controls.Dribble.RunFactor * RunSpeed, "v1.4: the CPU push drops below 1.125 x speed");

        // Near the goal the player defends, nothing changes.
        world.Ball.PlaceAt(new Vector2(5f, world.Pitch.HalfLength - 10f));
        Assert.Equal(outside, world.DribbleSpeed(player, RunSpeed), 4);
    }

    [Fact]
    public void Human_players_are_not_affected_by_default_as_in_version_1_4()
    {
        (FootballWorld world, Player player) = Setup();
        Assert.True(player.ControlledByHuman);
        BallAt(world, 40f);
        float outside = world.DribbleSpeed(player, RunSpeed);
        BallAt(world, 10f);
        Assert.Equal(outside, world.DribbleSpeed(player, RunSpeed), 4);
    }

    [Fact]
    public void The_world_cup_90_behaviour_is_a_data_setting()
    {
        FootballContent baseContent = TestPaths.BaseContent;
        DribbleConfig dribble = baseContent.Controls.Dribble;
        FootballContent worldCup90 = baseContent.With(controls: baseContent.Controls with
        {
            Dribble = dribble with { PenaltyBoxDribble = dribble.PenaltyBoxDribble with { HumanReduction = dribble.BaseSpeed } },
        });
        (FootballWorld world, Player player) = Setup(worldCup90);
        BallAt(world, 10f);
        Assert.Equal(dribble.RunFactor * RunSpeed, world.DribbleSpeed(player, RunSpeed), 4);
    }
}
