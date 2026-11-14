using System.Numerics;

namespace Arcade.Core.Common;

/// <summary>Small, allocation-free vector helpers used by the simulation.</summary>
public static class VectorMath
{
    /// <summary>Squared length under which a vector is treated as zero.</summary>
    public const float EpsilonSquared = 1e-10f;

    public static Vector2 SafeNormalize(Vector2 value)
    {
        float lengthSquared = value.LengthSquared();
        return lengthSquared > EpsilonSquared ? value / MathF.Sqrt(lengthSquared) : Vector2.Zero;
    }

    public static Vector2 ClampLength(Vector2 value, float maxLength)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared <= maxLength * maxLength)
        {
            return value;
        }

        return value * (maxLength / MathF.Sqrt(lengthSquared));
    }

    public static Vector3 ClampLength(Vector3 value, float maxLength)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared <= maxLength * maxLength)
        {
            return value;
        }

        return value * (maxLength / MathF.Sqrt(lengthSquared));
    }

    /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
    public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
    {
        Vector2 delta = target - current;
        float distanceSquared = delta.LengthSquared();
        if (distanceSquared <= maxDelta * maxDelta || distanceSquared <= EpsilonSquared)
        {
            return target;
        }

        return current + (delta * (maxDelta / MathF.Sqrt(distanceSquared)));
    }

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (MathF.Sign(target - current) * maxDelta);
    }

    /// <summary>Returns the vector rotated by +90 degrees (screen coordinates: to the right of a "down" vector).</summary>
    public static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);

    public static Vector2 Rotate(Vector2 value, float radians)
    {
        (float sin, float cos) = MathF.SinCos(radians);
        return new Vector2((value.X * cos) - (value.Y * sin), (value.X * sin) + (value.Y * cos));
    }

    public static Vector2 Horizontal(Vector3 value) => new(value.X, value.Y);

    public static Vector3 WithHorizontal(Vector3 value, Vector2 horizontal) => new(horizontal.X, horizontal.Y, value.Z);

    public static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);

    /// <summary>Linear interpolation of a 0..1 rating between two bounds.</summary>
    public static float FromRating(float rating, float atZero, float atOne) => Lerp(atZero, atOne, Math.Clamp(rating, 0f, 1f));
}
