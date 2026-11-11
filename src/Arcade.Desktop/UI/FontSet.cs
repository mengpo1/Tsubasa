using Arcade.Core.Content;
using FontStashSharp;

namespace Arcade.Desktop.UI;

/// <summary>TrueType fonts loaded at runtime from the content packs (full Unicode, replaceable by mods).</summary>
internal sealed class FontSet : IDisposable
{
    private const string RegularId = "ui.default";
    private const string BoldId = "ui.bold";

    private readonly FontSystem _regular;
    private readonly FontSystem _bold;

    public FontSet(ContentRepository content, AssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(catalog);
        _regular = Load(content, catalog, RegularId);
        _bold = catalog.Fonts.ContainsKey(BoldId) ? Load(content, catalog, BoldId) : Load(content, catalog, RegularId);
    }

    public SpriteFontBase Regular(float size) => _regular.GetFont(MathF.Max(6f, MathF.Round(size)));

    public SpriteFontBase Bold(float size) => _bold.GetFont(MathF.Max(6f, MathF.Round(size)));

    public void Dispose()
    {
        _regular.Dispose();
        _bold.Dispose();
    }

    private static FontSystem Load(ContentRepository content, AssetCatalog catalog, string id)
    {
        if (!catalog.Fonts.TryGetValue(id, out string? path))
        {
            throw new ContentException($"Font id '{id}' is missing from {AssetCatalog.FileName}.");
        }

        var system = new FontSystem();
        system.AddFont(File.ReadAllBytes(content.Resolve(path).FullPath));
        return system;
    }
}
