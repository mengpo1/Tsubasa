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
    }

    public ContentRepository Repository { get; }

    public IReadOnlyList<ContentIssue> Issues { get; }

    public FootballContent Football { get; }

    public AssetCatalog Assets { get; }

    public DisplayConfig Display { get; }

    public InputConfig Input { get; }

    public Localizer Localizer { get; }

    public IReadOnlyList<string> PackIds => [.. Repository.Packs.Select(pack => pack.Id)];

    /// <summary>
    /// Loads base content plus mods. If the mods make the data invalid, the error is reported and the game
    /// falls back to the base content alone instead of running in an inconsistent state. Base content errors are fatal.
    /// </summary>
    public static GameData Load(string gameRoot, IReadOnlyList<string> mods, string language, GameLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        ContentLoadResult loaded = ContentLoader.Load(new ContentLoadOptions(gameRoot, mods), log);
        try
        {
            return new GameData(loaded, loaded.Issues, language, log);
        }
        catch (ContentException exception) when (loaded.Repository.Packs.Count > 1)
        {
            log.Error("content", "Mods produce invalid data; falling back to the base content", ("error", exception.Message));
            ContentLoadResult baseOnly = ContentLoader.Load(new ContentLoadOptions(gameRoot, []), log);
            var issues = new List<ContentIssue>(loaded.Issues)
            {
                new(ContentIssueSeverity.Error, string.Join(",", mods), $"Mods disabled: {exception.Message}"),
            };
            return new GameData(baseOnly, issues, language, log);
        }
    }
}
