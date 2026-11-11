using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Arcade.Core.Content;

/// <summary>
/// Identity card of a content pack (<c>manifest.json</c> at the root of the pack).
/// What a pack adds or replaces is defined by the files it contains, not by the manifest;
/// the loader reports these files (see <see cref="ContentPack.Files"/>).
/// </summary>
public sealed partial record ModManifest
{
    /// <summary>Data format understood by this build. Packs declaring a newer format are rejected.</summary>
    public const int CurrentFormatVersion = 1;

    [JsonRequired]
    public string Id { get; init; } = string.Empty;

    [JsonRequired]
    public string Name { get; init; } = string.Empty;

    [JsonRequired]
    public string Version { get; init; } = string.Empty;

    [JsonRequired]
    public int FormatVersion { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>Returns the list of problems; empty when the manifest is valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (!IdPattern().IsMatch(Id))
        {
            problems.Add($"id '{Id}' must contain only lowercase letters, digits, '-', '_' or '.', and start with a letter or digit");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("name must not be empty");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            problems.Add("version must not be empty");
        }

        if (FormatVersion < 1)
        {
            problems.Add("formatVersion must be 1 or greater");
        }
        else if (FormatVersion > CurrentFormatVersion)
        {
            problems.Add($"formatVersion {FormatVersion} is newer than the supported version {CurrentFormatVersion}; update the game");
        }

        return problems;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$")]
    private static partial Regex IdPattern();
}
