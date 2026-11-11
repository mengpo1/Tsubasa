using System.Text.Json;
using Arcade.Core.Common;

namespace Arcade.Core.Content;

/// <summary>Where the game data lives and which mods to enable, in load order (later = higher priority).</summary>
public sealed record ContentLoadOptions(string GameRoot, IReadOnlyList<string> EnabledMods)
{
    public const string BaseFolder = "content";
    public const string ModsFolder = "mods";
    public const string ManifestFile = "manifest.json";
}

public sealed record ContentLoadResult(ContentRepository Repository, IReadOnlyList<ContentIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == ContentIssueSeverity.Error);
}

/// <summary>
/// Builds the layered <see cref="ContentRepository"/> from the base content folder and the enabled mods.
/// A mod with an invalid manifest or a malformed JSON file is rejected as a whole (never partially applied)
/// and the problem is reported. Problems with the base content are fatal.
/// </summary>
public static class ContentLoader
{
    public static ContentLoadResult Load(ContentLoadOptions options, GameLog log)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(log);
        var issues = new List<ContentIssue>();
        string baseDirectory = Path.Combine(options.GameRoot, ContentLoadOptions.BaseFolder);
        if (!Directory.Exists(baseDirectory))
        {
            throw new ContentException($"Base content folder not found: '{baseDirectory}'.");
        }

        ContentPack basePack = ReadPack(baseDirectory, isBase: true, out IReadOnlyList<string> baseProblems);
        if (baseProblems.Count > 0)
        {
            throw new ContentException($"Base content is invalid: {string.Join("; ", baseProblems)}");
        }

        var packs = new List<ContentPack> { basePack };
        log.Info("content", "Base content loaded", ("files", basePack.Files.Count), ("path", baseDirectory));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string modId in options.EnabledMods)
        {
            if (!seen.Add(modId))
            {
                Report(issues, log, ContentIssueSeverity.Warning, modId, "Mod is listed more than once; only the first occurrence is used.");
                continue;
            }

            string modDirectory = Path.Combine(options.GameRoot, ContentLoadOptions.ModsFolder, modId);
            if (!Directory.Exists(modDirectory))
            {
                Report(issues, log, ContentIssueSeverity.Error, modId, $"Mod is enabled but its folder was not found: '{modDirectory}'.");
                continue;
            }

            ContentPack pack = ReadPack(modDirectory, isBase: false, out IReadOnlyList<string> problems);
            if (problems.Count == 0 && !string.Equals(pack.Id, modId, StringComparison.Ordinal))
            {
                problems = [$"manifest id '{pack.Id}' does not match the folder name '{modId}'"];
            }

            if (problems.Count > 0)
            {
                Report(issues, log, ContentIssueSeverity.Error, modId, $"Mod rejected: {string.Join("; ", problems)}");
                continue;
            }

            packs.Add(pack);
            log.Info("content", "Mod loaded", ("id", pack.Id), ("version", pack.Manifest.Version), ("files", pack.Files.Count));
            foreach (string file in pack.Files)
            {
                string kind = basePack.Contains(file) ? "overrides" : "adds";
                log.Debug("content", "Mod file", ("mod", pack.Id), ("file", file), ("effect", kind));
            }
        }

        return new ContentLoadResult(new ContentRepository(packs), issues);
    }

    /// <summary>Lists the mod folders present on disk (whether enabled or not), sorted by id.</summary>
    public static IReadOnlyList<string> ListInstalledMods(string gameRoot)
    {
        string modsDirectory = Path.Combine(gameRoot, ContentLoadOptions.ModsFolder);
        if (!Directory.Exists(modsDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(modsDirectory)
            .Where(directory => File.Exists(Path.Combine(directory, ContentLoadOptions.ManifestFile)))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)];
    }

    private static ContentPack ReadPack(string directory, bool isBase, out IReadOnlyList<string> problems)
    {
        var found = new List<string>();
        string manifestPath = Path.Combine(directory, ContentLoadOptions.ManifestFile);
        ModManifest manifest = ReadManifest(manifestPath, found);
        var files = new List<string>();
        string root = Path.GetFullPath(directory);
        foreach (string fullPath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            if (relative == ContentLoadOptions.ManifestFile || IsHidden(relative))
            {
                continue;
            }

            if (!ContentPath.TryNormalize(relative, out string normalized, out string? error))
            {
                found.Add($"file '{relative}' has an unsupported name ({error})");
                continue;
            }

            if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                CheckJson(fullPath, normalized, found);
            }

            files.Add(normalized);
        }

        files.Sort(StringComparer.Ordinal);
        if (isBase && manifest.Id != "base")
        {
            found.Add($"the base content manifest id must be 'base' (found '{manifest.Id}')");
        }

        problems = found;
        return new ContentPack(manifest, root, isBase, files);
    }

    private static ModManifest ReadManifest(string manifestPath, List<string> problems)
    {
        var fallback = new ModManifest { Id = Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "unknown", Name = "?", Version = "?", FormatVersion = 0 };
        if (!File.Exists(manifestPath))
        {
            problems.Add($"missing {ContentLoadOptions.ManifestFile}");
            return fallback;
        }

        try
        {
            ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(manifestPath), ContentJson.SerializerOptions);
            if (manifest is null)
            {
                problems.Add($"{ContentLoadOptions.ManifestFile} is empty");
                return fallback;
            }

            foreach (string problem in manifest.Validate())
            {
                problems.Add($"{ContentLoadOptions.ManifestFile}: {problem}");
            }

            return manifest;
        }
        catch (JsonException exception)
        {
            problems.Add($"{ContentLoadOptions.ManifestFile} is invalid: {exception.Message}");
            return fallback;
        }
    }

    private static void CheckJson(string fullPath, string relativePath, List<string> problems)
    {
        try
        {
            if (ContentJson.Parse(File.ReadAllText(fullPath)) is not System.Text.Json.Nodes.JsonObject)
            {
                problems.Add($"'{relativePath}' must contain a JSON object at its root");
            }
        }
        catch (JsonException exception)
        {
            problems.Add($"'{relativePath}' is malformed JSON: {exception.Message}");
        }
    }

    private static bool IsHidden(string relativePath) =>
        relativePath.Split('/').Any(segment => segment.StartsWith('.'));

    private static void Report(List<ContentIssue> issues, GameLog log, ContentIssueSeverity severity, string packId, string message)
    {
        issues.Add(new ContentIssue(severity, packId, message));
        if (severity == ContentIssueSeverity.Error)
        {
            log.Error("content", message, ("pack", packId));
        }
        else
        {
            log.Warn("content", message, ("pack", packId));
        }
    }
}
