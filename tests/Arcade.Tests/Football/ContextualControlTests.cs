using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football;

public sealed class ContextualControlTests
{
    private static readonly ControlInput Up = Scenario.Stick(Direction8.Up);
    private static readonly ControlInput UpFire = Scenario.Stick(Direction8.Up, fire: true);

    [Fact]
    public void Running_into_the_ball_pushes_it_ahead_faster_than_the_player()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();

        Assert.True(scenario.World.Ball.Velocity.Y < scenario.Player.Velocity.Y, "ball must move up faster than the player");
        scenario.Run(0.3f, Up);
        Assert.True(scenario.Player.Position.Y - scenario.BallXY.Y > scenario.Content.Controls.Contact.FootRadius, "ball runs ahead");
        Assert.Empty(scenario.Kicks(KickKind.Shot));
    }

    [Fact]
    public void A_faster_run_pushes_the_ball_further()
    {
        float BallTravel(float runUpSeconds)
        {
            var scenario = new Scenario();
            scenario.Player.PlaceAt(new Vector2(0f, 30f), Direction8.Up);
            scenario.Run(runUpSeconds, Up);
            Vector2 at = scenario.Player.Position + new Vector2(0f, -0.7f);
            scenario.World.Ball.PlaceAt(at);
            scenario.RunUntil(() => scenario.Touches(BallTouchKind.Dribble).Any(), Up);
            Vector2 touched = scenario.BallXY;
            scenario.Run(3f, Scenario.Stick(Direction8.Right)); // run away, let the ball roll
            return Vector2.Distance(touched, scenario.BallXY);
        }

        Assert.True(BallTravel(1f) > BallTravel(0.08f) * 1.5f);
    }

    [Fact]
    public void Dribbling_keeps_the_ball_close_but_never_glued()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        float min = float.MaxValue;
        float max = 0f;
        for (int i = 0; i < scenario.Ticks(3f); i++)
        {
            scenario.Step(Up);
            float distance = scenario.World.DistanceToBall(scenario.Player);
            min = MathF.Min(min, distance);
            max = MathF.Max(max, distance);
        }

        Assert.True(scenario.Touches(BallTouchKind.Dribble).Count() >= 2, "several touches while dribbling");
        Assert.True(max > scenario.Content.Controls.Contact.FootRadius + 0.3f, $"ball should run ahead (max distance {max})");
        Assert.True(max < 5f, $"player should catch up (max distance {max})");
    }

    [Fact]
    public void A_sharp_turn_loses_the_ball()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        scenario.Run(1f, Scenario.Stick(Direction8.Right));
        Assert.True(scenario.World.DistanceToBall(scenario.Player) > 4f);
    }

    [Fact]
    public void A_player_running_past_the_side_of_the_ball_misses_it()
    {
        var scenario = new Scenario();
        scenario.Player.PlaceAt(new Vector2(0f, 30f), Direction8.Up);
        scenario.World.Ball.PlaceAt(new Vector2(0.8f, 25f));
        scenario.Run(1.5f, Up);
        Assert.DoesNotContain(scenario.Events, e => e.Type == MatchEventType.BallTouched);
        Assert.True(scenario.Player.Position.Y < 20f, "player overran the ball");
    }

    [Fact]
    public void Fire_pressed_after_a_touch_shoots_in_the_facing_direction()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        scenario.Step(UpFire);

        MatchEvent shot = Assert.Single(scenario.Kicks(KickKind.Shot));
        Assert.True(shot.Value > 20f, $"shot speed {shot.Value}");
        Assert.True(scenario.World.Ball.Velocity.Y < -20f);
        Assert.True(MathF.Abs(scenario.World.Ball.Velocity.X) < 0.01f);
        Assert.True(scenario.World.Ball.Velocity.Z > 0f, "a shot leaves the ground");
    }

    [Fact]
    public void Fire_pressed_before_the_contact_stops_the_ball_without_shooting()
    {
        var scenario = new Scenario();
        scenario.Player.PlaceAt(new Vector2(0f, 30f), Direction8.Up);
        scenario.World.Ball.PlaceAt(new Vector2(0f, 25f));
        scenario.Run(0.2f, Up);
        scenario.RunUntil(() => scenario.Controller.Phase == ControlPhase.Holding, UpFire, 1.5f);

        Assert.Equal(ControlPhase.Holding, scenario.Controller.Phase);
        Assert.Single(scenario.Touches(BallTouchKind.Trap));
        scenario.Run(0.5f, UpFire);
        Assert.Empty(scenario.Kicks(KickKind.Shot));
        Assert.True(scenario.Player.Speed < 0.5f, "the player stops on the ball");
        Assert.True(scenario.World.Ball.Velocity.Length() < 1f, "the ball is under control");
        Assert.True(scenario.World.DistanceToBall(scenario.Player) < 1f);
    }

    [Fact]
    public void Holding_fire_through_a_contact_never_triggers_a_shot()
    {
        // Press fire slightly too early, then keep it held while running through several potential contacts.
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        scenario.Run(0.35f, Up);
        scenario.Run(2f, UpFire);
        Assert.Empty(scenario.Kicks(KickKind.Shot));
    }

    [Fact]
    public void Releasing_fire_while_aiming_passes_and_aiming_longer_passes_harder()
    {
        float PassSpeed(float aimSeconds)
        {
            Scenario scenario = TrappedBall();
            scenario.Run(aimSeconds, Scenario.Stick(Direction8.Right, fire: true));
            scenario.Step(Scenario.Stick(Direction8.Right));
            MatchEvent pass = Assert.Single(scenario.Kicks(KickKind.Pass));
            Assert.True(scenario.World.Ball.Velocity.X > 5f && MathF.Abs(scenario.World.Ball.Velocity.Y) < 0.5f, "pass goes right");
            Assert.Equal(ControlPhase.Free, scenario.Controller.Phase);
            return pass.Value;
        }

        Assert.True(PassSpeed(0.7f) > PassSpeed(0.05f) * 1.5f);
    }

    [Fact]
    public void Releasing_fire_with_the_joystick_centred_keeps_the_ball_then_dribbles_or_shoots()
    {
        Scenario scenario = TrappedBall();
        scenario.Run(0.2f, Scenario.Stick(Direction8.None, fire: true));
        scenario.Run(0.5f, Scenario.Stick(Direction8.None));
        Assert.Equal(ControlPhase.AtFeet, scenario.Controller.Phase);
        Assert.DoesNotContain(scenario.Events, e => e.Type == MatchEventType.BallKicked);

        scenario.Step(Scenario.Stick(Direction8.Left));
        Assert.Single(scenario.Kicks(KickKind.Dribble));
        Assert.True(scenario.World.Ball.Velocity.X < 0f, "ball pushed in the new direction");
        scenario.Step(Scenario.Stick(Direction8.Left, fire: true));
        Assert.Single(scenario.Kicks(KickKind.Shot));
        Assert.True(scenario.World.Ball.Velocity.X < -15f);
    }

    [Fact]
    public void Reversing_the_joystick_with_the_ball_at_the_feet_chips_it_forward()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        scenario.Step(Scenario.Stick(Direction8.Down));

        Assert.Single(scenario.Kicks(KickKind.Chip));
        Assert.True(scenario.World.Ball.Velocity.Z > 4f, "the ball is lifted");
        Assert.True(scenario.World.Ball.Velocity.Y < 0f, "it keeps going in the original direction");
        scenario.Run(0.5f, Scenario.Stick(Direction8.None));
        Assert.True(scenario.World.Ball.Position.Z > 1f);
    }

    [Fact]
    public void Reversing_far_from_the_ball_does_not_chip()
    {
        var scenario = new Scenario();
        scenario.StartDribblingUp();
        scenario.Run(1f, Scenario.Stick(Direction8.Right));
        scenario.Step(Scenario.Stick(Direction8.Left));
        Assert.Empty(scenario.Kicks(KickKind.Chip));
    }

    [Fact]
    public void Aftertouch_bends_a_shot_towards_the_joystick_side()
    {
        float LateralAfter(Direction8 stick, bool aftertouch = true)
        {
            var scenario = new Scenario();
            scenario.Controller.AftertouchEnabled = aftertouch;
            scenario.StartDribblingUp();
            float startX = scenario.BallXY.X;
            scenario.Step(UpFire);
            scenario.Run(0.8f, Scenario.Stick(stick));
            return scenario.BallXY.X - startX;
        }

        float straight = LateralAfter(Direction8.Up);
        float right = LateralAfter(Direction8.Right);
        float upLeft = LateralAfter(Direction8.UpLeft);
        Assert.Equal(0f, straight, 3);
        Assert.True(right > 0.8f, $"curl right {right}");
        Assert.True(upLeft < -0.4f, $"curl left {upLeft}");
        Assert.True(MathF.Abs(upLeft) < MathF.Abs(right), "diagonals bend less than full sideways input");
        Assert.Equal(0f, LateralAfter(Direction8.Right, aftertouch: false), 3);
        Assert.Equal(0f, LateralAfter(Direction8.DownRight), 3);
    }

    [Fact]
    public void Aftertouch_only_works_during_a_short_window()
    {
        var late = new Scenario();
        late.StartDribblingUp();
        late.Step(UpFire);
        late.Run(late.Content.Controls.Aftertouch.Duration + 0.05f, Up);
        Vector3 velocityBefore = late.World.Ball.Velocity;
        late.Step(Scenario.Stick(Direction8.Right));
        Assert.Equal(VectorX(velocityBefore), VectorX(late.World.Ball.Velocity), 4);
    }

    [Fact]
    public void Simulation_is_deterministic_for_identical_inputs()
    {
        Vector3 Play()
        {
            var scenario = new Scenario("bumpy", seed: 99);
            scenario.StartDribblingUp();
            scenario.Step(UpFire);
            scenario.Run(0.3f, Scenario.Stick(Direction8.UpRight));
            scenario.Run(2f, Scenario.Stick(Direction8.None));
            return scenario.World.Ball.Position;
        }

        Assert.Equal(Play(), Play());
    }

    private static float VectorX(Vector3 v) => v.X;

    /// <summary>A player standing still with the ball under control (fire held).</summary>
    private static Scenario TrappedBall()
    {
        var scenario = new Scenario();
        scenario.Player.PlaceAt(new Vector2(0f, 30f), Direction8.Up);
        scenario.World.Ball.PlaceAt(new Vector2(0f, 27f));
        scenario.RunUntil(() => scenario.Controller.Phase == ControlPhase.Holding, UpFire);
        scenario.Run(0.4f, Scenario.Stick(Direction8.None, fire: true));
        Assert.Equal(ControlPhase.Holding, scenario.Controller.Phase);
        scenario.Events.Clear();
        return scenario;
    }
}
