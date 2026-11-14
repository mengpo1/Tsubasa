using System.Text.Json;
using Arcade.Core.Common;

namespace Arcade.Core.Content;

/// <summary>
/// Where the game data lives, which visual theme to apply and which mods to enable, in load order
/// (base, then theme, then mods; later = higher priority).
/// </summary>
public sealed record ContentLoadOptions(string GameRoot, IReadOnlyList<string> EnabledMods, string? Theme = null)
{
    public const string BaseFolder = "content";
    public const string ModsFolder = "mods";
    public const string ThemesFolder = "themes";
    public const string ManifestFile = "manifest.json";

    /// <summary>The only data files a theme may provide: it changes the look, never the rules.</summary>
    public static IReadOnlyList<string> ThemeDataFiles { get; } = [AssetCatalog.FileName, "config/display.json"];
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

        ContentPack basePack = ReadPack(baseDirectory, PackKind.Base, out IReadOnlyList<string> baseProblems);
        if (baseProblems.Count > 0)
        {
            throw new ContentException($"Base content is invalid: {string.Join("; ", baseProblems)}");
        }

        var packs = new List<ContentPack> { basePack };
        log.Info("content", "Base content loaded", ("files", basePack.Files.Count), ("path", baseDirectory));
        if (!string.IsNullOrEmpty(options.Theme))
        {
            LoadTheme(options, packs, issues, log);
        }

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

            ContentPack pack = ReadPack(modDirectory, PackKind.Mod, out IReadOnlyList<string> problems);
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

    /// <summary>Lists the themes present on disk (folders of <c>content/themes/</c> with a manifest), sorted by id.</summary>
    public static IReadOnlyList<string> ListThemes(string gameRoot)
    {
        string themes = Path.Combine(gameRoot, ContentLoadOptions.BaseFolder, ContentLoadOptions.ThemesFolder);
        if (!Directory.Exists(themes))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(themes)
            .Where(directory => File.Exists(Path.Combine(directory, ContentLoadOptions.ManifestFile)))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)];
    }

    private static void LoadTheme(ContentLoadOptions options, List<ContentPack> packs, List<ContentIssue> issues, GameLog log)
    {
        string id = options.Theme!;
        string directory = Path.Combine(options.GameRoot, ContentLoadOptions.BaseFolder, ContentLoadOptions.ThemesFolder, id);
        if (!Directory.Exists(directory))
        {
            Report(issues, log, ContentIssueSeverity.Error, id, $"Theme not found: '{directory}'. Using the default look.");
            return;
        }

        ContentPack pack = ReadPack(directory, PackKind.Theme, out IReadOnlyList<string> problems);
        if (problems.Count == 0 && !string.Equals(pack.Id, id, StringComparison.Ordinal))
        {
            problems = [$"manifest id '{pack.Id}' does not match the folder name '{id}'"];
        }

        if (problems.Count > 0)
        {
            Report(issues, log, ContentIssueSeverity.Error, id, $"Theme rejected: {string.Join("; ", problems)}");
            return;
        }

        packs.Add(pack);
        log.Info("content", "Theme loaded", ("id", pack.Id), ("files", pack.Files.Count));
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

    private static ContentPack ReadPack(string directory, PackKind kind, out IReadOnlyList<string> problems)
    {
        var found = new List<string>();
        string manifestPath = Path.Combine(directory, ContentLoadOptions.ManifestFile);
        ModManifest manifest = ReadManifest(manifestPath, found);
        var files = new List<string>();
        string root = Path.GetFullPath(directory);
        foreach (string fullPath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            if (relative == ContentLoadOptions.ManifestFile || IsHidden(relative)
                || (kind == PackKind.Base && relative.StartsWith(ContentLoadOptions.ThemesFolder + "/", StringComparison.Ordinal)))
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
                if (kind == PackKind.Theme && !ContentLoadOptions.ThemeDataFiles.Contains(normalized, StringComparer.Ordinal))
                {
                    found.Add($"'{normalized}' is not allowed in a theme: a theme only changes the look ({string.Join(", ", ContentLoadOptions.ThemeDataFiles)} and image or font files)");
                }
            }

            files.Add(normalized);
        }

        files.Sort(StringComparer.Ordinal);
        if (kind == PackKind.Base && manifest.Id != "base")
        {
            found.Add($"the base content manifest id must be 'base' (found '{manifest.Id}')");
        }

        problems = found;
        return new ContentPack(manifest, root, kind, files);
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
