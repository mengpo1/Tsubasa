using System.Text.Json.Nodes;
using Arcade.Core.Content;

namespace Arcade.Tests.Content;

/// <summary>Themes: a layer between the base content and the mods that may only change the look.</summary>
public sealed class ThemeLoadingTests
{
    [Fact]
    public void Theme_overrides_assets_and_display_but_mods_still_win()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/display.json", """{ "scale": 1, "name": "base" }""");
        root.WriteFile("content/textures/ball.png", "base");
        root.WriteTheme("retro");
        root.WriteFile("content/themes/retro/config/display.json", """{ "scale": 2 }""");
        root.WriteFile("content/themes/retro/textures/ball.png", "retro");
        root.WriteMod("big");
        root.WriteFile("mods/big/config/display.json", """{ "scale": 3 }""");

        ContentLoadResult themed = root.LoadWithTheme("retro");
        ContentLoadResult modded = root.LoadWithTheme("retro", "big");

        Assert.Empty(themed.Issues);
        Assert.Equal(2, (int)themed.Repository.LoadMergedJson("config/display.json")["scale"]!);
        Assert.Equal("base", (string)themed.Repository.LoadMergedJson("config/display.json")["name"]!);
        Assert.Equal("retro", File.ReadAllText(themed.Repository.Resolve("textures/ball.png").FullPath));
        Assert.Equal("retro", themed.Repository.Theme?.Id);
        Assert.Equal(3, (int)modded.Repository.LoadMergedJson("config/display.json")["scale"]!);
    }

    [Fact]
    public void Theme_folders_are_not_part_of_the_base_pack()
    {
        using var root = new TempGameRoot();
        root.WriteTheme("retro");
        root.WriteFile("content/themes/retro/textures/ball.png", "retro");

        ContentLoadResult result = root.Load();

        Assert.Empty(result.Issues);
        Assert.Null(result.Repository.Theme);
        Assert.False(result.Repository.Exists("themes/retro/textures/ball.png"));
        Assert.False(result.Repository.Exists("textures/ball.png"));
    }

    [Theory]
    [InlineData("config/physics.json")]
    [InlineData("rulesets/classic.json")]
    [InlineData("teams/reds.json")]
    public void Theme_changing_gameplay_data_is_rejected_as_a_whole(string file)
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/physics.json", """{ "gravity": 9.81 }""");
        root.WriteFile("content/textures/ball.png", "base");
        root.WriteTheme("cheat");
        root.WriteFile("content/themes/cheat/textures/ball.png", "cheat");
        root.WriteFile($"content/themes/cheat/{file}", """{ "gravity": 1 }""");

        ContentLoadResult result = root.LoadWithTheme("cheat");

        ContentIssue issue = Assert.Single(result.Issues);
        Assert.Equal(ContentIssueSeverity.Error, issue.Severity);
        Assert.Contains(file, issue.Message, StringComparison.Ordinal);
        Assert.Null(result.Repository.Theme);
        Assert.Equal(9.81, (double)result.Repository.LoadMergedJson("config/physics.json")["gravity"]!);
        Assert.Equal("base", File.ReadAllText(result.Repository.Resolve("textures/ball.png").FullPath));
    }

    [Fact]
    public void Missing_or_mismatched_theme_is_reported_and_ignored()
    {
        using var root = new TempGameRoot();
        root.WriteTheme("folder", manifestId: "other");

        ContentLoadResult missing = root.LoadWithTheme("nowhere");
        ContentLoadResult mismatched = root.LoadWithTheme("folder");

        Assert.Contains("not found", Assert.Single(missing.Issues).Message, StringComparison.Ordinal);
        Assert.Contains("does not match", Assert.Single(mismatched.Issues).Message, StringComparison.Ordinal);
        Assert.Null(missing.Repository.Theme);
        Assert.Null(mismatched.Repository.Theme);
    }

    [Fact]
    public void Repository_without_the_theme_falls_back_to_the_default_look()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/textures/ball.png", "base");
        root.WriteTheme("retro");
        root.WriteFile("content/themes/retro/textures/ball.png", "retro");

        ContentRepository themed = root.LoadWithTheme("retro").Repository;
        ContentRepository plain = themed.Without(themed.Theme!);

        Assert.Equal("base", File.ReadAllText(plain.Resolve("textures/ball.png").FullPath));
        Assert.Null(plain.Theme);
        Assert.Throws<ArgumentException>(() => themed.Without(themed.Packs.First(pack => pack.IsBase)));
    }

    [Fact]
    public void Installed_themes_are_listed_by_folder()
    {
        using var root = new TempGameRoot();
        root.WriteTheme("zeta");
        root.WriteTheme("alpha");
        root.WriteFile("content/themes/no-manifest/readme.txt", "x");

        Assert.Equal(["alpha", "zeta"], ContentLoader.ListThemes(root.Root));
    }

    [Fact]
    public void Repository_themes_are_valid_and_only_touch_the_look()
    {
        foreach (string theme in ContentLoader.ListThemes(TestPaths.RepositoryRoot))
        {
            ContentLoadResult result = ContentLoader.Load(new ContentLoadOptions(TestPaths.RepositoryRoot, [], theme), new Arcade.Core.Common.GameLog());
            Assert.Empty(result.Issues);
            Assert.Equal(theme, result.Repository.Theme?.Id);
            JsonObject? assets = result.Repository.TryLoadMergedJson(AssetCatalog.FileName);
            Assert.NotNull(assets);
            AssetCatalog catalog = result.Repository.LoadData<AssetCatalog>(AssetCatalog.FileName);
            Assert.NotEmpty(catalog.Textures);
        }
    }
}
