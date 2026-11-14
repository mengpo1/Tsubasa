using System.Text.Json;
using System.Text.Json.Nodes;
using Arcade.Importer.KickOff2;

const string Usage = """
    Imports the graphics of YOUR OWN copy of Kick Off 2 / The Final Whistle (Amiga) for the classic-amiga theme.
    Nothing is downloaded; the pictures stay on this computer (content/themes/classic-amiga/imported/, ignored by Git).

    Usage: dotnet run --project tools/Arcade.Importer -- [options]
      --adf <file>       Amiga disk image (.adf, or gzip-compressed .adz). Repeatable: later disks override
                         earlier ones (give Kick Off 2 first, then The Final Whistle).
      --files <folder>   Folder of files already extracted from the disk (sprites.dat, pitches.dat, title...). Repeatable.
      --root <folder>    Game folder containing content/ (default: found from the current folder).
      --theme <id>       Theme to fill (default: classic-amiga).
      --set-default      Also select the theme in settings.json.
    """;

string? root = null;
string theme = "classic-amiga";
bool setDefault = false;
var sources = new SourceFiles();
var inputs = new List<(bool Disk, string Path)>();
try
{
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Option {arg} needs a value.");
        switch (arg)
        {
            case "--adf": inputs.Add((true, Next())); break;
            case "--files": inputs.Add((false, Next())); break;
            case "--root": root = Next(); break;
            case "--theme": theme = Next(); break;
            case "--set-default": setDefault = true; break;
            case "--help" or "-h": Console.WriteLine(Usage); return 0;
            default: throw new ArgumentException($"Unknown option '{arg}'.");
        }
    }

    if (inputs.Count == 0)
    {
        throw new ArgumentException("Give at least one --adf or --files source.");
    }

    root ??= FindRoot() ?? throw new ArgumentException("Game folder not found: use --root.");
    string themeDirectory = Path.Combine(root, "content", "themes", theme);
    if (!File.Exists(Path.Combine(themeDirectory, "manifest.json")))
    {
        throw new ArgumentException($"Theme '{theme}' not found in {Path.Combine(root, "content", "themes")}.");
    }

    foreach ((bool disk, string path) in inputs)
    {
        if (disk)
        {
            sources.AddDiskImage(path);
        }
        else
        {
            sources.AddDirectory(path);
        }
    }

    ImportResult result = ClassicImporter.Run(sources, themeDirectory);
    Console.WriteLine($"{result.Written.Count} files written to {Path.GetDirectoryName(result.ReportPath)}");
    Console.WriteLine($"Theme resources installed: {result.ThemeTextures - result.ThemeTexturesMissing.Count} / {result.ThemeTextures}");
    foreach (string line in result.Missing.Concat(result.Problems))
    {
        Console.WriteLine($"  ! {line}");
    }

    Console.WriteLine($"Report: {result.ReportPath}");
    if (setDefault)
    {
        SelectTheme(root, theme);
        Console.WriteLine($"settings.json: theme set to '{theme}'.");
    }

    return result.Written.Count > 0 ? 0 : 1;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage);
    return 2;
}

static string? FindRoot()
{
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "content", "manifest.json")))
            {
                return directory.FullName;
            }
        }
    }

    return null;
}

static void SelectTheme(string root, string theme)
{
    string path = Path.Combine(root, "settings.json");
    JsonObject settings = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? [] : [];
    settings["theme"] = theme;
    File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
