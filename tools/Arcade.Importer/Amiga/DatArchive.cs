using System.Buffers.Binary;

namespace Arcade.Importer.Amiga;

/// <summary>
/// The <c>.dat</c> archives of Kick Off 2 (<c>sprites.dat</c>, <c>pitches.dat</c>...): a big-endian 32-bit length
/// <c>n</c> of the table, then <c>n / 16</c> entries of a 12-byte name and a 32-bit offset relative to the end of the
/// table (byte <c>4 + n</c>). A member ends where the next one starts (the last one at the end of the file).
/// </summary>
internal static class DatArchive
{
    public const int EntrySize = 16;

    public static bool LooksLikeArchive(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 + EntrySize)
        {
            return false;
        }

        int table = BinaryPrimitives.ReadInt32BigEndian(data);
        return table > 0 && table % EntrySize == 0 && 4 + table <= data.Length && DdfsDisk.ReadName(data.Slice(4, 12)) is not null;
    }

    public static IReadOnlyList<DiskFile> Read(byte[] data, string origin, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(problems);
        if (!LooksLikeArchive(data))
        {
            problems.Add($"{origin}: not a Kick Off 2 archive");
            return [];
        }

        int table = BinaryPrimitives.ReadInt32BigEndian(data);
        int bodyStart = 4 + table;
        var entries = new List<(string Name, int Start)>();
        for (int at = 4; at < bodyStart; at += EntrySize)
        {
            string? name = DdfsDisk.ReadName(data.AsSpan(at, 12));
            long start = bodyStart + (long)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at + 12, 4));
            if (name is null || start > data.Length)
            {
                problems.Add($"{origin}: entry {(at - 4) / EntrySize} is invalid; skipped");
                continue;
            }

            entries.Add((name, (int)start));
        }

        List<(string Name, int Start)> byOffset = [.. entries.OrderBy(entry => entry.Start)];
        var files = new List<DiskFile>(byOffset.Count);
        for (int i = 0; i < byOffset.Count; i++)
        {
            int end = i + 1 < byOffset.Count ? byOffset[i + 1].Start : data.Length;
            files.Add(new DiskFile(byOffset[i].Name, data.AsSpan(byOffset[i].Start, end - byOffset[i].Start).ToArray(), origin));
        }

        return files;
    }
}
