using System.Buffers.Binary;
using Arcade.Importer.Imaging;

namespace Arcade.Importer.KickOff2;

/// <summary>
/// Builds a whole pitch picture from a tile map (<c>pdat.*</c>: big-endian 16-bit width and height in tiles, then one
/// byte per tile, row by row) and a 16x16 tile set (<c>fbgraph4.*</c>, an ILBM picture read row by row, left to right).
/// For Kick Off 2 the map is 80x96 tiles, i.e. a 1280x1536 picture.
/// </summary>
internal static class PitchComposer
{
    public const int TileSize = 16;

    public static IndexedImage Compose(byte[] map, IndexedImage tiles, List<string> problems, string name)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(problems);
        if (map.Length < 4)
        {
            throw new InvalidDataException("tile map too short");
        }

        int width = BinaryPrimitives.ReadUInt16BigEndian(map);
        int height = BinaryPrimitives.ReadUInt16BigEndian(map.AsSpan(2));
        if (width == 0 || height == 0 || map.Length < 4 + (width * height))
        {
            throw new InvalidDataException($"tile map header {width}x{height} does not match its {map.Length} bytes");
        }

        if (map.Length != 4 + (width * height))
        {
            problems.Add($"{name}: {map.Length - 4 - (width * height)} unexpected bytes after the tile map (ignored)");
        }

        int columns = tiles.Width / TileSize;
        int tileCount = columns * (tiles.Height / TileSize);
        var pixels = new byte[width * TileSize * height * TileSize];
        int invalid = 0;
        for (int ty = 0; ty < height; ty++)
        {
            for (int tx = 0; tx < width; tx++)
            {
                int tile = map[4 + (ty * width) + tx];
                if (tile >= tileCount)
                {
                    invalid++;
                    continue;
                }

                int sx = tile % columns * TileSize;
                int sy = tile / columns * TileSize;
                for (int row = 0; row < TileSize; row++)
                {
                    Array.Copy(tiles.Pixels, ((sy + row) * tiles.Width) + sx, pixels, (((ty * TileSize) + row) * width * TileSize) + (tx * TileSize), TileSize);
                }
            }
        }

        if (invalid > 0)
        {
            problems.Add($"{name}: {invalid} map cells refer to tiles beyond the {tileCount} of the tile set (left in colour 0)");
        }

        return new IndexedImage(width * TileSize, height * TileSize, pixels, tiles.Palette);
    }
}
