using Arcade.Core.Content;

namespace Arcade.Desktop.Hosting;

/// <summary>
/// Finds the game folder (the one containing <c>content/manifest.json</c>). In development the executable
/// runs from bin/..., so the search walks up to the repository and data files are read in place:
/// editing a JSON file and pressing F5 is enough to try a new tuning.
/// </summary>
internal static class GameRoot
{
    public static string Find(string? explicitRoot)
    {
        if (explicitRoot is not null)
        {
            string full = Path.GetFullPath(explicitRoot);
            return IsGameRoot(full)
                ? full
                : throw new ContentException($"'{full}' is not a game folder (missing {ContentLoadOptions.BaseFolder}/{ContentLoadOptions.ManifestFile}).");
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (IsGameRoot(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new ContentException($"No game folder found from '{AppContext.BaseDirectory}'. Use --root <dir>.");
    }

    private static bool IsGameRoot(string directory) =>
        File.Exists(Path.Combine(directory, ContentLoadOptions.BaseFolder, ContentLoadOptions.ManifestFile));
}
