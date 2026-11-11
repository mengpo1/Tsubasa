namespace Arcade.Core.Simulation;

/// <summary>
/// Deterministic, seedable pseudo-random generator (xoshiro128**).
/// Simulation code must use this instead of <see cref="Random"/> so that runs and tests are reproducible.
/// </summary>
public sealed class GameRandom
{
    private uint _s0;
    private uint _s1;
    private uint _s2;
    private uint _s3;

    public GameRandom(ulong seed)
    {
        Seed = seed;
        // SplitMix64 expands the seed into a non-zero state.
        ulong x = seed;
        _s0 = (uint)SplitMix(ref x);
        _s1 = (uint)SplitMix(ref x);
        _s2 = (uint)SplitMix(ref x);
        _s3 = (uint)SplitMix(ref x);
    }

    public ulong Seed { get; }

    public uint NextUInt()
    {
        uint result = RotateLeft(_s1 * 5, 7) * 9;
        uint t = _s1 << 9;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 11);
        return result;
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / (1 << 24));

    /// <summary>Uniform float in [min, max).</summary>
    public float Range(float min, float max) => min + ((max - min) * NextFloat());

    /// <summary>Uniform float in [-1, 1).</summary>
    public float NextSigned() => (NextFloat() * 2f) - 1f;

    private static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));

    private static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
