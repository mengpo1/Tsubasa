using System.Buffers.Binary;

namespace Arcade.Importer.Amiga;

/// <summary>
/// Reader for the "Dino Dini File System" used by the Kick Off 2 disks (format documented by the Kick Off
/// Association wiki, checked against the structure of the disks): after the boot blocks, at byte 0x400, a table of
/// 16-byte entries, each a 12-byte zero-padded name followed by a big-endian 32-bit word whose 12 high bits are the
/// start block (512 bytes) and 20 low bits the size in bytes. The table ends at the first empty name.
/// </summary>
internal static class DdfsDisk
{
    public const int DirectoryOffset = 0x400;
    public const int BlockSize = 512;
    public const int EntrySize = 16;
    public const int NameLength = 12;

    /// <summary>Size of a standard double-density Amiga disk image.</summary>
    public const int StandardImageSize = 901120;

    private const int MaxEntries = 1024;

    public static IReadOnlyList<DiskFile> Read(byte[] image, string origin, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(problems);
        var files = new List<DiskFile>();
        if (image.Length != StandardImageSize)
        {
            problems.Add($"{origin}: {image.Length} bytes, a standard ADF has {StandardImageSize}; reading it anyway");
        }

        for (int at = DirectoryOffset, count = 0; at + EntrySize <= image.Length && count < MaxEntries; at += EntrySize, count++)
        {
            if (image[at] == 0)
            {
                break;
            }

            string? name = ReadName(image.AsSpan(at, NameLength));
            if (name is null)
            {
                problems.Add($"{origin}: entry {count} at 0x{at:X} is not a valid DDFS name; directory reading stopped");
                break;
            }

            uint word = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(at + NameLength, 4));
            long offset = (long)(word >> 20) * BlockSize;
            int size = (int)(word & 0xFFFFF);
            if (offset < DirectoryOffset + EntrySize || offset + size > image.Length)
            {
                problems.Add($"{origin}: '{name}' points outside the disk (block {word >> 20}, {size} bytes); skipped");
                continue;
            }

            files.Add(new DiskFile(name, image.AsSpan((int)offset, size).ToArray(), origin));
        }

        if (files.Count == 0)
        {
            problems.Add($"{origin}: no Dino Dini file table at offset 0x400. This is not an original Kick Off 2 disk layout "
                + "(it may be another version or a different file system). Extract the files with an Amiga tool and use --files.");
        }

        return files;
    }

    /// <summary>Printable ASCII, zero-padded; null when the bytes cannot be a file name.</summary>
    internal static string? ReadName(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        if (length == 0)
        {
            return null;
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            bool valid = i < length ? b is >= 0x21 and <= 0x7E : b == 0;
            if (!valid)
            {
                return null;
            }
        }

        return System.Text.Encoding.ASCII.GetString(bytes[..length]);
    }
}
