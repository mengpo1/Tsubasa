namespace Arcade.Core.Content;

/// <summary>Role of a pack in the layered content, in load order.</summary>
public enum PackKind
{
    /// <summary>The game data (<c>content/</c>).</summary>
    Base,

    /// <summary>A visual theme (<c>content/themes/&lt;id&gt;/</c>): textures, animations, fonts, display only.</summary>
    Theme,

    /// <summary>A mod (<c>mods/&lt;id&gt;/</c>).</summary>
    Mod,
}

/// <summary>A loaded content pack: the base game data, a theme or one mod.</summary>
public sealed class ContentPack
{
    public ContentPack(ModManifest manifest, string rootDirectory, PackKind kind, IReadOnlyList<string> files)
    {
        Manifest = manifest;
        RootDirectory = rootDirectory;
        Kind = kind;
        Files = files;
    }

    public PackKind Kind { get; }

    public ModManifest Manifest { get; }

    public string Id => Manifest.Id;

    public string RootDirectory { get; }

    public bool IsBase => Kind == PackKind.Base;

    /// <summary>Normalised relative paths of every file of the pack (manifest excluded).</summary>
    public IReadOnlyList<string> Files { get; }

    public bool Contains(string normalizedRelativePath) => Files.Contains(normalizedRelativePath, StringComparer.Ordinal);
}
