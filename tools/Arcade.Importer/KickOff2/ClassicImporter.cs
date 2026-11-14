using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Arcade.Importer.Amiga;
using Arcade.Importer.Imaging;

namespace Arcade.Importer.KickOff2;

/// <summary>What an import produced, for the report and the tests.</summary>
internal sealed class ImportResult
{
    public List<string> Written { get; } = [];

    public List<string> Missing { get; } = [];

    public List<string> Notes { get; } = [];

    public List<string> Problems { get; } = [];

    public int ThemeTextures { get; set; }

    public List<string> ThemeTexturesMissing { get; } = [];

    public string ReportPath { get; set; } = string.Empty;
}

/// <summary>
/// Converts the graphics of Kick Off 2 / The Final Whistle (Amiga) into the PNG files expected by the classic-amiga
/// theme (<c>content/themes/classic-amiga/imported/</c>), with contact sheets and a report. Everything that cannot be
/// found or decoded is listed; nothing is guessed silently.
/// </summary>
internal static class ClassicImporter
{
    public const string OutputFolder = "imported";
    public const string ReportFile = "IMPORT_REPORT.md";
    public const int SpriteSize = 16;

    /// <summary>Kit marker colours replaced by the team colours at run time (see the animations' palette in assets.json).</summary>
    public static readonly Rgb ShirtMarker = new(0xFF, 0x00, 0x00);
    public static readonly Rgb ShortsMarker = new(0x00, 0x00, 0xFF);

    /// <summary>Pitch picture suffixes of the original files and the pitch ids of the game data.</summary>
    internal static readonly (string Suffix, string PitchId, string? Note)[] Pitches =
    [
        ("nor", "normal", null),
        ("wet", "wet", null),
        ("sog", "soggy", null),
        ("art", "artificial", null),
        ("non", "bumpy", "The Final Whistle 'NON' pitch is assumed to be the bumpy one: not verified"),
        ("mud", "muddy", null),
        ("sno", "icy", "The snow pitch of The Final Whistle is used for the icy surface"),
        ("wem", "wembley", "No Wembley pitch exists in the game data yet: picture kept for later use"),
    ];

    private const string SpritesArchive = "sprites.dat";
    private const string PitchesArchive = "pitches.dat";

