using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Desktop.Hosting;
using Arcade.Desktop.Input;

namespace Arcade.Tests;

/// <summary>Desktop-side data and command line (no window is created).</summary>
public sealed class DesktopConfigurationTests
{
    private static readonly string[] DrawnStates = ["idle", "run", "dribble", "kick", "control", "keeper.run", "keeper.hold", "ball", "player.shadow"];

    [Fact]
    public void Display_and_input_configurations_are_valid()
    {
        var repository = TestPaths.LoadRepository().Repository;
        var display = repository.LoadData<DisplayConfig>(DisplayConfig.FileName);
        var input = repository.LoadData<InputConfig>(InputConfig.FileName);
        Assert.True(display.VirtualWidth > 0);
        Assert.NotEmpty(input.Players);
        Assert.All(Enum.GetNames<SystemAction>(), action =>
            Assert.Contains(input.System.Keys, key => string.Equals(key, action, StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("classic-amiga")]
    [InlineData("custom")]
    public void Every_theme_loads_its_display_and_animation_mapping(string theme)
    {
        GameData data = GameData.Load(TestPaths.RepositoryRoot, [], theme, "en", new GameLog());

        Assert.Empty(data.Issues);
        Assert.Equal(theme, data.ThemeId);
        Assert.NotNull(data.DefaultAssets);
        // Without imported files every state still has something to show (theme frames or the default look).
        AnimationStateMap map = AnimationStateMap.Resolve(data.Assets, id => data.Repository.Exists(data.Assets.Textures[id]), data.DefaultAssets, id => data.DefaultRepository!.Exists(data.DefaultAssets!.Textures[id]));
        Assert.All(DrawnStates, state => Assert.NotNull(map.Get(state)));
    }

    [Fact]
    public void Classic_theme_changes_the_look_but_not_the_game()
    {
        GameData plain = GameData.Load(TestPaths.RepositoryRoot, [], null, "en", new GameLog());
        GameData classic = GameData.Load(TestPaths.RepositoryRoot, [], "classic-amiga", "en", new GameLog());

        Assert.Equal(320, classic.Display.VirtualWidth);
        Assert.Equal(256, classic.Display.VirtualHeight);
        Assert.NotNull(classic.Display.PitchBitmap);
        Assert.Null(plain.Display.PitchBitmap);
        Assert.Equal(plain.Football.Physics, classic.Football.Physics);
        Assert.Equal(plain.Football.Controls, classic.Football.Controls);
        Assert.Equal(plain.Football.Pitches.Keys.Order(), classic.Football.Pitches.Keys.Order());
    }

    [Fact]
    public void Classic_theme_uses_its_own_frames_once_imported()
    {
        GameData classic = GameData.Load(TestPaths.RepositoryRoot, [], "classic-amiga", "en", new GameLog());
        AnimationStateMap map = AnimationStateMap.Resolve(classic.Assets, _ => true, classic.DefaultAssets, _ => true);

        Assert.Equal("classic.run", map.Get("run")!.AnimationId);
        Assert.Equal("classic.run", map.Get("dribble")!.AnimationId);
        Assert.Equal("classic.idle", map.Get("kick")!.AnimationId);
        Assert.Equal("classic.keeper.idle", map.Get("keeper.hold")!.AnimationId);
        Assert.Equal("classic.ball.low", map.Get("ball")!.AnimationId);
        Assert.All(map.All.Where(r => r.State != "player.shadow"), r => Assert.False(r.FromDefaultLook));
        Assert.Contains(map.Notes, note => note.Contains("'kick': shown with 'idle'", StringComparison.Ordinal));
    }

    [Fact]
    public void Theme_option_is_parsed()
    {
        Assert.Equal("classic-amiga", LaunchOptions.Parse(["--theme", "classic-amiga"]).Theme);
        Assert.True(LaunchOptions.Parse(["--list-themes"]).ListThemes);
    }

    [Fact]
    public void A_misspelt_key_in_the_bindings_is_reported()
    {
        using var root = new TempGameRoot();
        root.WriteFile("content/config/input.json", """
            { "players": [ { "up": ["Upp"], "down": [], "left": [], "right": [], "fire": ["Space"], "gamepad": -1, "gamepadFire": [] } ],
              "gamepadDeadZone": 0.3, "system": { "quit": ["Escape"] } }
            """);
        var repository = root.Load().Repository;
        var exception = Assert.Throws<Arcade.Core.Content.ContentException>(() => repository.LoadData<InputConfig>(InputConfig.FileName));
        Assert.Contains("Upp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_options_are_parsed_and_unknown_ones_rejected()
    {
        var options = LaunchOptions.Parse(["--mod", "a", "--mod", "b", "--lang", "fr", "--capture-ticks", "10,20", "--exit-tick", "30"]);
        Assert.Equal(["a", "b"], options.Mods);
        Assert.Equal("fr", options.Language);
        Assert.Equal([10L, 20L], options.CaptureTicks);
        Assert.Equal(30L, options.ExitTick);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--modd", "x"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--scale"]));
    }

    [Fact]
    public void Game_root_is_found_from_the_build_output()
    {
        Assert.Equal(TestPaths.RepositoryRoot, GameRoot.Find(null));
    }
}
