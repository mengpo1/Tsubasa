using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Localization;
using Arcade.Desktop.Input;

namespace Arcade.Desktop.Hosting;

/// <summary>Everything loaded from the content packs, validated as a whole.</summary>
internal sealed class GameData
{
    private GameData(ContentLoadResult loaded, IReadOnlyList<ContentIssue> issues, string language, GameLog log)
    {
        ContentRepository repository = loaded.Repository;
        Repository = repository;
        Issues = issues;
        Football = FootballContent.Load(repository);
        Assets = repository.LoadData<AssetCatalog>(AssetCatalog.FileName);
        Display = repository.LoadData<DisplayConfig>(DisplayConfig.FileName);
        Input = repository.LoadData<InputConfig>(InputConfig.FileName);
        Localizer = Localizer.Load(repository, language, "en", log);
        if (repository.Theme is ContentPack theme)
        {
            ThemeId = theme.Id;
            ThemeName = theme.Manifest.Name;
            DefaultRepository = repository.Without(theme);
            DefaultAssets = DefaultRepository.LoadData<AssetCatalog>(AssetCatalog.FileName);
        }
    }

    public ContentRepository Repository { get; }

    public IReadOnlyList<ContentIssue> Issues { get; }

    public FootballContent Football { get; }

    public AssetCatalog Assets { get; }

    public DisplayConfig Display { get; }

    public InputConfig Input { get; }

    public Localizer Localizer { get; }

    /// <summary>Active visual theme, or null for the default look.</summary>
    public string? ThemeId { get; }

    public string? ThemeName { get; }

    /// <summary>The same content without the theme (default look), used for what the theme does not provide.</summary>
    public ContentRepository? DefaultRepository { get; }

    public AssetCatalog? DefaultAssets { get; }

    public IReadOnlyList<string> PackIds => [.. Repository.Packs.Select(pack => pack.Id)];

    /// <summary>
    /// Loads base content plus theme and mods. If the mods (then the theme) make the data invalid, the error is reported
    /// and the game falls back without them instead of running in an inconsistent state. Base content errors are fatal.
    /// </summary>
    public static GameData Load(string gameRoot, IReadOnlyList<string> mods, string? theme, string language, GameLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(mods);
        var issues = new List<ContentIssue>();
        (IReadOnlyList<string> Mods, string? Theme)[] attempts = [(mods, theme), ([], theme), ([], null)];
        for (int i = 0; ; i++)
        {
            (IReadOnlyList<string> tryMods, string? tryTheme) = attempts[i];
            if (i > 0 && tryMods.Count == attempts[i - 1].Mods.Count && tryTheme == attempts[i - 1].Theme)
            {
                continue;
            }

            ContentLoadResult loaded = ContentLoader.Load(new ContentLoadOptions(gameRoot, tryMods, tryTheme), log);
            issues.AddRange(loaded.Issues.Where(issue => !issues.Contains(issue)));
            try
            {
                return new GameData(loaded, issues, language, log);
            }
            catch (ContentException exception) when (i < attempts.Length - 1 && loaded.Repository.Packs.Count > 1)
            {
                string disabled = tryMods.Count > 0 ? string.Join(",", tryMods) : tryTheme ?? "?";
                log.Error("content", "Invalid data; falling back", ("disabled", disabled), ("error", exception.Message));
                issues.Add(new ContentIssue(ContentIssueSeverity.Error, disabled, $"Disabled: {exception.Message}"));
            }
        }
    }
}
