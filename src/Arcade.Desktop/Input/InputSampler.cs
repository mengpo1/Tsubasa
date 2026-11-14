using Arcade.Core.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Arcade.Desktop.Input;

/// <summary>
/// Turns physical devices into logical inputs: one <see cref="ControlInput"/> per control set (8-way direction
/// + fire) and edge-triggered <see cref="SystemAction"/>s. A fire press shorter than a simulation step is latched
/// so that it is never lost when the frame rate is higher than the simulation rate.
/// </summary>
internal sealed class InputSampler
{
    private readonly PlayerBinding[] _players;
    private readonly Dictionary<SystemAction, Keys[]> _system;
    private readonly float _deadZone;
    private readonly bool[] _fireLatched;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;

    public InputSampler(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _players = [.. config.Players.Select(PlayerBinding.From)];
        _fireLatched = new bool[_players.Length];
        _deadZone = config.GamepadDeadZone;
        _system = config.System.ToDictionary(
            pair => Enum.Parse<SystemAction>(pair.Key, ignoreCase: true),
            pair => ParseKeys(pair.Value));
    }

    public int ControlSets => _players.Length;

    /// <summary>Reads the devices; call once per frame.</summary>
    public void Update()
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();
        for (int i = 0; i < _players.Length; i++)
        {
            if (IsFireDown(_players[i]))
            {
                _fireLatched[i] = true;
            }
        }
    }

    /// <summary>Input of a control set for one simulation step. Clears the fire latch.</summary>
    public ControlInput ReadControl(int index)
    {
        PlayerBinding binding = _players[index];
        bool fire = IsFireDown(binding) || _fireLatched[index];
        _fireLatched[index] = false;
        return new ControlInput(ReadDirection(binding), fire);
    }

    public bool WasPressed(SystemAction action) =>
        _system.TryGetValue(action, out Keys[]? keys) && keys.Any(key => _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key));

    private Direction8 ReadDirection(PlayerBinding binding)
    {
        bool up = binding.Up.Any(_keyboard.IsKeyDown);
        bool down = binding.Down.Any(_keyboard.IsKeyDown);
        bool left = binding.Left.Any(_keyboard.IsKeyDown);
        bool right = binding.Right.Any(_keyboard.IsKeyDown);
        Direction8 keyboard = Direction8Extensions.FromSwitches(up, down, left, right);
        if (keyboard != Direction8.None || binding.Gamepad < 0)
        {
            return keyboard;
        }

        GamePadState pad = GamePad.GetState(binding.Gamepad);
        if (!pad.IsConnected)
        {
            return Direction8.None;
        }

        Direction8 dpad = Direction8Extensions.FromSwitches(
            pad.DPad.Up == ButtonState.Pressed,
            pad.DPad.Down == ButtonState.Pressed,
            pad.DPad.Left == ButtonState.Pressed,
            pad.DPad.Right == ButtonState.Pressed);
        if (dpad != Direction8.None)
        {
            return dpad;
        }

        Vector2 stick = pad.ThumbSticks.Left;
        // Screen coordinates: +Y is down, the stick reports +Y up.
        return Direction8Extensions.FromVector(new System.Numerics.Vector2(stick.X, -stick.Y), _deadZone);
    }

    private bool IsFireDown(PlayerBinding binding)
    {
        if (binding.Fire.Any(_keyboard.IsKeyDown))
        {
            return true;
        }

        if (binding.Gamepad < 0)
        {
            return false;
        }

        GamePadState pad = GamePad.GetState(binding.Gamepad);
        return pad.IsConnected && binding.GamepadFire.Any(pad.IsButtonDown);
    }

    private static Keys[] ParseKeys(IEnumerable<string> names) => [.. names.Select(name => Enum.Parse<Keys>(name, ignoreCase: true))];

    private sealed record PlayerBinding(Keys[] Up, Keys[] Down, Keys[] Left, Keys[] Right, Keys[] Fire, int Gamepad, Buttons[] GamepadFire)
    {
        public static PlayerBinding From(PlayerBindingConfig config) => new(
            ParseKeys(config.Up),
            ParseKeys(config.Down),
            ParseKeys(config.Left),
            ParseKeys(config.Right),
            ParseKeys(config.Fire),
            config.Gamepad,
            [.. config.GamepadFire.Select(name => Enum.Parse<Buttons>(name, ignoreCase: true))]);
    }
}
