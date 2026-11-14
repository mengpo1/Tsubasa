using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Arcade.Tests.Importer;

/// <summary>
/// Builders of synthetic Amiga files (IFF ILBM, DDFS disk images, Kick Off 2 archives and tile maps). The importer is
/// only ever tested with these: no original game data is used or needed.
/// </summary>
internal static class AmigaFixtures
{
    public static byte[] Ilbm(int width, int height, int planes, Func<int, int, int> pixel, (byte R, byte G, byte B)[] palette,
        bool compress = true, byte masking = 0, Func<int, int, bool>? opaque = null, int transparent = 0)
    {
        int rowBytes = ((width + 15) / 16) * 2;
        int planesInBody = planes + (masking == 1 ? 1 : 0);
        using var body = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            for (int plane = 0; plane < planesInBody; plane++)
            {
                var row = new byte[rowBytes];
                for (int x = 0; x < width; x++)
                {
                    bool set = plane < planes ? ((pixel(x, y) >> plane) & 1) != 0 : opaque?.Invoke(x, y) ?? true;
                    if (set)
                    {
                        row[x >> 3] |= (byte)(0x80 >> (x & 7));
                    }
                }

                body.Write(compress ? PackBits(row) : row);
            }
        }

        var header = new byte[20];
        BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), (ushort)height);
        header[8] = (byte)planes;
        header[9] = masking;
        header[10] = compress ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(12), (ushort)transparent);
        header[14] = 10;
        header[15] = 11;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(16), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(18), (ushort)height);
        byte[] cmap = [.. palette.SelectMany(c => new[] { c.R, c.G, c.B })];

        using var form = new MemoryStream();
        form.Write("ILBM"u8);
        Chunk(form, "BMHD", header);
        Chunk(form, "CMAP", cmap);
        Chunk(form, "BODY", body.ToArray());
        using var file = new MemoryStream();
        file.Write("FORM"u8);
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, (int)form.Length);
        file.Write(size);
        file.Write(form.ToArray());
        return file.ToArray();
    }

    /// <summary>ByteRun1 encoder: runs of 3+ equal bytes are repeated, the rest written literally.</summary>
    public static byte[] PackBits(byte[] data)
    {
        var output = new List<byte>();
        int i = 0;
        while (i < data.Length)
        {
            int run = 1;
            while (i + run < data.Length && data[i + run] == data[i] && run < 128)
            {
                run++;
            }

            if (run >= 3)
            {
                output.Add((byte)(sbyte)(1 - run));
                output.Add(data[i]);
                i += run;
                continue;
            }

            int start = i;
            while (i < data.Length && i - start < 128 && !(i + 2 < data.Length && data[i] == data[i + 1] && data[i] == data[i + 2]))
            {
                i++;
            }

            output.Add((byte)(i - start - 1));
            output.AddRange(data[start..i]);
        }

        return [.. output];
    }

    /// <summary>A 880 KB disk image with a Dino Dini file table at 0x400 and the files from block 8 on.</summary>
    public static byte[] Ddfs(params (string Name, byte[] Data)[] files)
    {
        var image = new byte[901120];
        int block = 8;
        for (int i = 0; i < files.Length; i++)
        {
            int entry = 0x400 + (i * 16);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(image, entry);
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(entry + 12), ((uint)block << 20) | (uint)files[i].Data.Length);
            files[i].Data.CopyTo(image, block * 512);
            block += (files[i].Data.Length + 511) / 512;
        }

        return image;
    }

    /// <summary>A .dat archive; the table lists the members in reverse order to check that offsets, not order, delimit them.</summary>
    public static byte[] Archive(params (string Name, byte[] Data)[] members)
    {
        int table = members.Length * 16;
        var data = new List<byte>();
        var offsets = new int[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            offsets[i] = data.Count;
            data.AddRange(members[i].Data);
        }

        var output = new byte[4 + table + data.Count];
        BinaryPrimitives.WriteInt32BigEndian(output, table);
        for (int slot = 0; slot < members.Length; slot++)
        {
            int i = members.Length - 1 - slot;
            Encoding.ASCII.GetBytes(members[i].Name).CopyTo(output, 4 + (slot * 16));
            BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(4 + (slot * 16) + 12), offsets[i]);
        }

        data.CopyTo(output, 4 + table);
        return output;
    }

    public static byte[] TileMap(int width, int height, Func<int, int, byte> tile)
    {
        var map = new byte[4 + (width * height)];
        BinaryPrimitives.WriteUInt16BigEndian(map, (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan(2), (ushort)height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[4 + (y * width) + x] = tile(x, y);
            }
        }

        return map;
    }

    public static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    private static void Chunk(Stream stream, string id, byte[] data)
    {
        stream.Write(Encoding.ASCII.GetBytes(id));
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, data.Length);
        stream.Write(size);
        stream.Write(data);
        if ((data.Length & 1) != 0)
        {
            stream.WriteByte(0);
        }
    }
}

/// <summary>Reads back the PNG files written by the importer (8-bit RGBA, filter 0 only).</summary>
internal static class PngReader
{
    public static (int Width, int Height, byte[] Rgba) Read(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        int width = 0, height = 0;
        using var idat = new MemoryStream();
        for (int at = 8; at < file.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at));
            string type = Encoding.ASCII.GetString(file, at + 4, 4);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at + 8));
                height = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at + 12));
            }
            else if (type == "IDAT")
            {
                idat.Write(file, at + 8, length);
            }

            at += 12 + length;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        byte[] rows = raw.ToArray();
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(0, rows[y * ((width * 4) + 1)]);
            Array.Copy(rows, (y * ((width * 4) + 1)) + 1, rgba, y * width * 4, width * 4);
        }

        return (width, height, rgba);
    }

    public static (byte R, byte G, byte B, byte A) Pixel((int Width, int Height, byte[] Rgba) image, int x, int y)
    {
        int i = ((y * image.Width) + x) * 4;
        return (image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
    }
}