    public static ImportResult Run(SourceFiles sources, string themeDirectory)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var result = new ImportResult();
        string output = Path.Combine(Path.GetFullPath(themeDirectory), OutputFolder);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        result.Problems.AddRange(sources.Problems);
        ImportSprites(sources, output, result);
        ImportPitches(sources, output, result);
        ImportPictures(sources, output, result);
        ImportFonts(sources, output, result);
        CheckTheme(themeDirectory, result);
        result.ReportPath = Path.Combine(output, ReportFile);
        File.WriteAllText(result.ReportPath, BuildReport(sources, result), Encoding.UTF8);
        return result;
    }

    private static void ImportSprites(SourceFiles sources, string output, ImportResult result)
    {
        foreach (string style in Enumerable.Range(1, 6).SelectMany(n => new[] { $"{n}a", $"{n}b" }))
        {
            Decode(sources, $"style{style}.iff", SpritesArchive, result, (ilbm, name) =>
            {
                IndexedImage sheet = ToGrid(ilbm.Image, name, result);
                int transparent = ilbm.Image.TransparentIndex ?? 0;
                RgbaImage kit = sheet.ToRgba(index => index == transparent ? null
                    : index is 3 or 4 ? ShirtMarker
                    : index == 5 ? ShortsMarker
                    : sheet.Palette[index]);
                Save(kit, output, $"sprites/players-style{style}.png", result);
                Save(ContactSheet.Build(sheet.ToRgba(transparent), SpriteSize, SpriteSize, 3), output, $"contact/players-style{style}.png", result);
                result.Notes.Add($"{name}: {Describe(ilbm, sheet)}");
            });
        }

        foreach (string sheetName in new[] { "keeper", "ball" })
        {
            Decode(sources, $"{sheetName}.iff", SpritesArchive, result, (ilbm, name) =>
            {
                IndexedImage sheet = ToGrid(ilbm.Image, name, result);
                int transparent = ilbm.Image.TransparentIndex ?? 0;
                Save(sheet.ToRgba(transparent), output, $"sprites/{sheetName}.png", result);
                Save(ContactSheet.Build(sheet.ToRgba(transparent), SpriteSize, SpriteSize, 3), output, $"contact/{sheetName}.png", result);
                result.Notes.Add($"{name}: {Describe(ilbm, sheet)}");
            });
        }
    }

    private static void ImportPitches(SourceFiles sources, string output, ImportResult result)
    {
        DiskFile[] maps = [.. sources.Members(PitchesArchive).Where(f => f.Name.StartsWith("pdat.", StringComparison.OrdinalIgnoreCase))];
        DiskFile[] tileSets = [.. sources.Members(PitchesArchive).Where(f => f.Name.StartsWith("fbgraph4.", StringComparison.OrdinalIgnoreCase))];
        if (tileSets.Length == 0)
        {
            result.Missing.Add($"pitch tile sets (fbgraph4.* in {PitchesArchive}): the procedural pitch stays in use");
            return;
        }

        var produced = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiskFile tiles in tileSets.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            string suffix = tiles.Name[(tiles.Name.IndexOf('.', StringComparison.Ordinal) + 1)..].ToLowerInvariant();
            DiskFile? map = maps.FirstOrDefault(m => m.Name.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                ?? (maps.Length == 1 ? maps[0] : maps.FirstOrDefault(m => m.Name.Equals("pdat.nor", StringComparison.OrdinalIgnoreCase)));
            (string Suffix, string PitchId, string? Note) known = Pitches.FirstOrDefault(p => p.Suffix == suffix);
            string pitchId = known.PitchId ?? suffix;
            if (map is null)
            {
                result.Missing.Add($"tile map (pdat.*) for {tiles.Name}: pitch '{pitchId}' not composed");
                continue;
            }

            try
            {
                IlbmImage set = IlbmImage.Decode(tiles.Data);
                IndexedImage pitch = PitchComposer.Compose(map.Data, set.Image, result.Problems, $"{map.Name} + {tiles.Name}");
                Save(pitch.ToRgba(transparent: null), output, $"pitches/{pitchId}.png", result);
                Save(ContactSheet.Build(set.Image.ToRgba(transparent: null), SpriteSize, SpriteSize, 2), output, $"contact/tiles-{suffix}.png", result);
                produced.Add(pitchId);
                result.Notes.Add($"pitch '{pitchId}': {map.Name} ({pitch.Width}x{pitch.Height}) with {tiles.Name} ({set.Image.Width}x{set.Image.Height}, {set.Planes} planes, palette {string.Join(" ", set.Image.Palette.Take(8))})"
                    + (known.Note is null ? string.Empty : $". {known.Note}")
                    + (known.PitchId is null ? $". Unknown suffix '{suffix}': not used by the theme" : string.Empty));
            }
            catch (InvalidDataException exception)
            {
                result.Problems.Add($"{tiles.Name}: {exception.Message}");
            }
        }

        foreach ((string suffix, string pitchId, _) in Pitches.Where(p => !produced.Contains(p.PitchId)))
        {
            result.Missing.Add($"pitch '{pitchId}' (fbgraph4.{suffix}): not found in the sources");
        }
    }

    private static void ImportPictures(SourceFiles sources, string output, ImportResult result)
    {
        foreach ((string file, string target) in new[] { ("cornsel1.iff", "ui/cornsel1.png"), ("cornsel2.iff", "ui/cornsel2.png"), ("title", "ui/title.png") })
        {
            Decode(sources, file, null, result, (ilbm, name) =>
            {
                Save(ilbm.Image.ToRgba(ilbm.Image.TransparentIndex), output, target, result);
                result.Notes.Add($"{name}: {Describe(ilbm, ilbm.Image)}");
            });
        }
    }

    private static void ImportFonts(SourceFiles sources, string output, ImportResult result)
    {
        Decode(sources, "fonts.bl3", null, result, (ilbm, name) =>
        {
            RgbaImage font = ilbm.Image.ToRgba(transparent: 0);
            Save(font, output, "fonts/fonts-bl3.png", result);
            Save(ContactSheet.Build(font, 8, 8, 3), output, "contact/fonts-bl3.png", result);
            result.Notes.Add($"{name}: {Describe(ilbm, ilbm.Image)}. Character order not identified: not used by the theme yet");
        });

        DiskFile? raw = sources.Find("fonts.dat");
        if (raw is null)
        {
            result.Missing.Add("fonts.dat");
            return;
        }

        // Layout unknown: shown as 1-bit 8x8 glyphs (8 bytes each) so that it can be examined; not used by the theme.
        int glyphs = raw.Data.Length / 8;
        const int columns = 32;
        var sheet = new RgbaImage(columns * 8, ((glyphs + columns - 1) / columns) * 8);
        for (int g = 0; g < glyphs; g++)
        {
            for (int row = 0; row < 8; row++)
            {
                byte bits = raw.Data[(g * 8) + row];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((bits & (0x80 >> bit)) != 0)
                    {
                        sheet.Set((g % columns * 8) + bit, (g / columns * 8) + row, 255, 255, 255, 255);
                    }
                }
            }
        }

        Save(ContactSheet.Build(sheet, 8, 8, 2), output, "contact/fonts-dat-8x8.png", result);
        result.Notes.Add($"fonts.dat: {raw.Data.Length} bytes of raw data, layout unknown; shown as 1-bit 8x8 glyphs in contact/fonts-dat-8x8.png (hypothesis to check)");
    }

    /// <summary>
    /// Crops a sheet to whole 16x16 tiles. The original sheets are one line taller than a whole number of tiles
    /// (e.g. 320x161); the tile grid is taken to start at the top unless only the top lines are empty.
    /// </summary>
    private static IndexedImage ToGrid(IndexedImage image, string name, ImportResult result)
    {
        int extra = image.Height % SpriteSize;
        int columnsExtra = image.Width % SpriteSize;
        if (extra == 0 && columnsExtra == 0)
        {
            return image;
        }

        int transparent = image.TransparentIndex ?? 0;
        bool topEmpty = image.RowsEmpty(0, extra, transparent);
        bool bottomEmpty = image.RowsEmpty(image.Height - extra, extra, transparent);
        int top = topEmpty && !bottomEmpty ? extra : 0;
        result.Notes.Add($"{name}: {image.Width}x{image.Height} is not a whole number of 16x16 tiles; grid taken from line {top} "
            + $"(top extra line empty: {Yes(topEmpty)}, bottom extra line empty: {Yes(bottomEmpty)})");
        return image.Crop(0, top, image.Width - columnsExtra, image.Height - extra);
    }

    private static void Decode(SourceFiles sources, string file, string? archive, ImportResult result, Action<IlbmImage, string> use)
    {
        DiskFile? found = sources.Find(file, archive);
        if (found is null)
        {
            result.Missing.Add(archive is null ? file : $"{file} (in {archive})");
            return;
        }

        if (!IlbmImage.IsIlbm(found.Data))
        {
            result.Problems.Add($"{file}: not an IFF ILBM picture (packed or different format?); not imported");
            return;
        }

        try
        {
            use(IlbmImage.Decode(found.Data), file);
        }
        catch (InvalidDataException exception)
        {
            result.Problems.Add($"{file}: {exception.Message}");
        }
    }

    private static void Save(RgbaImage image, string output, string relative, ImportResult result)
    {
        PngWriter.Save(image, Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar)));
        result.Written.Add($"{OutputFolder}/{relative} ({image.Width}x{image.Height})");
    }

    private static string Describe(IlbmImage ilbm, IndexedImage used)
    {
        int[] histogram = used.Histogram();
        IEnumerable<string> colours = Enumerable.Range(0, used.Palette.Count)
            .Where(i => histogram[i] > 0)
            .Select(i => $"{i}={used.Palette[i]}×{histogram[i]}");
        return $"{ilbm.Image.Width}x{ilbm.Image.Height}, {ilbm.Planes} planes, masking {ilbm.Masking}, compression {ilbm.Compression}"
            + (ilbm.PaletteScaled ? ", 4-bit palette expanded" : string.Empty)
            + $"; colours used (index=colour×pixels): {string.Join(", ", colours)}";
    }

    private static void CheckTheme(string themeDirectory, ImportResult result)
    {
        string assets = Path.Combine(themeDirectory, "assets.json");
        if (!File.Exists(assets))
        {
            result.Problems.Add($"theme catalog not found: {assets}");
            return;
        }

        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assets), options);
        if (!document.RootElement.TryGetProperty("textures", out JsonElement textures))
        {
            return;
        }

        foreach (JsonProperty texture in textures.EnumerateObject())
        {
            result.ThemeTextures++;
            string path = Path.Combine(themeDirectory, texture.Value.GetString()!.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                result.ThemeTexturesMissing.Add($"{texture.Name} ({texture.Value.GetString()})");
            }
        }
    }

    private static string BuildReport(SourceFiles sources, ImportResult result)
    {
        var text = new StringBuilder();
        CultureInfo c = CultureInfo.InvariantCulture;
        text.AppendLine("# Import report: Classic Amiga theme").AppendLine();
        text.AppendLine(c, $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by tools/Arcade.Importer.").AppendLine();
        text.AppendLine("These pictures come from your own copy of the game. They are for your personal use only: do not share them and");
        text.AppendLine("never commit them (this folder is excluded by `.gitignore`).").AppendLine();
        text.AppendLine(c, $"**Theme resources installed: {result.ThemeTextures - result.ThemeTexturesMissing.Count} / {result.ThemeTextures}.**");
        foreach (string missing in result.ThemeTexturesMissing)
        {
            text.AppendLine(c, $"- missing: {missing} (the game shows a clearly flagged replacement)");
        }

        Section(text, "Sources", sources.Sources);
        text.AppendLine("| File | Origin | Bytes | SHA-256 |").AppendLine("|---|---|---|---|");
        foreach (DiskFile file in sources.Files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            text.AppendLine(c, $"| {file.Name} | {file.Origin} | {file.Data.Length} | `{Convert.ToHexStringLower(SHA256.HashData(file.Data))}` |");
        }

        Section(text, "Files written", result.Written);
        Section(text, "Not found or not imported", result.Missing);
        Section(text, "Details and assumptions to check with the contact sheets (contact/)", result.Notes);
        Section(text, "Problems", result.Problems);
        text.AppendLine().AppendLine("## What this import does not establish").AppendLine();
        text.AppendLine("- Kit colours: palette indices 3 and 4 (team primary colours) become the shirt marker, index 5 the shorts marker [Approximatif].");
        text.AppendLine("- Animations: only the running cycle (frames 0-7, sequence of Open Kick-Off) and frame 0 for standing are mapped [Reconstitué].");
        text.AppendLine("  Kicks, tackles, headers, dives and celebrations are in the sheets but their frame numbers still have to be identified.");
        text.AppendLine("- Which style sheet each team uses, the keeper and selection colours, the bitmap fonts and the score panel layout.");
        text.AppendLine("- Whether the goals are drawn in the pitch tiles (the theme still draws its own goals).");
        text.AppendLine("- Pixel-perfect fidelity: never claimed without a side-by-side comparison with the original running in an emulator.");
        return text.ToString();
    }

    private static void Section(StringBuilder text, string title, List<string> lines)
    {
        text.AppendLine().AppendLine(CultureInfo.InvariantCulture, $"## {title}").AppendLine();
        if (lines.Count == 0)
        {
            text.AppendLine("(none)");
        }

        foreach (string line in lines)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
        }
    }

    private static string Yes(bool value) => value ? "yes" : "no";
}
