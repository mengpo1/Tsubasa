namespace Arcade.Core.Localization;

/// <summary>
/// Plural selection rules, chosen per language in the locale file ("@plural").
/// Covers the languages planned for the project; more rules can be added here without touching callers.
/// </summary>
public enum PluralRule
{
    /// <summary>"one" for exactly 1, "other" otherwise (English, German, Italian, Spanish...).</summary>
    OneIsSingular,

    /// <summary>"one" for 0 and 1, "other" otherwise (French).</summary>
    ZeroAndOneAreSingular,

    /// <summary>No plural forms: always "other" (Japanese, Chinese...).</summary>
    NoPlural,
}

public static class PluralRules
{
    public static string Category(this PluralRule rule, long count) => rule switch
    {
        PluralRule.OneIsSingular => count == 1 ? "one" : "other",
        PluralRule.ZeroAndOneAreSingular => count is 0 or 1 ? "one" : "other",
        _ => "other",
    };

    public static bool TryParse(string? value, out PluralRule rule)
    {
        switch (value)
        {
            case "one":
                rule = PluralRule.OneIsSingular;
                return true;
            case "zero-one":
                rule = PluralRule.ZeroAndOneAreSingular;
                return true;
            case "none":
                rule = PluralRule.NoPlural;
                return true;
            default:
                rule = PluralRule.OneIsSingular;
                return false;
        }
    }
}
