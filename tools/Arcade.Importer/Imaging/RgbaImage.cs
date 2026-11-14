namespace Arcade.Importer.Imaging;

/// <summary>A 32-bit RGBA picture (straight alpha), the output of the importer.</summary>
internal sealed class RgbaImage
{
    public RgbaImage(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Data { get; }

    public void Set(int x, int y, byte r, byte g, byte b, byte a)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return;
        }

        int i = ((y * Width) + x) * 4;
        Data[i] = r;
        Data[i + 1] = g;
        Data[i + 2] = b;
        Data[i + 3] = a;
    }

    public (byte R, byte G, byte B, byte A) Get(int x, int y)
    {
        int i = ((y * Width) + x) * 4;
        return (Data[i], Data[i + 1], Data[i + 2], Data[i + 3]);
    }

    public void Fill(int x, int y, int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                Set(column, row, r, g, b, a);
            }
        }
    }

    /// <summary>Copies a region of <paramref name="source"/>, enlarged by an integer factor (nearest neighbour), over this picture.</summary>
    public void Blit(RgbaImage source, int sourceX, int sourceY, int width, int height, int x, int y, int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (int row = 0; row < height * scale; row++)
        {
            for (int column = 0; column < width * scale; column++)
            {
                (byte r, byte g, byte b, byte a) = source.Get(sourceX + (column / scale), sourceY + (row / scale));
                if (a != 0)
                {
                    Set(x + column, y + row, r, g, b, a);
                }
            }
        }
    }
}
