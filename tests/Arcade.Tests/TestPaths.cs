using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;

namespace Arcade.Tests;

/// <summary>Locates the repository so that tests run against the real game data.</summary>
internal static class TestPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);
    private static readonly Lazy<FootballContent> BaseContentLazy = new(() => FootballContent.Load(LoadRepository().Repository));

    public static string RepositoryRoot => RootLazy.Value;

    /// <summary>Football data from the base content only (no mods), shared by tests (read-only).</summary>
    public static FootballContent BaseContent => BaseContentLazy.Value;

    public static ContentLoadResult LoadRepository(params string[] mods) =>
        ContentLoader.Load(new ContentLoadOptions(RepositoryRoot, mods), new GameLog());

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tsubasa.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Tsubasa.slnx) not found.");
    }
}

/// <summary>A temporary game root with its own content/ and mods/ folders, deleted after the test.</summary>
internal sealed class TempGameRoot : IDisposable
{
    public TempGameRoot()
    {
        Root = Path.Combine(Path.GetTempPath(), "arcade-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        WriteFile("content/manifest.json", """{ "id": "base", "name": "Base", "version": "1", "formatVersion": 1 }""");
    }

    public string Root { get; }

    public void WriteFile(string relativePath, string text)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void WriteMod(string id, int formatVersion = 1, string? manifestId = null) =>
        WriteFile($"mods/{id}/manifest.json", $$"""{ "id": "{{manifestId ?? id}}", "name": "Mod {{id}}", "version": "1.0", "formatVersion": {{formatVersion}} }""");

    public ContentLoadResult Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions(Root, mods), new GameLog());

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary folder: leaving it behind is harmless.
        }
    }
}
