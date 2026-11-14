using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Arcade.Core.Common;
using Arcade.Core.Content;

namespace Arcade.Core.Localization;

/// <summary>
/// Translates stable text keys. Locale files live in <c>locales/&lt;code&gt;.json</c> and are merged across packs,
/// so a mod can override single strings or add a language.
/// <para>
/// Strings may contain named parameters (<c>{name}</c>). Plural forms are separate keys suffixed with the plural
/// category (<c>key.one</c>, <c>key.other</c>) chosen by the language's rule. A missing key falls back to the
/// fallback language, then to a visible marker (<c>[key]</c>) and is recorded in <see cref="MissingKeys"/>.
/// </para>
/// </summary>
public sealed class Localizer
{
    public const string LocalesFolder = "locales";
    public const string NameKey = "@name";
    public const string PluralKey = "@plural";

    private readonly Dictionary<string, LocaleTable> _locales;
    private readonly HashSet<string> _missingKeys = new(StringComparer.Ordinal);
    private readonly GameLog? _log;
    private LocaleTable _current;
    private readonly LocaleTable _fallback;

    private Localizer(Dictionary<string, LocaleTable> locales, string language, string fallbackLanguage, GameLog? log)
    {
        _locales = locales;
        _log = log;
        _fallback = locales.TryGetValue(fallbackLanguage, out LocaleTable? fallback)
            ? fallback
            : throw new ContentException($"Fallback language '{fallbackLanguage}' has no locale file in '{LocalesFolder}/'.");
        _current = _fallback;
        SetLanguage(language);
    }

    public string Language => _current.Code;

    public IReadOnlyCollection<string> MissingKeys => _missingKeys;

    /// <summary>Available languages as (code, display name), sorted by code.</summary>
    public IReadOnlyList<(string Code, string Name)> Languages =>
        [.. _locales.Values.OrderBy(table => table.Code, StringComparer.Ordinal).Select(table => (table.Code, table.DisplayName))];

    public static Localizer Load(ContentRepository content, string language, string fallbackLanguage = "en", GameLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var locales = new Dictionary<string, LocaleTable>(StringComparer.Ordinal);
        foreach (string file in content.ListFiles(LocalesFolder, ".json"))
        {
            string code = Path.GetFileNameWithoutExtension(file);
            JsonObject json = content.LoadMergedJson(file);
            locales[code] = LocaleTable.FromJson(code, json, file);
        }

        return new Localizer(locales, language, fallbackLanguage, log);
    }

    /// <summary>Creates a localizer from in-memory tables (tests, tools).</summary>
    public static Localizer FromTables(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables, string language, string fallbackLanguage = "en")
    {
        ArgumentNullException.ThrowIfNull(tables);
        var locales = new Dictionary<string, LocaleTable>(StringComparer.Ordinal);
        foreach ((string code, IReadOnlyDictionary<string, string> strings) in tables)
        {
            var json = new JsonObject();
            foreach ((string key, string value) in strings)
            {
                json[key] = value;
            }

            locales[code] = LocaleTable.FromJson(code, json, $"{code} (memory)");
        }

        return new Localizer(locales, language, fallbackLanguage, null);
    }

    /// <summary>Switches language. Unknown codes keep the current language and return false.</summary>
    public bool SetLanguage(string code)
    {
        if (_locales.TryGetValue(code, out LocaleTable? table))
        {
            _current = table;
            return true;
        }

        _log?.Warn("i18n", "Unknown language, keeping current one", ("requested", code), ("current", _current.Code));
        return false;
    }

    public bool HasKey(string key) => _current.Strings.ContainsKey(key) || _fallback.Strings.ContainsKey(key);

    public string Get(string key) => Format(Lookup(key), ReadOnlySpan<(string, object)>.Empty);

    public string Get(string key, params ReadOnlySpan<(string Name, object Value)> arguments) => Format(Lookup(key), arguments);

