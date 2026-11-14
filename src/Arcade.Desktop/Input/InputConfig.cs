using Arcade.Core.Content;
using Microsoft.Xna.Framework.Input;

namespace Arcade.Desktop.Input;

/// <summary>Application-level actions (not gameplay): debug, reload, options...</summary>
internal enum SystemAction
{
    Quit,
    Pause,
    Debug,
    Reload,
    ToggleMods,
    ResetBall,
    ToggleAftertouch,
    NextPitch,
    NextLanguage,
    NextTheme,
    ToggleRadar,
    ZoomIn,
    ZoomOut,
    Fullscreen,
}

/// <summary>
/// Remappable bindings (<c>config/input.json</c>): physical keys and buttons are named with the MonoGame
/// <see cref="Keys"/> and <see cref="Buttons"/> enum names. Validated at load time.
/// </summary>
internal sealed record InputConfig : IContentData
{
    public const string FileName = "config/input.json";

    public required IReadOnlyList<PlayerBindingConfig> Players { get; init; }

    /// <summary>Analogue stick dead zone before the 8-way quantisation.</summary>
    public required float GamepadDeadZone { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> System { get; init; }

    public void Validate(ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (Players.Count == 0)
        {
            problems.Add("players must define at least one control set");
        }

        for (int i = 0; i < Players.Count; i++)
        {
            DataChecks.Nested(problems, $"players[{i}]", Players[i]);
        }

        DataChecks.InRange(problems, "gamepadDeadZone", GamepadDeadZone, 0f, 0.95f);
        foreach ((string action, IReadOnlyList<string> keys) in System)
        {
            if (!Enum.TryParse<SystemAction>(action, ignoreCase: true, out _))
            {
                problems.Add($"system.{action}: unknown action (known: {string.Join(", ", Enum.GetNames<SystemAction>())})");
            }

            PlayerBindingConfig.CheckKeys(problems, $"system.{action}", keys);
        }
    }
}

internal sealed record PlayerBindingConfig : IContentData
{
    public required IReadOnlyList<string> Up { get; init; }

    public required IReadOnlyList<string> Down { get; init; }

    public required IReadOnlyList<string> Left { get; init; }

    public required IReadOnlyList<string> Right { get; init; }

    public required IReadOnlyList<string> Fire { get; init; }

    /// <summary>Gamepad index used by this control set, or -1 for none.</summary>
    public required int Gamepad { get; init; }

    public required IReadOnlyList<string> GamepadFire { get; init; }

    public void Validate(ICollection<string> problems)
    {
        CheckKeys(problems, "up", Up);
        CheckKeys(problems, "down", Down);
        CheckKeys(problems, "left", Left);
        CheckKeys(problems, "right", Right);
        CheckKeys(problems, "fire", Fire);
        DataChecks.InRange(problems, "gamepad", Gamepad, -1, 3);
        foreach (string button in GamepadFire)
        {
            if (!Enum.TryParse<Buttons>(button, ignoreCase: true, out _))
            {
                problems.Add($"gamepadFire: unknown button '{button}'");
            }
        }
    }

    internal static void CheckKeys(ICollection<string> problems, string name, IReadOnlyList<string> keys)
    {
        foreach (string key in keys)
        {
            if (!Enum.TryParse<Keys>(key, ignoreCase: true, out _))
            {
                problems.Add($"{name}: unknown key '{key}'");
            }
        }
    }
}
