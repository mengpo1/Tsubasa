namespace Arcade.Importer.Imaging;

internal readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>A palette-indexed picture, as stored by the Amiga files (one byte per pixel here).</summary>
internal sealed class IndexedImage
{
    public IndexedImage(int width, int height, byte[] pixels, IReadOnlyList<Rgb> palette, byte[]? mask = null, int? transparentIndex = null)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
        {
            throw new ArgumentException("pixel buffer does not match the size", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
        Palette = palette;
        Mask = mask;
        TransparentIndex = transparentIndex;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public IReadOnlyList<Rgb> Palette { get; }

    /// <summary>Optional mask plane (1 = opaque).</summary>
    public byte[]? Mask { get; }

    /// <summary>Palette index declared transparent by the file (ILBM masking 2), if any.</summary>
    public int? TransparentIndex { get; }

    public byte this[int x, int y] => Pixels[(y * Width) + x];

    public bool IsMasked(int x, int y) => Mask is not null && Mask[(y * Width) + x] == 0;

    public IndexedImage Crop(int x, int y, int width, int height)
    {
        var pixels = new byte[width * height];
        byte[]? mask = Mask is null ? null : new byte[width * height];
        for (int row = 0; row < height; row++)
        {
            Array.Copy(Pixels, ((y + row) * Width) + x, pixels, row * width, width);
            if (mask is not null)
            {
                Array.Copy(Mask!, ((y + row) * Width) + x, mask, row * width, width);
            }
        }

        return new IndexedImage(width, height, pixels, Palette, mask, TransparentIndex);
    }

    /// <summary>Pixel count per palette index (masked pixels excluded).</summary>
    public int[] Histogram()
    {
        var counts = new int[256];
        for (int i = 0; i < Pixels.Length; i++)
        {
            if (Mask is null || Mask[i] != 0)
            {
                counts[Pixels[i]]++;
            }
        }

        return counts;
    }

    /// <summary>True when every pixel of the rows is transparent (index <paramref name="transparent"/> or masked).</summary>
    public bool RowsEmpty(int firstRow, int rows, int transparent)
    {
        for (int y = firstRow; y < firstRow + rows; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsMasked(x, y) && this[x, y] != transparent)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <param name="colourOf">Colour of a palette index, or null for a transparent pixel.</param>
    public RgbaImage ToRgba(Func<int, Rgb?> colourOf)
    {
        ArgumentNullException.ThrowIfNull(colourOf);
        var image = new RgbaImage(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsMasked(x, y) && colourOf(this[x, y]) is Rgb c)
                {
                    image.Set(x, y, c.R, c.G, c.B, 255);
                }
            }
        }

        return image;
    }

    /// <summary>Original colours; <paramref name="transparent"/> (if any) becomes fully transparent.</summary>
    public RgbaImage ToRgba(int? transparent) =>
        ToRgba(index => index == transparent ? null : index < Palette.Count ? Palette[index] : new Rgb(255, 0, 255));
}
