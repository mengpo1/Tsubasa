using System.Text.Json;
using Arcade.Core.Content;

namespace Arcade.Desktop.Hosting;

/// <summary>
/// Optional per-installation settings (<c>settings.json</c> in the game folder, not versioned).
/// Example: <c>{ "language": "fr", "mods": ["example-mod"] }</c>.
/// </summary>
internal sealed record UserSettings
{
    public const string FileName = "settings.json";

    public string? Language { get; init; }

    public IReadOnlyList<string> Mods { get; init; } = [];

    public bool? Aftertouch { get; init; }

    public int? WindowScale { get; init; }

    public static UserSettings Load(string gameRoot)
    {
        string path = Path.Combine(gameRoot, FileName);
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), ContentJson.SerializerOptions) ?? new UserSettings();
        }
        catch (JsonException exception)
        {
            throw new ContentException($"Invalid {FileName}: {exception.Message}", exception);
        }
    }
}
