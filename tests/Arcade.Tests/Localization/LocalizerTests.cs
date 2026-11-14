using Arcade.Core.Localization;

namespace Arcade.Tests.Localization;

public sealed class LocalizerTests
{
    private static Localizer Create(string language) => Localizer.FromTables(
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["@plural"] = "one",
                ["menu.play"] = "Play",
                ["only.english"] = "English only",
                ["goals.one"] = "{count} goal",
                ["goals.other"] = "{count} goals",
                ["score"] = "{home} – {away}",
            },
            ["fr"] = new Dictionary<string, string>
            {
                ["@name"] = "Français",
                ["@plural"] = "zero-one",
                ["menu.play"] = "Jouer",
                ["goals.one"] = "{count} but",
                ["goals.other"] = "{count} buts",
                ["score"] = "{home} à {away}",
            },
        },
        language);

    [Fact]
    public void Strings_follow_the_current_language_and_can_be_switched()
    {
        Localizer localizer = Create("fr");
        Assert.Equal("Jouer", localizer.Get("menu.play"));
        Assert.True(localizer.SetLanguage("en"));
        Assert.Equal("Play", localizer.Get("menu.play"));
        Assert.False(localizer.SetLanguage("xx"));
        Assert.Equal("en", localizer.Language);
    }

    [Fact]
    public void Missing_translation_falls_back_to_english_and_is_recorded()
    {
        Localizer localizer = Create("fr");
        Assert.Equal("English only", localizer.Get("only.english"));
        Assert.Contains("fr:only.english", localizer.MissingKeys);
    }

    [Fact]
    public void Unknown_key_is_visible_and_recorded()
    {
        Localizer localizer = Create("en");
        Assert.Equal("[nope]", localizer.Get("nope"));
        Assert.Contains("nope", localizer.MissingKeys);
    }

    [Theory]
    [InlineData("en", 0, "0 goals")]
    [InlineData("en", 1, "1 goal")]
    [InlineData("en", 2, "2 goals")]
    [InlineData("fr", 0, "0 but")]
    [InlineData("fr", 1, "1 but")]
    [InlineData("fr", 2, "2 buts")]
    public void Plural_forms_follow_the_language_rule(string language, long count, string expected)
    {
        Assert.Equal(expected, Create(language).GetPlural("goals", count));
    }

    [Fact]
    public void Named_parameters_let_each_language_order_the_sentence()
    {
        Assert.Equal("2 à 1", Create("fr").Get("score", ("away", 1), ("home", 2)));
        Assert.Equal("2 – 1", Create("en").Get("score", ("home", 2), ("away", 1)));
    }

    [Fact]
    public void Unicode_text_is_preserved()
    {
        Localizer localizer = Localizer.FromTables(
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string> { ["title"] = "Fußball · Ελλάδα · Футбол · サッカー" },
            },
            "en");
        Assert.Equal("Fußball · Ελλάδα · Футбол · サッカー", localizer.Get("title"));
    }

    [Fact]
    public void Real_locale_files_have_the_same_keys_in_english_and_french()
    {
        var repository = TestPaths.LoadRepository().Repository;
        var english = repository.LoadMergedJson("locales/en.json").Select(pair => pair.Key).Where(key => !key.StartsWith('@')).ToHashSet();
        var french = repository.LoadMergedJson("locales/fr.json").Select(pair => pair.Key).Where(key => !key.StartsWith('@')).ToHashSet();
        Assert.Empty(english.Except(french));
        Assert.Empty(french.Except(english));
    }

    [Fact]
    public void A_mod_can_override_a_single_translated_string()
    {
        var withMod = Localizer.Load(TestPaths.LoadRepository("example-mod").Repository, "fr");
        var withoutMod = Localizer.Load(TestPaths.LoadRepository().Repository, "fr");
        Assert.Equal("Entraînement nocturne néon", withMod.Get("hud.training"));
        Assert.Equal("Entraînement", withoutMod.Get("hud.training"));
        Assert.Equal(withoutMod.Get("hud.goal"), withMod.Get("hud.goal"));
    }
}
