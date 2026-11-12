using Arcade.Core.Content;
using Arcade.Core.Football;

namespace Arcade.Tests.Content;

/// <summary>Checks the real data shipped with the game and the demonstration mod.</summary>
public sealed class GameDataTests
{
    [Fact]
    public void Base_content_loads_without_any_issue()
    {
        ContentLoadResult result = TestPaths.LoadRepository();
        Assert.Empty(result.Issues);
        FootballContent content = FootballContent.Load(result.Repository);
        Assert.Contains("normal", content.Pitches.Keys);
        Assert.Equal(7, content.Surfaces.Count);
        Assert.All(content.Teams.Values, team => Assert.True(team.Players.Count >= 11));
        Assert.Contains("4-4-2", content.Tactics.Keys);
        Assert.Contains("4-3-3", content.Tactics.Keys);
        Assert.Contains("classic", content.Rulesets.Keys);
        Assert.All(content.Teams.Values, team => Assert.Contains(team.Tactic, content.Tactics.Keys));
        var catalog = result.Repository.LoadData<AssetCatalog>(AssetCatalog.FileName);
        Assert.All(catalog.Textures.Values, path => Assert.True(result.Repository.Exists(path), path));
        Assert.All(catalog.Fonts.Values, path => Assert.True(result.Repository.Exists(path), path));
    }

    [Fact]
    public void Every_pitch_visual_references_known_textures()
    {
        ContentRepository repository = TestPaths.LoadRepository().Repository;
        FootballContent content = FootballContent.Load(repository);
        var catalog = repository.LoadData<AssetCatalog>(AssetCatalog.FileName);
        foreach (var pitch in content.Pitches.Values)
        {
            Assert.Contains(pitch.Visual.GrassTexture, catalog.Textures.Keys);
            Assert.Contains(pitch.Visual.SurroundTexture, catalog.Textures.Keys);
            Assert.Contains(pitch.Visual.NetTexture, catalog.Textures.Keys);
        }
    }

    [Fact]
    public void Example_mod_changes_pitch_team_physics_and_textures()
    {
        ContentLoadResult modded = TestPaths.LoadRepository("example-mod");
        Assert.Empty(modded.Issues);
        FootballContent baseContent = TestPaths.BaseContent;
        FootballContent modContent = FootballContent.Load(modded.Repository);

        Assert.Equal("Neon Rangers", modContent.Teams["reds"].Name);
        Assert.Equal("#18D8C8", modContent.Teams["reds"].Kit.Shirt);
        Assert.Equal(baseContent.Teams["reds"].Players.Count, modContent.Teams["reds"].Players.Count);
        Assert.Equal(0.9f, modContent.Physics.Ball.RollingResistance);
        Assert.Equal(baseContent.Physics.Ball.Radius, modContent.Physics.Ball.Radius);
        Assert.Equal("#7CFFF0", modContent.Pitches["normal"].Visual.LineColor);

        var catalog = modded.Repository.LoadData<AssetCatalog>(AssetCatalog.FileName);
        ResolvedFile grass = modded.Repository.Resolve(catalog.Textures["pitch.grass.normal"]);
        Assert.Equal("example-mod", grass.Pack.Id);
        Assert.True(File.Exists(grass.FullPath));
        Assert.Equal("base", modded.Repository.Resolve(catalog.Textures["player.sheet"]).Pack.Id);
    }
}
