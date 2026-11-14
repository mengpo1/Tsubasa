using System.Numerics;

namespace Arcade.Core.Input;

/// <summary>
/// The eight joystick directions of the historical digital joystick, plus <see cref="None"/> (centred).
/// World axes: +X to the right of the screen, +Y towards the bottom of the screen.
/// </summary>
public enum Direction8
{
    None = 0,
    Up = 1,
    UpRight = 2,
    Right = 3,
    DownRight = 4,
    Down = 5,
    DownLeft = 6,
    Left = 7,
    UpLeft = 8,
}

public static class Direction8Extensions
{
    private const float Diagonal = 0.70710678f;

    private static readonly Vector2[] Vectors =
    [
        Vector2.Zero,
        new(0f, -1f),
        new(Diagonal, -Diagonal),
        new(1f, 0f),
        new(Diagonal, Diagonal),
        new(0f, 1f),
        new(-Diagonal, Diagonal),
        new(-1f, 0f),
        new(-Diagonal, -Diagonal),
    ];

    /// <summary>Unit vector of the direction (zero for <see cref="Direction8.None"/>).</summary>
    public static Vector2 ToVector(this Direction8 direction) => Vectors[(int)direction];

    /// <summary>Index 0..7 starting at Up, clockwise. Used for 8-direction sprite rows.</summary>
    public static int ToIndex(this Direction8 direction) =>
        direction == Direction8.None ? throw new ArgumentOutOfRangeException(nameof(direction), "Centred has no index.") : (int)direction - 1;

    public static Direction8 Opposite(this Direction8 direction) =>
        direction == Direction8.None ? Direction8.None : (Direction8)((((int)direction - 1 + 4) % 8) + 1);

    /// <summary>
    /// Quantises an analogue vector to the nearest of the eight directions.
    /// Vectors shorter than <paramref name="deadZone"/> return <see cref="Direction8.None"/>.
    /// </summary>
    public static Direction8 FromVector(Vector2 value, float deadZone = 0.3f)
    {
        if (value.LengthSquared() < deadZone * deadZone)
        {
            return Direction8.None;
        }

        // Angle measured clockwise from "up" (screen coordinates, +Y down).
        float angle = MathF.Atan2(value.X, -value.Y);
        int sector = (int)MathF.Round(angle / (MathF.PI / 4f));
        sector = ((sector % 8) + 8) % 8;
        return (Direction8)(sector + 1);
    }

    /// <summary>Builds a direction from four digital switches, as a joystick or a keyboard would.</summary>
    public static Direction8 FromSwitches(bool up, bool down, bool left, bool right)
    {
        int x = (right ? 1 : 0) - (left ? 1 : 0);
        int y = (down ? 1 : 0) - (up ? 1 : 0);
        return FromVector(new Vector2(x, y), 0.5f);
    }
}
