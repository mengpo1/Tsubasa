using System.Text.Json;
using System.Text.Json.Nodes;

namespace Arcade.Core.Content;

/// <summary>A file found in the layered content, with the pack that provides it.</summary>
public readonly record struct ResolvedFile(string RelativePath, string FullPath, ContentPack Pack);

/// <summary>
/// Layered view over the base content and the enabled mods. Packs are ordered by priority:
/// the base pack first, then mods in load order; a later pack overrides an earlier one.
/// Binary files (textures, fonts...) are replaced as a whole; JSON files are merged
/// (see <see cref="JsonMerge"/>).
/// </summary>
public sealed class ContentRepository
{
    private readonly List<ContentPack> _packs;

    public ContentRepository(IEnumerable<ContentPack> packsInPriorityOrder)
    {
        _packs = [.. packsInPriorityOrder];
        if (_packs.Count == 0 || !_packs[0].IsBase)
        {
            throw new ArgumentException("The first pack must be the base content pack.", nameof(packsInPriorityOrder));
        }
    }

    public IReadOnlyList<ContentPack> Packs => _packs;

    public bool Exists(string relativePath) => TryResolve(relativePath, out _);

    public bool TryResolve(string relativePath, out ResolvedFile file)
    {
        string path = ContentPath.Normalize(relativePath);
        for (int i = _packs.Count - 1; i >= 0; i--)
        {
            ContentPack pack = _packs[i];
            if (pack.Contains(path))
            {
                file = new ResolvedFile(path, ContentPath.ToFullPath(pack.RootDirectory, path), pack);
                return true;
            }
        }

        file = default;
        return false;
    }

    public ResolvedFile Resolve(string relativePath)
    {
        if (!TryResolve(relativePath, out ResolvedFile file))
        {
            throw new ContentException($"Content file '{relativePath}' was not found in any pack ({DescribePacks()}).");
        }

        return file;
    }

    public Stream OpenRead(string relativePath) => File.OpenRead(Resolve(relativePath).FullPath);

    /// <summary>Packs that provide the given file, lowest priority first.</summary>
    public IReadOnlyList<ContentPack> PacksProviding(string relativePath)
    {
        string path = ContentPath.Normalize(relativePath);
        return [.. _packs.Where(pack => pack.Contains(path))];
    }

    /// <summary>Union of the files with the given extension directly inside <paramref name="directory"/>, sorted.</summary>
    public IReadOnlyList<string> ListFiles(string directory, string extension)
    {
        string prefix = ContentPath.Normalize(directory) + "/";
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (ContentPack pack in _packs)
        {
            foreach (string file in pack.Files)
            {
                if (file.StartsWith(prefix, StringComparison.Ordinal)
                    && file.IndexOf('/', prefix.Length) < 0
                    && file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(file);
                }
            }
        }

        return [.. result];
    }

    /// <summary>Loads a JSON object merged across every pack that provides it.</summary>
    public JsonObject LoadMergedJson(string relativePath) =>
        TryLoadMergedJson(relativePath)
        ?? throw new ContentException($"Required data file '{relativePath}' was not found in any pack ({DescribePacks()}).");

    public JsonObject? TryLoadMergedJson(string relativePath)
    {
        JsonObject? merged = null;
        foreach (ContentPack pack in PacksProviding(relativePath))
        {
            JsonObject layer = ReadJsonObject(pack, relativePath);
            if (merged is null)
            {
                merged = layer;
            }
            else
            {
                JsonMerge.Apply(merged, layer);
            }
        }

        return merged;
    }

    /// <summary>Loads, merges, deserialises and validates a data file.</summary>
    public T LoadData<T>(string relativePath)
        where T : class
    {
        JsonObject merged = LoadMergedJson(relativePath);
        return Deserialize<T>(merged, relativePath);
    }

    /// <summary>Deserialises and validates an already merged JSON object, reporting errors with the file name and its packs.</summary>
    public T Deserialize<T>(JsonNode node, string relativePath)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(node);
        T? value;
        try
        {
            value = node.Deserialize<T>(ContentJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new ContentException($"Invalid data in '{relativePath}' (packs: {DescribeProviders(relativePath)}): {exception.Message}", exception);
        }

        if (value is null)
        {
            throw new ContentException($"Data file '{relativePath}' is empty.");
        }

        if (value is IContentData data)
        {
            var problems = new List<string>();
            data.Validate(problems);
            if (problems.Count > 0)
            {
                throw new ContentException(
                    $"Invalid values in '{relativePath}' (packs: {DescribeProviders(relativePath)}): {string.Join("; ", problems)}");
            }
        }

        return value;
    }

    private static JsonObject ReadJsonObject(ContentPack pack, string relativePath)
    {
        string fullPath = ContentPath.ToFullPath(pack.RootDirectory, ContentPath.Normalize(relativePath));
        JsonNode? node;
        try
        {
            node = ContentJson.Parse(File.ReadAllText(fullPath));
        }
        catch (JsonException exception)
        {
            throw new ContentException($"Malformed JSON in '{relativePath}' of pack '{pack.Id}': {exception.Message}", exception);
        }

        return node as JsonObject
            ?? throw new ContentException($"'{relativePath}' of pack '{pack.Id}' must contain a JSON object at its root.");
    }

    private string DescribePacks() => string.Join(", ", _packs.Select(pack => pack.Id));

    private string DescribeProviders(string relativePath) => string.Join(" > ", PacksProviding(relativePath).Select(pack => pack.Id));
}
