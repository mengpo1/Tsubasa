namespace Arcade.Core.Content;

/// <summary>A loaded content pack: the base game data or one mod.</summary>
public sealed class ContentPack
{
    public ContentPack(ModManifest manifest, string rootDirectory, bool isBase, IReadOnlyList<string> files)
    {
        Manifest = manifest;
        RootDirectory = rootDirectory;
        IsBase = isBase;
        Files = files;
    }

    public ModManifest Manifest { get; }

    public string Id => Manifest.Id;

    public string RootDirectory { get; }

    public bool IsBase { get; }

    /// <summary>Normalised relative paths of every file of the pack (manifest excluded).</summary>
    public IReadOnlyList<string> Files { get; }

    public bool Contains(string normalizedRelativePath) => Files.Contains(normalizedRelativePath, StringComparer.Ordinal);
}
