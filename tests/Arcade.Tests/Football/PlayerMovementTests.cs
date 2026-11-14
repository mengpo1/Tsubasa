using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football;

public sealed class PlayerMovementTests
{
    private static FootballContent NoFatigue() => TestPaths.BaseContent.With(
        playerRules: TestPaths.BaseContent.PlayerRules with { Fatigue = TestPaths.BaseContent.PlayerRules.Fatigue with { Enabled = false } });

    private static Scenario FarFromBall(FootballContent? content = null, string pitch = "normal", int playerIndex = 9)
    {
        var scenario = new Scenario(pitch, content ?? NoFatigue(), playerIndex: playerIndex);
        scenario.World.Ball.PlaceAt(new Vector2(30f, -40f));
        scenario.Player.PlaceAt(new Vector2(0f, 0f), Direction8.Up);
        return scenario;
    }

    private static float TopSpeed(Scenario s) => PlayerMovement.TopSpeed(s.Player, s.Content.Physics.Movement, s.Content.PlayerRules);

    [Fact]
    public void A_player_reaches_top_speed_quickly_and_never_exceeds_it()
    {
        Scenario scenario = FarFromBall();
        float top = TopSpeed(scenario);
        int ticks = scenario.RunUntil(() => scenario.Player.Speed >= top - 0.01f, Scenario.Stick(Direction8.Up), 2f);
        Assert.InRange(scenario.Seconds(ticks), 0.1f, 0.45f);
        scenario.Run(1f, Scenario.Stick(Direction8.Up));
        Assert.True(scenario.Player.Speed <= top + 1e-4f);
    }

    [Fact]
    public void Facing_changes_instantly_but_velocity_keeps_momentum_through_a_turn()
    {
        Scenario scenario = FarFromBall();
        scenario.Run(1f, Scenario.Stick(Direction8.Up));
        scenario.Step(Scenario.Stick(Direction8.Right));
        Assert.Equal(Direction8.Right, scenario.Player.Facing);
        Assert.True(scenario.Player.Velocity.Y < -4f, "still moving up after one step");
        scenario.Run(0.5f, Scenario.Stick(Direction8.Right));
        Assert.True(MathF.Abs(scenario.Player.Velocity.Y) < 0.01f);
        Assert.True(scenario.Player.Velocity.X > 7f);
    }

    [Fact]
    public void Braking_stops_a_sprinting_player_within_a_short_distance()
    {
        Scenario scenario = FarFromBall();
        scenario.Run(1f, Scenario.Stick(Direction8.Up));
        float start = scenario.Player.Position.Y;
        int ticks = scenario.RunUntil(() => scenario.Player.Speed == 0f, Scenario.Stick(Direction8.None), 2f);
        float distance = start - scenario.Player.Position.Y;
        Assert.InRange(scenario.Seconds(ticks), 0.1f, 0.35f);
        Assert.InRange(distance, 0.3f, 1.5f);
    }

    [Fact]
    public void Reversing_takes_longer_than_starting_from_rest()
    {
        Scenario fromRest = FarFromBall();
        int start = fromRest.RunUntil(() => fromRest.Player.Velocity.Y >= 5f, Scenario.Stick(Direction8.Down), 2f);
        Scenario reversing = FarFromBall();
        reversing.Run(1f, Scenario.Stick(Direction8.Up));
        int reverse = reversing.RunUntil(() => reversing.Player.Velocity.Y >= 5f, Scenario.Stick(Direction8.Down), 2f);
        Assert.True(reverse > start);
    }

    [Fact]
    public void Pace_rating_changes_top_speed()
    {
        var content = NoFatigue();
        var team = content.Teams["reds"];
        int fastest = Enumerable.Range(0, team.Players.Count).MaxBy(i => team.Players[i].Attributes.Pace);
        int slowest = Enumerable.Range(0, team.Players.Count).MinBy(i => team.Players[i].Attributes.Pace);
        Assert.True(TopSpeed(FarFromBall(content, playerIndex: fastest)) > TopSpeed(FarFromBall(content, playerIndex: slowest)));
    }

    [Fact]
    public void Fatigue_lowers_top_speed_after_a_long_sprint_unless_disabled()
    {
        var team = TestPaths.BaseContent.Teams["reds"];
        int leastStamina = Enumerable.Range(0, team.Players.Count).MinBy(i => team.Players[i].Attributes.Stamina);
        Scenario tired = FarFromBall(TestPaths.BaseContent, playerIndex: leastStamina);
        float fresh = TopSpeed(tired);
        for (int lap = 0; lap < 8; lap++)
        {
            tired.Run(4f, Scenario.Stick(Direction8.Up));
            tired.Run(4f, Scenario.Stick(Direction8.Down));
        }

        Assert.True(tired.Player.Energy < 0.92f, $"energy {tired.Player.Energy}");
        Assert.True(TopSpeed(tired) < fresh);

        Scenario untiring = FarFromBall();
        untiring.Run(10f, Scenario.Stick(Direction8.Right));
        Assert.Equal(1f, untiring.Player.Energy);
    }

    [Fact]
    public void Ice_reduces_grip_so_stopping_takes_longer()
    {
        float StopDistance(string pitch)
        {
            Scenario scenario = FarFromBall(pitch: pitch);
            scenario.Run(1f, Scenario.Stick(Direction8.Up));
            float start = scenario.Player.Position.Y;
            scenario.Run(1f, Scenario.Stick(Direction8.None));
            return start - scenario.Player.Position.Y;
        }

        Assert.True(StopDistance("icy") > StopDistance("normal") * 1.4f);
    }
}
