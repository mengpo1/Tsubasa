using System.Numerics;
using Arcade.Core.Input;
using Arcade.Core.Simulation;

namespace Arcade.Tests.Common;

public sealed class SimulationTests
{
    [Fact]
    public void Fixed_step_clock_produces_one_step_per_period_whatever_the_frame_rate()
    {
        var at30 = new FixedStepClock(60);
        var at144 = new FixedStepClock(60);
        int steps30 = 0;
        int steps144 = 0;
        for (int i = 0; i < 60; i++)
        {
            steps30 += at30.Advance(1.0 / 30.0);
        }

        for (int i = 0; i < 288; i++)
        {
            steps144 += at144.Advance(1.0 / 144.0);
        }

        Assert.Equal(120, steps30);
        Assert.InRange(steps144, 119, 120);
    }

    [Fact]
    public void Fixed_step_clock_caps_long_frames_and_stops_when_paused()
    {
        var clock = new FixedStepClock(60, maxFrameSeconds: 0.25);
        Assert.Equal(15, clock.Advance(5.0));
        clock.Paused = true;
        Assert.Equal(0, clock.Advance(1.0));
    }

    [Fact]
    public void Fixed_step_clock_alpha_is_the_fraction_of_the_pending_step()
    {
        var clock = new FixedStepClock(60);
        clock.Advance(1.5 / 60.0);
        Assert.InRange(clock.Alpha, 0.49f, 0.51f);
    }

    [Fact]
    public void Random_generator_is_reproducible_from_its_seed()
    {
        var a = new GameRandom(1234);
        var b = new GameRandom(1234);
        var c = new GameRandom(1235);
        float[] first = [.. Enumerable.Range(0, 20).Select(_ => a.NextFloat())];
        float[] second = [.. Enumerable.Range(0, 20).Select(_ => b.NextFloat())];
        float[] other = [.. Enumerable.Range(0, 20).Select(_ => c.NextFloat())];
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.All(first, value => Assert.InRange(value, 0f, 0.99999994f));
    }

    [Theory]
    [InlineData(0f, -1f, Direction8.Up)]
    [InlineData(1f, -1f, Direction8.UpRight)]
    [InlineData(1f, 0.1f, Direction8.Right)]
    [InlineData(0.9f, 1f, Direction8.DownRight)]
    [InlineData(-0.05f, 1f, Direction8.Down)]
    [InlineData(-1f, 0.9f, Direction8.DownLeft)]
    [InlineData(-1f, 0f, Direction8.Left)]
    [InlineData(-0.8f, -0.9f, Direction8.UpLeft)]
    [InlineData(0.1f, 0.1f, Direction8.None)]
    public void Analogue_input_is_quantised_to_eight_directions(float x, float y, Direction8 expected)
    {
        Assert.Equal(expected, Direction8Extensions.FromVector(new Vector2(x, y)));
    }

    [Fact]
    public void Opposite_left_right_switches_cancel_and_directions_have_opposites()
    {
        Assert.Equal(Direction8.Up, Direction8Extensions.FromSwitches(up: true, down: false, left: true, right: true));
        Assert.Equal(Direction8.None, Direction8Extensions.FromSwitches(up: true, down: true, left: false, right: false));
        Assert.Equal(Direction8.DownLeft, Direction8.UpRight.Opposite());
        Assert.Equal(Direction8.Up, Direction8.Down.Opposite());
    }
}