    /// <summary>Selects the plural form of <paramref name="key"/> for <paramref name="count"/>; <c>{count}</c> is available as a parameter.</summary>
    public string GetPlural(string key, long count, params ReadOnlySpan<(string Name, object Value)> arguments)
    {
        string category = _current.PluralRule.Category(count);
        string pluralKey = $"{key}.{category}";
        string template = _current.Strings.TryGetValue(pluralKey, out string? value)
            ? value
            : Lookup(_fallback.Strings.ContainsKey(pluralKey) ? pluralKey : $"{key}.other");
        var all = new (string, object)[arguments.Length + 1];
        all[0] = ("count", count);
        arguments.CopyTo(all.AsSpan(1));
        return Format(template, all);
    }

    private string Lookup(string key)
    {
        if (_current.Strings.TryGetValue(key, out string? value))
        {
            return value;
        }

        if (_fallback.Strings.TryGetValue(key, out value))
        {
            RecordMissing($"{_current.Code}:{key}");
            return value;
        }

        RecordMissing(key);
        return $"[{key}]";
    }

    private void RecordMissing(string key)
    {
        if (_missingKeys.Add(key))
        {
            _log?.Warn("i18n", "Missing translation", ("key", key));
        }
    }

    private string Format(string template, ReadOnlySpan<(string Name, object Value)> arguments)
    {
        if (template.IndexOf('{', StringComparison.Ordinal) < 0)
        {
            return template;
        }

        var builder = new StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                builder.Append('{');
                i += 2;
                continue;
            }

            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                builder.Append('}');
                i += 2;
                continue;
            }

            int end = c == '{' ? template.IndexOf('}', i + 1) : -1;
            if (end < 0)
            {
                builder.Append(c);
                i++;
                continue;
            }

            string name = template[(i + 1)..end];
            builder.Append(FindArgument(arguments, name) ?? $"{{{name}}}");
            i = end + 1;
        }

        return builder.ToString();
    }

    private string? FindArgument(ReadOnlySpan<(string Name, object Value)> arguments, string name)
    {
        foreach ((string argumentName, object value) in arguments)
        {
            if (string.Equals(argumentName, name, StringComparison.Ordinal))
            {
                return value is IFormattable formattable
                    ? formattable.ToString(null, _current.Culture)
                    : value.ToString();
            }
        }

        RecordMissing($"argument '{name}'");
        return null;
    }

    private sealed class LocaleTable
    {
        private LocaleTable(string code, string displayName, PluralRule pluralRule, Dictionary<string, string> strings)
        {
            Code = code;
            DisplayName = displayName;
            PluralRule = pluralRule;
            Strings = strings;
            Culture = CreateCulture(code);
        }

        public string Code { get; }

        public string DisplayName { get; }

        public PluralRule PluralRule { get; }

        public Dictionary<string, string> Strings { get; }

        public CultureInfo Culture { get; }

        public static LocaleTable FromJson(string code, JsonObject json, string file)
        {
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            string displayName = code;
            PluralRule rule = PluralRule.OneIsSingular;
            foreach (KeyValuePair<string, JsonNode?> entry in json)
            {
                if (entry.Value is not JsonValue value || !value.TryGetValue(out string? text))
                {
                    throw new ContentException($"Locale file '{file}': value of '{entry.Key}' must be a string.");
                }

                if (entry.Key == NameKey)
                {
                    displayName = text;
                }
                else if (entry.Key == PluralKey)
                {
                    if (!PluralRules.TryParse(text, out rule))
                    {
                        throw new ContentException($"Locale file '{file}': unknown plural rule '{text}' (use 'one', 'zero-one' or 'none').");
                    }
                }
                else
                {
                    strings[entry.Key] = text;
                }
            }

            return new LocaleTable(code, displayName, rule, strings);
        }

        private static CultureInfo CreateCulture(string code)
        {
            try
            {
                return CultureInfo.GetCultureInfo(code);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
