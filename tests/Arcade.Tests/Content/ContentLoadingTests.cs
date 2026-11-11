using System.Text.Json.Nodes;
using Arcade.Core.Content;

namespace Arcade.Tests.Content;

public sealed class ContentLoadingTests
{
    [Theory]
    [InlineData("../secret.json")]
    [InlineData("/etc/passwd")]
    [InlineData("a//b.png")]
    [InlineData("a/./b.png")]
    [InlineData("")]
    [InlineData("c:x.png")]
    public void Unsafe_content_paths_are_rejected(string path)
    {
        Assert.False(ContentPath.TryNormalize(path, out _, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Content_paths_are_normalised_to_forward_slashes()
    {
        Assert.Equal("assets/textures/ball.png", ContentPath.Normalize("assets\\textures\\ball.png"));
    }

    [Fact]
    public void Json_merge_patch_merges_objects_replaces_values_and_removes_nulls()
    {
        var target = (JsonObject)JsonNode.Parse("""{ "a": 1, "b": { "c": 2, "d": 3 }, "list": [1, 2], "gone": true }""")!;
        var patch = (JsonObject)JsonNode.Parse("""{ "b": { "c": 20 }, "list": [9], "gone": null, "new": "x" }""")!;
        JsonMerge.Apply(target, patch);
        Assert.Equal(1, (int)target["a"]!);
        Assert.Equal(20, (int)target["b"]!["c"]!);
        Assert.Equal(3, (int)target["b"]!["d"]!);
        Assert.Single(target["list"]!.AsArray());
        Assert.False(target.ContainsKey("gone"));
        Assert.Equal("x", (string)target["new"]!);
    }

    [Fact]
    public void Mod_overrides_single_values_and_adds_files()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/test.json", """{ "speed": 1.0, "name": "base" }""");
        root.WriteMod("faster");
        root.WriteFile("mods/faster/config/test.json", """{ "speed": 2.5 }""");
        root.WriteFile("mods/faster/teams/extra.json", """{ "id": "extra" }""");

        ContentLoadResult result = root.Load("faster");

        Assert.Empty(result.Issues);
        JsonObject merged = result.Repository.LoadMergedJson("config/test.json");
        Assert.Equal(2.5, (double)merged["speed"]!);
        Assert.Equal("base", (string)merged["name"]!);
        Assert.Equal(["teams/extra.json"], result.Repository.ListFiles("teams", ".json"));
    }

    [Fact]
    public void Later_mods_have_priority_over_earlier_ones()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/test.json", """{ "value": "base" }""");
        root.WriteFile("content/textures/ball.png", "base");
        root.WriteMod("first");
        root.WriteFile("mods/first/config/test.json", """{ "value": "first" }""");
        root.WriteFile("mods/first/textures/ball.png", "first");
        root.WriteMod("second");
        root.WriteFile("mods/second/config/test.json", """{ "value": "second" }""");

        ContentLoadResult result = root.Load("first", "second");

        Assert.Equal("second", (string)result.Repository.LoadMergedJson("config/test.json")["value"]!);
        ResolvedFile texture = result.Repository.Resolve("textures/ball.png");
        Assert.Equal("first", texture.Pack.Id);
        Assert.Equal("first", File.ReadAllText(texture.FullPath));
    }

    [Fact]
    public void Removing_a_mod_restores_the_base_data()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/test.json", """{ "value": "base" }""");
        root.WriteMod("changer");
        root.WriteFile("mods/changer/config/test.json", """{ "value": "mod" }""");

        Assert.Equal("mod", (string)root.Load("changer").Repository.LoadMergedJson("config/test.json")["value"]!);
        Assert.Equal("base", (string)root.Load().Repository.LoadMergedJson("config/test.json")["value"]!);
    }

    [Fact]
    public void Mod_with_malformed_json_is_rejected_as_a_whole()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/test.json", """{ "value": "base" }""");
        root.WriteMod("broken");
        root.WriteFile("mods/broken/config/test.json", """{ "value": "mod" }""");
        root.WriteFile("mods/broken/teams/bad.json", """{ "id": "bad", """);

        ContentLoadResult result = root.Load("broken");

        ContentIssue issue = Assert.Single(result.Issues);
        Assert.Equal(ContentIssueSeverity.Error, issue.Severity);
        Assert.Contains("teams/bad.json", issue.Message, StringComparison.Ordinal);
        Assert.Equal("base", (string)result.Repository.LoadMergedJson("config/test.json")["value"]!);
        Assert.Single(result.Repository.Packs);
    }

    [Theory]
    [InlineData(99, null, "newer than the supported version")]
    [InlineData(1, "Bad Id", "must contain only lowercase")]
    [InlineData(1, "other-name", "does not match the folder name")]
    public void Mod_with_invalid_manifest_is_rejected_with_a_clear_message(int formatVersion, string? manifestId, string expected)
    {
        using var root = new TempGameRoot();
        root.WriteMod("mymod", formatVersion, manifestId);

        ContentLoadResult result = root.Load("mymod");

        ContentIssue issue = Assert.Single(result.Issues);
        Assert.Contains(expected, issue.Message, StringComparison.Ordinal);
        Assert.Single(result.Repository.Packs);
    }

    [Fact]
    public void Enabled_mod_without_folder_is_reported()
    {
        using var root = new TempGameRoot();
        ContentLoadResult result = root.Load("ghost");
        Assert.Contains(result.Issues, issue => issue.PackId == "ghost" && issue.Severity == ContentIssueSeverity.Error);
    }

    [Fact]
    public void Unknown_properties_in_data_files_are_reported_with_the_file_name()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/catalog.json", """{ "textures": {}, "fonts": {}, "animations": {}, "texturez": {} }""");
        ContentRepository repository = root.Load().Repository;

        var exception = Assert.Throws<ContentException>(() => repository.LoadData<AssetCatalog>("config/catalog.json"));
        Assert.Contains("config/catalog.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("texturez", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Asset_catalog_rejects_paths_escaping_the_pack()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/assets.json", """{ "textures": { "ball.default": "../../outside.png" }, "fonts": {}, "animations": {} }""");
        ContentRepository repository = root.Load().Repository;

        var exception = Assert.Throws<ContentException>(() => repository.LoadData<AssetCatalog>(AssetCatalog.FileName));
        Assert.Contains("ball.default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Hidden_files_such_as_finder_metadata_are_ignored()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/.DS_Store", "binary junk");
        root.WriteFile("content/teams/.hidden.json", "{ not json");
        ContentLoadResult result = root.Load();
        Assert.Empty(result.Repository.Packs[0].Files);
    }
}
