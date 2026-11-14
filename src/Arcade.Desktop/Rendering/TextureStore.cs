using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football.Teams;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Rendering;

/// <summary>
/// Resolves logical texture ids (see <c>assets.json</c>) to GPU textures through the layered content.
/// When a theme is active, a texture it does not provide (e.g. not imported yet) is taken from the default look
/// (<see cref="Fallback"/>); a texture missing everywhere is replaced by a visible magenta checkerboard, never by a
/// silent blank. Every replacement is logged once. Team kits are produced by palette swap of marker colours and cached per kit.
/// </summary>
internal sealed class TextureStore : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly ContentRepository _content;
    private readonly AssetCatalog _catalog;
    private readonly GameLog _log;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _redirected = new(StringComparer.Ordinal);
    private readonly Texture2D _fallback;

    /// <param name="fallback">Store of the default look when a theme is active (owned and disposed by this store).</param>
    public TextureStore(GraphicsDevice device, ContentRepository content, AssetCatalog catalog, GameLog log, TextureStore? fallback = null)
    {
        _device = device;
        _content = content;
        _catalog = catalog;
        _log = log;
        Fallback = fallback;
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
        _fallback = CreateFallback(device);
    }

    /// <summary>Default look used for what the active theme does not provide, or null without theme.</summary>
    public TextureStore? Fallback { get; }

    /// <summary>Magenta checkerboard shown for a missing resource.</summary>
    public Texture2D Placeholder => _fallback;

    /// <summary>1x1 white texture for lines and rectangles.</summary>
    public Texture2D Pixel { get; }

    public AssetCatalog Catalog => _catalog;

    /// <summary>True when the id is in the catalog and its file exists (no replacement needed).</summary>
    public bool IsAvailable(string id) => _catalog.Textures.TryGetValue(id, out string? path) && _content.Exists(path);

    /// <summary>Texture ids of the catalog whose file is missing.</summary>
    public IReadOnlyList<string> MissingTextures() =>
        [.. _catalog.Textures.Keys.Where(id => !IsAvailable(id)).Order(StringComparer.Ordinal)];

    /// <summary>The store to draw an animation from: the default look for animations resolved there.</summary>
    public TextureStore ForLook(bool defaultLook) => defaultLook && Fallback is not null ? Fallback : this;

    public Texture2D Get(string id)
    {
        if (_textures.TryGetValue(id, out Texture2D? texture))
        {
            return texture;
        }

        if (Redirect(id) is TextureStore other)
        {
            return other.Get(id);
        }

        texture = Load(id, colorProcessor: null) ?? _fallback;
        _textures[id] = texture;
        return texture;
    }

    /// <summary>Texture recoloured for a team kit according to the animation palette.</summary>
    public Texture2D GetKit(string id, IReadOnlyList<PaletteSwap> palette, KitDefinition kit)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(kit);
        if (palette.Count == 0)
        {
            return Get(id);
        }

        if (Redirect(id) is TextureStore other)
        {
            return other.GetKit(id, palette, kit);
        }

        string key = $"{id}|{kit.Shirt}|{kit.Shorts}|{kit.Socks}";
        if (_textures.TryGetValue(key, out Texture2D? texture))
        {
            return texture;
        }

        var swaps = new Dictionary<int, ColorRgb>();
        foreach (PaletteSwap swap in palette)
        {
            ColorRgb marker = ColorRgb.Parse(swap.Marker);
            swaps[Pack(marker.R, marker.G, marker.B)] = kit.ColorFor(swap.Slot).Scale(swap.Brightness);
        }

        texture = Load(id, bytes => Recolor(bytes, swaps)) ?? _fallback;
        _textures[key] = texture;
        return texture;
    }

    public void Dispose()
    {
        foreach (Texture2D texture in _textures.Values.Distinct())
        {
            if (!ReferenceEquals(texture, _fallback))
            {
                texture.Dispose();
            }
        }

        _textures.Clear();
        _fallback.Dispose();
        Pixel.Dispose();
        Fallback?.Dispose();
    }

    /// <summary>The default-look store when this (theme) store cannot provide the id but the default look can.</summary>
    private TextureStore? Redirect(string id)
    {
        if (Fallback is null)
        {
            return null;
        }

        if (_redirected.Contains(id))
        {
            return Fallback;
        }

        if (IsAvailable(id) || !Fallback.IsAvailable(id))
        {
            return null;
        }

        _redirected.Add(id);
        _log.Warn("assets", "Theme texture missing, using the default look", ("id", id));
        return Fallback;
    }

    private Texture2D? Load(string id, Action<byte[]>? colorProcessor)
    {
        if (!_catalog.Textures.TryGetValue(id, out string? path))
        {
            _log.Warn("assets", "Unknown texture id, using placeholder", ("id", id));
            return null;
        }

        if (!_content.TryResolve(path, out ResolvedFile file))
        {
            _log.Warn("assets", "Texture file not found, using placeholder", ("id", id), ("path", path));
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(file.FullPath);
            Texture2D texture = Texture2D.FromStream(_device, stream, bytes =>
            {
                colorProcessor?.Invoke(bytes);
                DefaultColorProcessors.PremultiplyAlpha(bytes);
            });
            _log.Debug("assets", "Texture loaded", ("id", id), ("pack", file.Pack.Id), ("path", path));
            return texture;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            _log.Error("assets", "Texture could not be decoded, using placeholder", ("id", id), ("path", file.FullPath), ("error", exception.Message));
            return null;
        }
    }

    private static void Recolor(byte[] rgba, Dictionary<int, ColorRgb> swaps)
    {
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] != 0 && swaps.TryGetValue(Pack(rgba[i], rgba[i + 1], rgba[i + 2]), out ColorRgb color))
            {
                rgba[i] = color.R;
                rgba[i + 1] = color.G;
                rgba[i + 2] = color.B;
            }
        }
    }

    private static int Pack(byte r, byte g, byte b) => (r << 16) | (g << 8) | b;

    private static Texture2D CreateFallback(GraphicsDevice device)
    {
        const int size = 8;
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                data[(y * size) + x] = ((x / 4) + (y / 4)) % 2 == 0 ? Color.Magenta : Color.Black;
            }
        }

        texture.SetData(data);
        return texture;
    }
}
