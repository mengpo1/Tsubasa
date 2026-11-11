using System.Buffers;

namespace Arcade.Core.Content;

/// <summary>
/// Validation of the relative paths used inside content packs. Paths always use '/' and may never
/// escape the pack folder (no rooted paths, no "..", no empty segments).
/// </summary>
public static class ContentPath
{
    private static readonly SearchValues<char> ForbiddenCharacters = SearchValues.Create(":*?\"<>|\0");

    public static string Normalize(string relativePath)
    {
        if (!TryNormalize(relativePath, out string normalized, out string? error))
        {
            throw new ContentException($"Invalid content path '{relativePath}': {error}");
        }

        return normalized;
    }

    public static bool TryNormalize(string? relativePath, out string normalized, out string? error)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "path is empty";
            return false;
        }

        string path = relativePath.Replace('\\', '/');
        if (path.StartsWith('/') || Path.IsPathRooted(relativePath))
        {
            error = "absolute paths are not allowed";
            return false;
        }

        if (path.AsSpan().ContainsAny(ForbiddenCharacters))
        {
            error = "path contains a forbidden character";
            return false;
        }

        string[] segments = path.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                error = "path segments must not be empty, '.' or '..'";
                return false;
            }
        }

        normalized = string.Join('/', segments);
        error = null;
        return true;
    }

    /// <summary>Combines a pack root and a validated relative path, and checks that the result stays inside the root.</summary>
    public static string ToFullPath(string packRoot, string normalizedRelativePath)
    {
        string root = Path.GetFullPath(packRoot);
        string full = Path.GetFullPath(Path.Combine(root, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ContentException($"Content path '{normalizedRelativePath}' escapes its pack folder '{packRoot}'.");
        }

        return full;
    }
}
