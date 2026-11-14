using System.Text.Json;
using Arcade.Core.Content;
using Arcade.Core.Input;

namespace Arcade.Desktop.Input;

/// <summary>
/// Input timeline read from a JSON file, used for automated runs and screenshots:
/// <c>{ "steps": [ { "from": 0, "to": 60, "direction": "up", "fire": false } ] }</c> (ticks, "to" exclusive).
/// Ticks not covered by any step are neutral.
/// </summary>
internal sealed class ScriptedInput
{
    private readonly IReadOnlyList<ScriptStep> _steps;

    private ScriptedInput(IReadOnlyList<ScriptStep> steps)
    {
        _steps = steps;
    }

    public static ScriptedInput Load(string path)
    {
        try
        {
            var script = JsonSerializer.Deserialize<ScriptFile>(File.ReadAllText(path), ContentJson.SerializerOptions)
                ?? throw new ContentException($"Script '{path}' is empty.");
            return new ScriptedInput(script.Steps);
        }
        catch (JsonException exception)
        {
            throw new ContentException($"Invalid input script '{path}': {exception.Message}", exception);
        }
    }

    public ControlInput At(long tick)
    {
        foreach (ScriptStep step in _steps)
        {
            if (tick >= step.From && tick < step.To)
            {
                return new ControlInput(step.Direction, step.Fire);
            }
        }

        return ControlInput.Neutral;
    }

    private sealed record ScriptFile
    {
        public required IReadOnlyList<ScriptStep> Steps { get; init; }
    }

    private sealed record ScriptStep
    {
        public required long From { get; init; }

        public required long To { get; init; }

        public Direction8 Direction { get; init; }

        public bool Fire { get; init; }
    }
}
