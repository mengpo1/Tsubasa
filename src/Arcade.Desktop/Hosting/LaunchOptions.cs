using System.Globalization;

namespace Arcade.Desktop.Hosting;

/// <summary>Command-line options. Unknown options are an error (no silent typos).</summary>
internal sealed record LaunchOptions
{
    public const string Usage = """
        Usage: Arcade.Desktop [options]
          --mode <match|training> Friendly match (default) or training pitch
          --home <id>            Home team (match), default reds
          --away <id>            Away team (match), default blues
          --players <0|1|2>      Humans: 0 = computer vs computer, 1 = you (home) vs computer (default),
                                 2 = keyboard set 1 (home) vs keyboard set 2 (away)
          --level <id>           Computer level: easy, normal, hard
          --half-minutes <n>     Real length of a half in minutes (default: ruleset, 3)
          --seed <n>             Random seed of the match
          --root <dir>           Game folder containing content/ and mods/ (default: auto-detected)
          --mod <id>             Enable a mod (repeatable, load order = priority). Overrides settings.json
          --no-mods              Disable every mod
          --list-mods            Print installed mods and exit
          --lang <code>          Language (en, fr, ...)
          --pitch <id>           Training pitch (normal, wet, soggy, artificial, icy, muddy, bumpy)
          --team <id>            Team of the training player (training)
          --scale <n>            Window scale of the virtual resolution
          --fullscreen           Start in full screen
          --debug                Start with the debug overlay (F1)
          --script <file>        Scripted input (JSON) instead of the keyboard; runs at a fixed 60 Hz
          --capture-ticks <list> Save screenshots at these simulation ticks (comma separated)
          --capture-dir <dir>    Folder of the screenshots (default: artifacts/screenshots)
          --exit-tick <n>        Quit after this simulation tick
        """;

    public string Mode { get; init; } = "match";

    public string Home { get; init; } = "reds";

    public string Away { get; init; } = "blues";

    public int Players { get; init; } = 1;

    public string? Level { get; init; }

    public float? HalfMinutes { get; init; }

    public ulong? Seed { get; init; }

    public string? Root { get; init; }

    public IReadOnlyList<string>? Mods { get; init; }

    public bool NoMods { get; init; }

    public bool ListMods { get; init; }

    public string? Language { get; init; }

    public string? Pitch { get; init; }

    public string? Team { get; init; }

    public int? WindowScale { get; init; }

    public bool Fullscreen { get; init; }

    public bool Debug { get; init; }

    public string? ScriptPath { get; init; }

    public IReadOnlyList<long> CaptureTicks { get; init; } = [];

    public string CaptureDirectory { get; init; } = Path.Combine("artifacts", "screenshots");

    public long? ExitTick { get; init; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new LaunchOptions();
        var mods = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"Option {arg} needs a value.\n{Usage}");
            options = arg switch
            {
                "--mode" => options with { Mode = ParseMode(Next()) },
                "--home" => options with { Home = Next() },
                "--away" => options with { Away = Next() },
                "--players" => options with { Players = ParsePlayers(Next()) },
                "--level" => options with { Level = Next() },
                "--half-minutes" => options with { HalfMinutes = ParseInt(arg, Next()) },
                "--seed" => options with { Seed = (ulong)ParseInt(arg, Next()) },
                "--root" => options with { Root = Next() },
                "--mod" => AddMod(options, mods, Next()),
                "--no-mods" => options with { NoMods = true },
                "--list-mods" => options with { ListMods = true },
                "--lang" => options with { Language = Next() },
                "--pitch" => options with { Pitch = Next() },
                "--team" => options with { Team = Next() },
                "--scale" => options with { WindowScale = ParseInt(arg, Next()) },
                "--fullscreen" => options with { Fullscreen = true },
                "--debug" => options with { Debug = true },
                "--script" => options with { ScriptPath = Next() },
                "--capture-ticks" => options with { CaptureTicks = [.. Next().Split(',').Select(value => (long)ParseInt(arg, value))] },
                "--capture-dir" => options with { CaptureDirectory = Next() },
                "--exit-tick" => options with { ExitTick = ParseInt(arg, Next()) },
                "--help" or "-h" => throw new ArgumentException(Usage),
                _ => throw new ArgumentException($"Unknown option '{arg}'.\n{Usage}"),
            };
        }

        return options;
    }

    private static LaunchOptions AddMod(LaunchOptions options, List<string> mods, string id)
    {
        mods.Add(id);
        return options with { Mods = mods };
    }

    private static string ParseMode(string value) =>
        value is "match" or "training" ? value : throw new ArgumentException($"Option --mode expects 'match' or 'training', got '{value}'.");

    private static int ParsePlayers(string value) =>
        value is "0" or "1" or "2" ? value[0] - '0' : throw new ArgumentException($"Option --players expects 0, 1 or 2, got '{value}'.");

    private static int ParseInt(string option, string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result >= 0
            ? result
            : throw new ArgumentException($"Option {option} expects a non-negative integer, got '{value}'.");
}
