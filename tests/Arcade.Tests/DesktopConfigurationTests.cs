using Arcade.Desktop.Hosting;
using Arcade.Desktop.Input;

namespace Arcade.Tests;

/// <summary>Desktop-side data and command line (no window is created).</summary>
public sealed class DesktopConfigurationTests
{
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
