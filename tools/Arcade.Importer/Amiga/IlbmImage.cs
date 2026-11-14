using System.Buffers.Binary;
using System.Text;
using Arcade.Importer.Imaging;

namespace Arcade.Importer.Amiga;

/// <summary>
/// Decoder for IFF ILBM pictures (EA IFF 85 / Commodore specification): BMHD header, CMAP palette, BODY of
/// interleaved bit planes (row by row, plane by plane, plus an optional mask plane), uncompressed or ByteRun1.
/// HAM and extra-half-brite modes are reported as unsupported rather than decoded wrongly.
/// </summary>
internal sealed class IlbmImage
{
    public const byte MaskNone = 0;
    public const byte MaskHasMask = 1;
    public const byte MaskTransparentColor = 2;

    private const uint CamgHam = 0x800;
    private const uint CamgExtraHalfBrite = 0x80;

    private IlbmImage(IndexedImage image, int planes, byte masking, byte compression, int transparentColor, bool paletteScaled)
    {
        Image = image;
        Planes = planes;
        Masking = masking;
        Compression = compression;
        TransparentColor = transparentColor;
        PaletteScaled = paletteScaled;
    }

    public IndexedImage Image { get; }

    public int Planes { get; }

    public byte Masking { get; }

    public byte Compression { get; }

    public int TransparentColor { get; }

    /// <summary>True when the palette held 4-bit Amiga values in the high nibbles and was expanded to 8 bits.</summary>
    public bool PaletteScaled { get; }

    public static bool IsIlbm(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && data[..4].SequenceEqual("FORM"u8) && data.Slice(8, 4).SequenceEqual("ILBM"u8);

    public static IlbmImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsIlbm(data))
        {
            throw new InvalidDataException("not an IFF ILBM picture");
        }

        int formEnd = (int)Math.Min(data.Length, 8L + BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
        int width = 0, height = 0, planes = 0, transparent = 0;
        byte masking = 0, compression = 0;
        bool header = false;
        Rgb[] palette = [];
        bool scaled = false;
        ReadOnlySpan<byte> body = default;
        uint camg = 0;
        for (int at = 12; at + 8 <= formEnd;)
        {
            string id = Encoding.ASCII.GetString(data, at, 4);
            int size = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at + 4));
            int start = at + 8;
            if (start + size > formEnd)
            {
                throw new InvalidDataException($"chunk {id} is truncated");
            }

            ReadOnlySpan<byte> chunk = data.AsSpan(start, size);
            switch (id)
            {
                case "BMHD" when size >= 20:
                    width = BinaryPrimitives.ReadUInt16BigEndian(chunk);
                    height = BinaryPrimitives.ReadUInt16BigEndian(chunk[2..]);
                    planes = chunk[8];
                    masking = chunk[9];
                    compression = chunk[10];
                    transparent = BinaryPrimitives.ReadUInt16BigEndian(chunk[12..]);
                    header = true;
                    break;
                case "CMAP":
                    (palette, scaled) = ReadPalette(chunk);
                    break;
                case "CAMG" when size >= 4:
                    camg = BinaryPrimitives.ReadUInt32BigEndian(chunk);
                    break;
                case "BODY":
                    body = chunk;
                    break;
            }

            at = start + size + (size & 1);
        }

        if (!header || width == 0 || height == 0 || planes is 0 or > 8)
        {
            throw new InvalidDataException("missing or invalid BMHD header");
        }

        if ((camg & (CamgHam | CamgExtraHalfBrite)) != 0)
        {
            throw new InvalidDataException("HAM and extra-half-brite pictures are not supported");
        }

        if (body.IsEmpty)
        {
            throw new InvalidDataException("missing BODY chunk");
        }

        if (compression > 1)
        {
            throw new InvalidDataException($"unsupported compression {compression}");
        }

        int rowBytes = ((width + 15) / 16) * 2;
        int planesInBody = planes + (masking == MaskHasMask ? 1 : 0);
        int expected = rowBytes * planesInBody * height;
        byte[] raw = compression == 1 ? UnpackByteRun1(body, expected) : body.Length >= expected ? body[..expected].ToArray() : throw new InvalidDataException("BODY is too short");
        var pixels = new byte[width * height];
        byte[]? mask = masking == MaskHasMask ? new byte[width * height] : null;
        for (int y = 0; y < height; y++)
        {
            for (int plane = 0; plane < planesInBody; plane++)
            {
                int rowStart = ((y * planesInBody) + plane) * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int bit = (raw[rowStart + (x >> 3)] >> (7 - (x & 7))) & 1;
                    if (plane < planes)
                    {
                        pixels[(y * width) + x] |= (byte)(bit << plane);
                    }
                    else
                    {
                        mask![(y * width) + x] = (byte)bit;
                    }
                }
            }
        }

        int colours = 1 << planes;
        if (palette.Length < colours)
        {
            Rgb[] padded = new Rgb[colours];
            palette.CopyTo(padded, 0);
            palette = padded;
        }

        int? transparentIndex = masking == MaskTransparentColor ? transparent : null;
        return new IlbmImage(new IndexedImage(width, height, pixels, palette, mask, transparentIndex), planes, masking, compression, transparent, scaled);
    }

    /// <summary>ByteRun1 (PackBits): n in 0..127 copies n+1 bytes, n in -127..-1 repeats the next byte 1-n times, -128 is a no-op.</summary>
    public static byte[] UnpackByteRun1(ReadOnlySpan<byte> source, int expected)
    {
        var output = new byte[expected];
        int read = 0, written = 0;
        while (written < expected)
        {
            if (read >= source.Length)
            {
                throw new InvalidDataException($"ByteRun1 data ends after {written} of {expected} bytes");
            }

            sbyte n = (sbyte)source[read++];
            if (n >= 0)
            {
                int count = Math.Min(n + 1, expected - written);
                if (read + count > source.Length)
                {
                    throw new InvalidDataException("ByteRun1 literal run is truncated");
                }

                source.Slice(read, count).CopyTo(output.AsSpan(written));
                read += n + 1;
                written += count;
            }
            else if (n != -128)
            {
                if (read >= source.Length)
                {
                    throw new InvalidDataException("ByteRun1 repeat run is truncated");
                }

                int count = Math.Min(1 - n, expected - written);
                output.AsSpan(written, count).Fill(source[read++]);
                written += count;
            }
        }

        return output;
    }

    private static (Rgb[] Palette, bool Scaled) ReadPalette(ReadOnlySpan<byte> chunk)
    {
        var palette = new Rgb[chunk.Length / 3];
        bool fourBit = true;
        bool anyColour = false;
        for (int i = 0; i < palette.Length; i++)
        {
            palette[i] = new Rgb(chunk[i * 3], chunk[(i * 3) + 1], chunk[(i * 3) + 2]);
            fourBit &= (chunk[i * 3] & 0x0F) == 0 && (chunk[(i * 3) + 1] & 0x0F) == 0 && (chunk[(i * 3) + 2] & 0x0F) == 0;
            anyColour |= chunk[i * 3] != 0 || chunk[(i * 3) + 1] != 0 || chunk[(i * 3) + 2] != 0;
        }

        if (!fourBit || !anyColour)
        {
            return (palette, false);
        }

        // Amiga OCS colours are 4 bits per component, often stored as 0xN0: expand to the full 0..255 range.
        for (int i = 0; i < palette.Length; i++)
        {
            Rgb c = palette[i];
            palette[i] = new Rgb((byte)(c.R | (c.R >> 4)), (byte)(c.G | (c.G >> 4)), (byte)(c.B | (c.B >> 4)));
        }

        return (palette, true);
    }
}
