using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Players;

namespace Arcade.Core.Football.AI;

/// <summary>
/// Where the ball will be if nobody touches it, sampled every <see cref="Interval"/> seconds, computed once per
/// step with the same physics as the game (without random bumps). Used by the AI to intercept and by keepers
/// to read shots: the computer players predict, they never move the ball or read hidden information.
/// </summary>
public sealed class BallForecast
{
    public const float Interval = 0.1f;
    public const int Samples = 31;

    /// <summary>A ball higher than this cannot be played with the feet or the body (m).</summary>
    private const float PlayableHeight = 1.6f;

    private readonly Vector3[] _positions = new Vector3[Samples];

    public Vector3 At(int sample) => _positions[sample];

    public void Update(FootballWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        BallState state = world.Ball.Current;
        _positions[0] = state.Position;
        if (world.Ball.Status != BallStatus.Live)
        {
            Array.Fill(_positions, state.Position);
            return;
        }

        int stepsPerSample = Math.Max(1, (int)MathF.Round(Interval / world.StepSeconds));
        float h = world.StepSeconds;
        var frame = world.Content.Physics;
        for (int i = 1; i < Samples; i++)
        {
            for (int s = 0; s < stepsPerSample; s++)
            {
                world.BallPhysics.Integrate(ref state, world.Surface, world.Wind, Vector3.Zero, h, null);
                GoalFrameCollider.Resolve(ref state, world.Pitch, frame.GoalFrame, frame.Boards, frame.Ball.Radius);
            }

            _positions[i] = state.Position;
        }
    }

    /// <summary>
    /// Earliest point where a player running at <paramref name="speed"/> after <paramref name="reaction"/> seconds
    /// can meet the ball at a playable height. Returns the last sample when the ball cannot be reached.
    /// </summary>
    public (float Time, Vector2 Point) Intercept(Player player, float speed, float reaction, float reach)
    {
        ArgumentNullException.ThrowIfNull(player);
        for (int i = 0; i < Samples; i++)
        {
            float time = i * Interval;
            Vector3 ball = _positions[i];
            float reachable = (speed * MathF.Max(0f, time - reaction)) + reach;
            if (ball.Z < PlayableHeight && Vector2.Distance(player.Position, VectorMath.Horizontal(ball)) <= reachable)
            {
                return (time, VectorMath.Horizontal(ball));
            }
        }

        Vector3 last = _positions[Samples - 1];
        return ((Samples - 1) * Interval + (Vector2.Distance(player.Position, VectorMath.Horizontal(last)) / MathF.Max(speed, 0.1f)), VectorMath.Horizontal(last));
    }

    /// <summary>
    /// First sample where the ball crosses the horizontal line y = <paramref name="lineY"/> going outward
    /// (sign of <paramref name="outward"/>), with its x and the time. Null if it does not within the forecast.
    /// </summary>
    public (float Time, float X, float Height)? Crossing(float lineY, float outward)
    {
        for (int i = 1; i < Samples; i++)
        {
            Vector3 a = _positions[i - 1];
            Vector3 b = _positions[i];
            if ((a.Y - lineY) * outward < 0f && (b.Y - lineY) * outward >= 0f)
            {
                float t = (lineY - a.Y) / (b.Y - a.Y);
                return (((i - 1) + t) * Interval, a.X + ((b.X - a.X) * t), a.Z + ((b.Z - a.Z) * t));
            }
        }

        return null;
    }
}
