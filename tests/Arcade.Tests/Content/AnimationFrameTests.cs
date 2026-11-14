using Arcade.Core.Content;

namespace Arcade.Tests.Content;

public sealed class AnimationFrameTests
{
    private static AnimationDefinition Running(int stride = 0, int columns = 0) => new()
    {
        Texture = "players",
        FrameWidth = 16,
        FrameHeight = 16,
        Directions = 8,
        Frames = [0, 3, 2, 1, 1, 2, 3, 4, 7, 6, 5, 5, 6, 7],
        FrameDuration = 0.05f,
        ReferenceSpeed = 10f,
        Anchor = [8, 15],
        DirectionStride = stride,
        SheetColumns = columns,
    };

    [Fact]
    public void Distance_driven_animation_advances_one_frame_per_reference_step()
    {
        AnimationDefinition run = Running();
        // 10 m/s x 0.05 s = 0.5 m per frame.
        Assert.Equal(0, AnimationFrames.FrameNumber(run, 0.49f, 99f, 0f));
        Assert.Equal(1, AnimationFrames.FrameNumber(run, 0.51f, 0f, 0f));
        Assert.Equal(0, AnimationFrames.FrameNumber(run, 7.01f, 0f, 0f));
    }

    [Fact]
    public void Time_driven_animation_ignores_distance()
    {
        AnimationDefinition idle = Running() with { ReferenceSpeed = 0f, Frames = [0, 1, 2] };
        Assert.Equal(2, AnimationFrames.FrameNumber(idle, 100f, 0.11f, 0f));
        Assert.Equal(0, AnimationFrames.FrameNumber(idle, 100f, 0.15f, 0f));
    }

    [Fact]
    public void Height_driven_animation_clamps_to_the_last_frame()
    {
        AnimationDefinition ball = Running() with { Directions = 1, Frames = [4, 5, 6, 7], Driver = AnimationDriver.Height, HeightStep = 32f };
        Assert.Equal(0, AnimationFrames.FrameNumber(ball, 0f, 0f, -3f));
        Assert.Equal(1, AnimationFrames.FrameNumber(ball, 0f, 0f, 40f));
        Assert.Equal(3, AnimationFrames.FrameNumber(ball, 0f, 0f, 500f));
    }

    [Fact]
    public void Rows_layout_puts_directions_on_rows()
    {
        Assert.Equal((3 * 16, 2 * 16), AnimationFrames.FrameOrigin(Running(), 1, 2, 320));
    }

    [Fact]
    public void Indexed_layout_numbers_frames_across_the_grid_like_the_original_sheets()
    {
        // Tile = sequence[i] + 8 x direction on a 20-column sheet: step 7 is frame 4; direction 3 (down-right) -> tile 28 -> column 8, row 1.
        AnimationDefinition run = Running(stride: 8);
        Assert.Equal((8 * 16, 16), AnimationFrames.FrameOrigin(run, 7, 3, 320));
        Assert.Equal((8 * 16, 16), AnimationFrames.FrameOrigin(run with { SheetColumns = 20 }, 7, 3, 0));
        Assert.Equal((0, 0), AnimationFrames.FrameOrigin(run, 0, 0, 320));
    }

    [Fact]
    public void Asset_catalog_reports_empty_state_mappings()
    {
        var catalog = new AssetCatalog
        {
            Textures = new Dictionary<string, string> { ["players"] = "players.png" },
            Fonts = new Dictionary<string, string>(),
            Animations = new Dictionary<string, AnimationDefinition> { ["run"] = Running() },
            States = new Dictionary<string, IReadOnlyList<string>> { ["kick"] = [] },
        };
        var problems = new List<string>();
        catalog.Validate(problems);
        Assert.Equal(["states.kick: list at least one animation id"], problems);
    }
}
