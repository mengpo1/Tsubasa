namespace Arcade.Core.Simulation;

/// <summary>
/// Converts variable real elapsed time into a whole number of fixed simulation steps.
/// The simulation never reads the wall clock: it only ever advances by <see cref="StepSeconds"/>.
/// </summary>
public sealed class FixedStepClock
{
    private double _accumulator;

    /// <param name="stepsPerSecond">Simulation frequency (e.g. 60).</param>
    /// <param name="maxFrameSeconds">Upper bound on the real time consumed per frame, to avoid a "spiral of death" after a hitch.</param>
    public FixedStepClock(int stepsPerSecond, double maxFrameSeconds = 0.25)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepsPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameSeconds);
        StepsPerSecond = stepsPerSecond;
        StepSeconds = 1f / stepsPerSecond;
        MaxFrameSeconds = maxFrameSeconds;
    }

    public int StepsPerSecond { get; }

    public float StepSeconds { get; }

    public double MaxFrameSeconds { get; }

    public bool Paused { get; set; }

    /// <summary>Total number of steps produced since creation.</summary>
    public long TotalSteps { get; private set; }

    /// <summary>Fraction (0..1) of a step left in the accumulator; used for render interpolation.</summary>
    public float Alpha => (float)(_accumulator * StepsPerSecond);

    /// <summary>Adds real elapsed time and returns how many fixed steps must be simulated now.</summary>
    public int Advance(double realElapsedSeconds)
    {
        if (Paused || realElapsedSeconds <= 0)
        {
            return 0;
        }

        _accumulator += Math.Min(realElapsedSeconds, MaxFrameSeconds);
        double step = 1.0 / StepsPerSecond;
        int steps = (int)(_accumulator / step);
        _accumulator -= steps * step;
        TotalSteps += steps;
        return steps;
    }

    public void Reset()
    {
        _accumulator = 0;
    }
}
