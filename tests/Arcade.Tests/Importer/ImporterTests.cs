using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Desktop.Hosting;
using Arcade.Importer.Amiga;
using Arcade.Importer.Imaging;
using Arcade.Importer.KickOff2;

namespace Arcade.Tests.Importer;

/// <summary>The importer of the classic-amiga theme, tested on synthetic files only (no original data).</summary>
public sealed class ImporterTests
{
    private static readonly (byte, byte, byte)[] AmigaPalette =
    [
        (0x00, 0x00, 0x00), (0xA0, 0x50, 0x00), (0xF0, 0xC0, 0x90), (0xF0, 0x00, 0x00),
        (0x00, 0x00, 0xF0), (0xF0, 0xF0, 0xF0), (0x00, 0xA0, 0x00), (0x10, 0x30, 0x10),
    ];

    [Fact]
    public void ByteRun1_decodes_the_reference_example()
    {
        byte[] packed = [0xFE, 0xAA, 0x02, 0x80, 0x00, 0x2A, 0xFD, 0xAA, 0x03, 0x80, 0x00, 0x2A, 0x22, 0xF7, 0xAA];
        byte[] expected = [0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0x22, .. Enumerable.Repeat((byte)0xAA, 10)];

        Assert.Equal(expected, IlbmImage.UnpackByteRun1(packed, expected.Length));
        Assert.Throws<InvalidDataException>(() => IlbmImage.UnpackByteRun1(packed.AsSpan(0, 5), expected.Length));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ilbm_planes_palette_and_mask_are_decoded(bool compress)
    {
        static int Pixel(int x, int y) => (x + (2 * y)) % 8;
        byte[] file = AmigaFixtures.Ilbm(21, 5, 3, Pixel, AmigaPalette, compress, masking: IlbmImage.MaskHasMask, opaque: (x, _) => x != 4);

        IlbmImage ilbm = IlbmImage.Decode(file);

        Assert.Equal((21, 5, 3), (ilbm.Image.Width, ilbm.Image.Height, ilbm.Planes));
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 21; x++)
            {
                Assert.Equal(Pixel(x, y), ilbm.Image[x, y]);
                Assert.Equal(x == 4, ilbm.Image.IsMasked(x, y));
            }
        }

