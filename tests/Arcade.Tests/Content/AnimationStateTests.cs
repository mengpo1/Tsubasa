using Arcade.Core.Content;

namespace Arcade.Tests.Content;

public sealed class AnimationStateTests
{
    private static AnimationDefinition Sheet(string texture) => new()
    {
        Texture = texture,
        FrameWidth = 16,
        FrameHeight = 16,
        Directions = 8,
        Frames = [0],
        FrameDuration = 1f,
        Anchor = [8, 8],
    };

    private static AssetCatalog Catalog(Dictionary<string, IReadOnlyList<string>> states, params string[] animations) => new()
    {
        Textures = animations.ToDictionary(id => "tex." + id, id => $"{id}.png"),
        Fonts = new Dictionary<string, string>(),
        Animations = animations.ToDictionary(id => id, id => Sheet("tex." + id)),
        States = states,
    };

    [Fact]
    public void Missing_states_fall_back_along_the_parent_chain()
    {
        AssetCatalog catalog = Catalog(new() { ["idle"] = ["stand"], ["run"] = ["jog"] }, "stand", "jog");

        AnimationStateMap map = AnimationStateMap.Resolve(catalog, _ => true);

        Assert.Equal("jog", map.Get("dribble")!.AnimationId);
        Assert.Equal("run", map.Get("dribble")!.UsedState);
        Assert.Equal("stand", map.Get("keeper.hold")!.AnimationId);
        Assert.Contains("state 'dribble': shown with 'run' (jog)", map.Notes);
    }

    [Fact]
    public void Candidates_are_tried_in_order_and_unavailable_textures_are_skipped()
    {
        AssetCatalog catalog = Catalog(new() { ["idle"] = ["missing", "stand"] }, "missing", "stand");

        AnimationStateMap map = AnimationStateMap.Resolve(catalog, texture => texture != "tex.missing");

        Assert.Equal("stand", map.Get("idle")!.AnimationId);
        Assert.Contains(map.Notes, note => note.Contains("'missing' unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public void Theme_without_its_files_falls_back_to_the_default_look()
    {
        AssetCatalog theme = Catalog(new() { ["idle"] = ["classic"] }, "classic");
        AssetCatalog plain = Catalog(new() { ["idle"] = ["modern"], ["kick"] = ["modern-kick"] }, "modern", "modern-kick");

        AnimationStateMap map = AnimationStateMap.Resolve(theme, _ => false, plain, _ => true);

        ResolvedAnimation idle = map.Get("idle")!;
        Assert.Equal("modern", idle.AnimationId);
        Assert.True(idle.FromDefaultLook);
        Assert.Contains("state 'idle': default look used (modern)", map.Notes);
    }

    [Fact]
    public void Theme_stays_consistent_before_using_the_default_look()
    {
        // The theme has no kick: its own idle frame is preferred to the default look's kick.
        AssetCatalog theme = Catalog(new() { ["idle"] = ["classic"] }, "classic");
        AssetCatalog plain = Catalog(new() { ["idle"] = ["modern"], ["kick"] = ["modern-kick"] }, "modern", "modern-kick");

        AnimationStateMap map = AnimationStateMap.Resolve(theme, _ => true, plain, _ => true);

        Assert.Equal("classic", map.Get("kick")!.AnimationId);
        Assert.False(map.Get("kick")!.FromDefaultLook);
    }

    [Fact]
    public void Optional_states_may_be_absent_without_a_report()
    {
        AnimationStateMap map = AnimationStateMap.Resolve(Catalog(new() { ["idle"] = ["stand"], ["ball"] = ["ball"] }, "stand", "ball"), _ => true);

        Assert.Null(map.Get("ball.high"));
        Assert.DoesNotContain(map.Notes, note => note.Contains("ball.high", StringComparison.Ordinal));
        Assert.Empty(AnimationStateMap.Resolve(Catalog(new(), "x"), _ => true).All);
        Assert.Contains("state 'idle': no animation available", AnimationStateMap.Resolve(Catalog(new(), "x"), _ => true).Notes);
    }
}
