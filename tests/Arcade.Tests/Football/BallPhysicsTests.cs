using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Simulation;

namespace Arcade.Tests.Football;

public sealed class BallPhysicsTests
{
    private static readonly FootballContent Content = TestPaths.BaseContent;
    private static readonly BallPhysics Physics = new(Content.Physics.Ball, Content.Physics.Gravity);
    private const float Dt = 1f / 60f;

    private static (float Distance, float Seconds) RollToStop(string surface, float speed)
    {
        var state = new BallState { Velocity = new Vector3(speed, 0f, 0f) };
        SurfaceDefinition definition = Content.Surfaces[surface];
        int ticks = 0;
        while (state.Velocity != Vector3.Zero && ticks < 6000)
        {
            Physics.Integrate(ref state, definition, Vector2.Zero, Vector3.Zero, Dt, null);
            ticks++;
        }

        return (state.Position.X, ticks * Dt);
    }

    [Fact]
    public void A_rolling_ball_slows_down_and_stops()
    {
        (float distance, float seconds) = RollToStop("normal", 15f);
        Assert.InRange(distance, 15f, 60f);
        Assert.InRange(seconds, 2f, 10f);
    }

    [Fact]
    public void Wet_and_icy_pitches_let_the_ball_travel_further_and_mud_much_less()
    {
        float normal = RollToStop("normal", 15f).Distance;
        Assert.True(RollToStop("wet", 15f).Distance > normal * 1.1f);
        Assert.True(RollToStop("icy", 15f).Distance > RollToStop("wet", 15f).Distance);
        Assert.True(RollToStop("soggy", 15f).Distance < normal * 0.8f);
        Assert.True(RollToStop("muddy", 15f).Distance < normal * 0.5f);
    }

    [Fact]
    public void A_dropped_ball_bounces_lower_each_time_then_rolls()
    {
        var state = new BallState { Position = new Vector3(0f, 0f, 5f) };
        var peaks = new List<float>();
        float previousVz = 0f;
        for (int i = 0; i < 600; i++)
        {
            Physics.Integrate(ref state, Content.Surfaces["normal"], Vector2.Zero, Vector3.Zero, Dt, null);
            if (previousVz > 0f && state.Velocity.Z <= 0f)
            {
                peaks.Add(state.Position.Z);
            }

            previousVz = state.Velocity.Z;
        }

        Assert.True(peaks.Count >= 2, $"expected several bounces, got {peaks.Count}");
        for (int i = 1; i < peaks.Count; i++)
        {
            Assert.True(peaks[i] < peaks[i - 1]);
        }

        Assert.True(state.IsOnGround);
        Assert.Equal(0f, state.Position.Z);
    }

    [Fact]
    public void Artificial_turf_bounces_higher_than_a_soggy_pitch()
    {
        float FirstBounceHeight(string surface)
        {
            var state = new BallState { Position = new Vector3(0f, 0f, 3f) };
            bool bounced = false;
            float peak = 0f;
            for (int i = 0; i < 400; i++)
            {
                BallImpacts impacts = Physics.Integrate(ref state, Content.Surfaces[surface], Vector2.Zero, Vector3.Zero, Dt, null);
                bounced |= (impacts & BallImpacts.Bounce) != 0;
                if (bounced)
                {
                    peak = MathF.Max(peak, state.Position.Z);
                }
            }

            return peak;
        }

        Assert.True(FirstBounceHeight("artificial") > FirstBounceHeight("normal"));
        Assert.True(FirstBounceHeight("normal") > FirstBounceHeight("soggy"));
    }

    [Fact]
    public void Flight_time_follows_gravity()
    {
        var state = new BallState { Velocity = new Vector3(10f, 0f, 9.81f) };
        int ticks = 0;
        while (ticks == 0 || state.Position.Z > 0f)
        {
            Physics.Integrate(ref state, Content.Surfaces["normal"], Vector2.Zero, Vector3.Zero, Dt, null);
            ticks++;
        }

        Assert.InRange(ticks * Dt, 1.85f, 2.05f);
    }

    [Fact]
    public void Wind_pushes_an_airborne_ball_sideways()
    {
        var calm = new BallState { Velocity = new Vector3(0f, -20f, 6f) };
        var windy = calm;
        for (int i = 0; i < 60; i++)
        {
            Physics.Integrate(ref calm, Content.Surfaces["normal"], Vector2.Zero, Vector3.Zero, Dt, null);
            Physics.Integrate(ref windy, Content.Surfaces["normal"], new Vector2(10f, 0f), Vector3.Zero, Dt, null);
        }

        Assert.Equal(0f, calm.Position.X, 3);
        Assert.True(windy.Position.X > 0.3f);
    }

    [Fact]
    public void Bumpy_pitch_deviates_bounces_reproducibly_with_the_same_seed()
    {
        Vector3 Simulate(string surface, ulong seed)
        {
            var random = new GameRandom(seed);
            var state = new BallState { Position = new Vector3(0f, 0f, 2f), Velocity = new Vector3(0f, -12f, 2f) };
            for (int i = 0; i < 180; i++)
            {
                Physics.Integrate(ref state, Content.Surfaces[surface], Vector2.Zero, Vector3.Zero, Dt, random);
            }

            return state.Position;
        }

        Vector3 flat = Simulate("normal", 7);
        Vector3 bumpy = Simulate("bumpy", 7);
        Assert.Equal(0f, flat.X, 4);
        Assert.NotEqual(0f, bumpy.X);
        Assert.Equal(bumpy, Simulate("bumpy", 7));
    }

    [Fact]
    public void Sub_steps_keep_each_move_shorter_than_the_ball_radius()
    {
        float radius = Content.Physics.Ball.Radius;
        int substeps = Physics.SubstepCount(Content.Physics.Ball.MaxSpeed + 12f, Dt);
        float perSubstep = (Content.Physics.Ball.MaxSpeed + 12f) * Dt / substeps;
        Assert.True(perSubstep < radius, $"{perSubstep} m per sub-step");
    }
}