        Assert.True(ilbm.PaletteScaled);
        Assert.Equal(new Rgb(0xAA, 0x55, 0x00), ilbm.Image.Palette[1]);
        Assert.Equal(new Rgb(0xFF, 0xFF, 0xFF), ilbm.Image.Palette[5]);
    }

    [Fact]
    public void Ilbm_with_full_8_bit_palette_is_left_unchanged_and_non_ilbm_is_rejected()
    {
        byte[] file = AmigaFixtures.Ilbm(16, 1, 1, (_, _) => 1, [(0x12, 0x34, 0x56), (0xFE, 0xDC, 0xBA)], masking: IlbmImage.MaskTransparentColor);

        IlbmImage ilbm = IlbmImage.Decode(file);

        Assert.False(ilbm.PaletteScaled);
        Assert.Equal(new Rgb(0xFE, 0xDC, 0xBA), ilbm.Image.Palette[1]);
        Assert.Equal(0, ilbm.Image.TransparentIndex);
        Assert.Throws<InvalidDataException>(() => IlbmImage.Decode(new byte[64]));
    }

    [Fact]
    public void Ddfs_directory_and_archives_are_read()
    {
        byte[] archive = AmigaFixtures.Archive(("first.iff", [1, 2, 3]), ("second", [4]), ("third.x", [5, 6]));
        byte[] disk = AmigaFixtures.Ddfs(("sprites.dat", archive), ("title", [9, 9, 9, 9]));
        var problems = new List<string>();

        IReadOnlyList<DiskFile> files = DdfsDisk.Read(disk, "test.adf", problems);
        IReadOnlyList<DiskFile> members = DatArchive.Read(files[0].Data, "sprites.dat", problems);

        Assert.Empty(problems);
        Assert.Equal(["sprites.dat", "title"], files.Select(f => f.Name));
        Assert.Equal([9, 9, 9, 9], files[1].Data);
        Assert.Equal(["first.iff", "second", "third.x"], members.Select(m => m.Name));
        Assert.Equal([[1, 2, 3], [4], [5, 6]], members.Select(m => m.Data));
    }

    [Fact]
    public void A_disk_without_dino_dini_table_is_reported_precisely()
    {
        var problems = new List<string>();

        Assert.Empty(DdfsDisk.Read(new byte[901120], "other.adf", problems));
        Assert.Contains(problems, p => p.Contains("--files", StringComparison.Ordinal));
    }

    [Fact]
    public void Pitch_is_composed_from_tiles_and_bad_cells_are_reported()
    {
        // Tile set 32x32: four 16x16 tiles of colours 0..3.
        IlbmImage tiles = IlbmImage.Decode(AmigaFixtures.Ilbm(32, 32, 2, (x, y) => (x / 16) + (2 * (y / 16)), AmigaPalette));
        byte[] map = AmigaFixtures.TileMap(3, 2, (x, y) => (byte)(y == 1 && x == 2 ? 200 : x + y));
        var problems = new List<string>();

        IndexedImage pitch = PitchComposer.Compose(map, tiles.Image, problems, "test");

        Assert.Equal((48, 32), (pitch.Width, pitch.Height));
        Assert.Equal(0, pitch[0, 0]);
        Assert.Equal(2, pitch[32, 0]);
        Assert.Equal(2, pitch[31, 31]);
        Assert.Equal(0, pitch[47, 31]); // invalid cell left in colour 0
        Assert.Contains(problems, p => p.Contains("1 map cells", StringComparison.Ordinal));
    }

    [Fact]
    public void Png_output_is_valid()
    {
        Assert.Equal(0xAE426082u, PngWriter.Crc32("IEND"u8, []));
        var image = new RgbaImage(3, 2);
        image.Set(2, 1, 10, 20, 30, 255);
        string path = Path.Combine(Path.GetTempPath(), $"arcade-png-{Guid.NewGuid():N}.png");
        try
        {
            PngWriter.Save(image, path);
            var read = PngReader.Read(path);
            Assert.Equal((3, 2), (read.Width, read.Height));
            Assert.Equal((10, 20, 30, 255), PngReader.Pixel(read, 2, 1));
            Assert.Equal((0, 0, 0, 0), PngReader.Pixel(read, 0, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Synthetic_disks_are_imported_into_a_working_classic_theme()
    {
        using var root = new TempGameRoot();
        CopyDirectory(Path.Combine(TestPaths.RepositoryRoot, "content"), Path.Combine(root.Root, "content"));
        string theme = Path.Combine(root.Root, "content", "themes", "classic-amiga");
        string[] before = ThemeFilesOutsideImport(theme);
        string kickOff2 = Path.Combine(root.Root, "ko2.adf");
        string finalWhistle = Path.Combine(root.Root, "fw.adz");
        File.WriteAllBytes(kickOff2, AmigaFixtures.Ddfs(
            ("sprites.dat", AmigaFixtures.Archive(
                ("style1a.iff", Sheet(320, 161)),
                ("keeper.iff", Sheet(320, 177)),
                ("ball.iff", Sheet(320, 17)))),
            ("pitches.dat", AmigaFixtures.Archive(
                ("pdat.nor", AmigaFixtures.TileMap(80, 96, (x, y) => (byte)((x + y) % 5))),
                ("fbgraph4.nor", AmigaFixtures.Ilbm(320, 240, 3, (x, y) => ((x / 16) + (y / 16 * 20)) % 8, AmigaPalette)))),
            ("title", AmigaFixtures.Ilbm(320, 256, 5, (x, y) => (x + y) % 32, [.. Enumerable.Range(0, 32).Select(i => ((byte)(i * 8), (byte)0, (byte)0))]))));
        File.WriteAllBytes(finalWhistle, AmigaFixtures.Gzip(AmigaFixtures.Ddfs(
            ("pitches.dat", AmigaFixtures.Archive(("fbgraph4.SNO", AmigaFixtures.Ilbm(320, 208, 3, (_, _) => 5, AmigaPalette)))))));

        var sources = new SourceFiles();
        sources.AddDiskImage(kickOff2);
        sources.AddDiskImage(finalWhistle);
        ImportResult result = ClassicImporter.Run(sources, theme);

        Assert.Equal(10, result.ThemeTextures);
        Assert.Equal(5, result.ThemeTextures - result.ThemeTexturesMissing.Count);
        Assert.Contains(result.Missing, m => m.StartsWith("style1b.iff", StringComparison.Ordinal));
        Assert.Contains(result.Missing, m => m.StartsWith("pitch 'wet'", StringComparison.Ordinal));
        Assert.Contains("fonts.dat", result.Missing);
        Assert.Contains(result.Notes, n => n.StartsWith("style1a.iff: 320x161 is not a whole number", StringComparison.Ordinal));

        var players = PngReader.Read(Path.Combine(theme, "imported", "sprites", "players-style1a.png"));
        Assert.Equal((320, 160), (players.Width, players.Height));
        Assert.Equal((255, 0, 0, 255), PngReader.Pixel(players, 0, 0)); // index 3 -> shirt marker
        Assert.Equal((0, 0, 255, 255), PngReader.Pixel(players, 1, 0)); // index 5 -> shorts marker
        Assert.Equal((0xAA, 0x55, 0x00, 255), PngReader.Pixel(players, 2, 0)); // hair, original colour (4-bit expanded)
        Assert.Equal(0, PngReader.Pixel(players, 8, 8).A); // index 0 -> transparent
        var keeper = PngReader.Read(Path.Combine(theme, "imported", "sprites", "keeper.png"));
        Assert.Equal((0xFF, 0x00, 0x00, 255), PngReader.Pixel(keeper, 0, 0)); // original colours, no kit marker
        var pitch = PngReader.Read(Path.Combine(theme, "imported", "pitches", "normal.png"));
        Assert.Equal((1280, 1536), (pitch.Width, pitch.Height));
        Assert.Equal((0x00, 0x00, 0xFF, 255), PngReader.Pixel(pitch, 16 * 2, (16 * 2) + 3)); // map cell (2,2) -> tile 4 -> colour 4
        Assert.True(File.Exists(Path.Combine(theme, "imported", "pitches", "icy.png")));
        string report = File.ReadAllText(result.ReportPath);
        Assert.Contains("Theme resources installed: 5 / 10", report, StringComparison.Ordinal);
        Assert.Contains("never commit", report, StringComparison.Ordinal);
        Assert.Equal(before, ThemeFilesOutsideImport(theme));

        // The game now uses the imported frames and pitch picture for this look.
        GameData data = GameData.Load(root.Root, [], "classic-amiga", "en", new GameLog());
        Assert.Empty(data.Issues);
        AnimationStateMap map = AnimationStateMap.Resolve(data.Assets, id => data.Repository.Exists(data.Assets.Textures[id]), data.DefaultAssets, _ => true);
        Assert.Equal("classic.run", map.Get("run")!.AnimationId);
        Assert.False(map.Get("run")!.FromDefaultLook);
        Assert.Equal("classic.ball.low", map.Get("ball")!.AnimationId);
        Assert.True(data.Repository.Exists(data.Assets.Textures["pitch.bitmap.normal"]));
    }

    /// <summary>A sprite sheet whose 16x16 tiles start with colours 3, 5, 1 on the first line; the extra last line is empty.</summary>
    private static byte[] Sheet(int width, int height) =>
        AmigaFixtures.Ilbm(width, height, 3, (x, y) => y == height - 1 ? 0 : (x % 16, y % 16) switch
        {
            (0, 0) => 3,
            (1, 0) => 5,
            (2, 0) => 1,
            (_, 15) => 7,
            _ => 0,
        }, AmigaPalette, masking: IlbmImage.MaskTransparentColor);

    private static string[] ThemeFilesOutsideImport(string theme) =>
        [.. Directory.EnumerateFiles(theme, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(theme, f))
            .Where(f => !f.StartsWith("imported", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(f => f + ":" + File.ReadAllText(Path.Combine(theme, f)).Length)];

    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (relative.Contains("imported", StringComparison.Ordinal))
            {
                continue;
            }

            string destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
