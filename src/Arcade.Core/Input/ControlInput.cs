namespace Arcade.Core.Input;

/// <summary>
/// Hardware-independent state of one historical control set (8-way joystick + one fire button)
/// for a single simulation step. Produced by the desktop input layer, by scripts or by tests.
/// </summary>
public readonly record struct ControlInput(Direction8 Direction, bool Fire)
{
    public static ControlInput Neutral => default;
}
