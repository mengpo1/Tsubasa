namespace Arcade.Importer.Imaging;

/// <summary>
/// Enlarged view of a sheet of tiles with each tile's index written on it, so that animations can be mapped by
/// looking at one's own imported files (frame numbers of <c>assets.json</c> are these indices).
/// </summary>
internal static class ContactSheet
{
    private const int Gap = 1;

    // 3x5 digits, one row of 3 bits per entry (most significant bit on the left).
    private static readonly byte[][] Digits =
    [
        [7, 5, 5, 5, 7], [2, 6, 2, 2, 7], [7, 1, 7, 4, 7], [7, 1, 7, 1, 7], [5, 5, 7, 1, 1],
        [7, 4, 7, 1, 7], [7, 4, 7, 5, 7], [7, 1, 1, 1, 1], [7, 5, 7, 5, 7], [7, 5, 7, 1, 7],
    ];

    public static RgbaImage Build(RgbaImage sheet, int tileWidth, int tileHeight, int scale)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        int columns = sheet.Width / tileWidth;
        int rows = sheet.Height / tileHeight;
        int cellWidth = (tileWidth * scale) + Gap;
        int cellHeight = (tileHeight * scale) + Gap;
        var output = new RgbaImage((columns * cellWidth) + Gap, (rows * cellHeight) + Gap);
        output.Fill(0, 0, output.Width, output.Height, 40, 40, 48);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int x = Gap + (column * cellWidth);
                int y = Gap + (row * cellHeight);
                Checker(output, x, y, tileWidth * scale, tileHeight * scale);
                output.Blit(sheet, column * tileWidth, row * tileHeight, tileWidth, tileHeight, x, y, scale);
                Label(output, (row * columns) + column, x + 1, y + 1);
            }
        }

        return output;
    }

    /// <summary>Writes a number with a dark background box at the given position.</summary>
    public static void Label(RgbaImage image, int value, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(image);
        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        image.Fill(x, y, (text.Length * 4) + 1, 7, 0, 0, 0, 200);
        for (int i = 0; i < text.Length; i++)
        {
            byte[] glyph = Digits[text[i] - '0'];
            for (int row = 0; row < 5; row++)
            {
                for (int bit = 0; bit < 3; bit++)
                {
                    if ((glyph[row] & (4 >> bit)) != 0)
                    {
                        image.Set(x + 1 + (i * 4) + bit, y + 1 + row, 255, 240, 80, 255);
                    }
                }
            }
        }
    }

    private static void Checker(RgbaImage image, int x, int y, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                byte shade = ((column / 4) + (row / 4)) % 2 == 0 ? (byte)96 : (byte)112;
                image.Set(x + column, y + row, shade, shade, shade, 255);
            }
        }
    }
}
