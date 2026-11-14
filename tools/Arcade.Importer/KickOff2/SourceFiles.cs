using System.IO.Compression;
using Arcade.Importer.Amiga;

namespace Arcade.Importer.KickOff2;

/// <summary>
/// The files available for the import, gathered from disk images and folders in the order given: a later source
/// replaces a file of the same name (names are compared without case), and the members of <c>.dat</c> archives are
/// merged the same way, so that The Final Whistle can complete or override Kick Off 2.
/// </summary>
internal sealed class SourceFiles
{
    private readonly Dictionary<string, DiskFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, DiskFile>> _members = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Problems { get; } = [];

    /// <summary>Description of each source and of what it provided, for the report.</summary>
    public List<string> Sources { get; } = [];

    public IReadOnlyCollection<DiskFile> Files => _files.Values;

    public void AddDiskImage(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var input = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            data = output.ToArray();
        }

        string origin = Path.GetFileName(path);
        IReadOnlyList<DiskFile> files = DdfsDisk.Read(data, origin, Problems);
        Sources.Add($"{origin}: disk image, {files.Count} files ({string.Join(", ", files.Select(f => f.Name))})");
        foreach (DiskFile file in files)
        {
            Add(file);
        }
    }

    public void AddDirectory(string path)
    {
        string root = Path.GetFullPath(path);
        var names = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith('.'))
            {
                continue;
            }

            names.Add(name);
            Add(new DiskFile(name, File.ReadAllBytes(file), Path.GetFileName(root) + "/" + name));
        }

        Sources.Add($"{Path.GetFileName(root)}/: folder, {names.Count} files ({string.Join(", ", names)})");
    }

    public void Add(DiskFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _files[file.Name] = file;
        if (file.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) && DatArchive.LooksLikeArchive(file.Data))
        {
            if (!_members.TryGetValue(file.Name, out Dictionary<string, DiskFile>? members))
            {
                members = new Dictionary<string, DiskFile>(StringComparer.OrdinalIgnoreCase);
                _members[file.Name] = members;
            }

            foreach (DiskFile member in DatArchive.Read(file.Data, $"{file.Origin}/{file.Name}", Problems))
            {
                members[member.Name] = member;
            }
        }
    }

    /// <summary>Members of an archive (merged across sources), empty when the archive is absent.</summary>
    public IReadOnlyCollection<DiskFile> Members(string archive) =>
        _members.TryGetValue(archive, out Dictionary<string, DiskFile>? members) ? members.Values : [];

    /// <summary>A file by name: member of <paramref name="archive"/> first, then a loose file (already extracted).</summary>
    public DiskFile? Find(string name, string? archive = null)
    {
        if (archive is not null && _members.TryGetValue(archive, out Dictionary<string, DiskFile>? members) && members.TryGetValue(name, out DiskFile? member))
        {
            return member;
        }

        return _files.GetValueOrDefault(name);
    }
}
