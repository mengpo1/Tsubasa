using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Arcade.Core.Content;

/// <summary>Shared JSON settings for game data. Unknown properties are rejected so that typos in mods are reported.</summary>
public static class ContentJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    public static JsonNodeOptions NodeOptions { get; } = new() { PropertyNameCaseInsensitive = false };

    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static JsonNode? Parse(string text) => JsonNode.Parse(text, NodeOptions, DocumentOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>Implemented by data types that check their own values after deserialisation.</summary>
public interface IContentData
{
    /// <summary>Appends human-readable problems (empty when valid).</summary>
    void Validate(ICollection<string> problems);
}

/// <summary>Helpers for writing <see cref="IContentData.Validate"/> implementations.</summary>
public static class DataChecks
{
    public static void Positive(ICollection<string> problems, string name, float value)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (!(value > 0f) || float.IsInfinity(value))
        {
            problems.Add($"{name} must be a positive number (found {value})");
        }
    }

    public static void NonNegative(ICollection<string> problems, string name, float value)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (!(value >= 0f) || float.IsInfinity(value))
        {
            problems.Add($"{name} must be zero or positive (found {value})");
        }
    }

    public static void InRange(ICollection<string> problems, string name, float value, float min, float max)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (!(value >= min && value <= max))
        {
            problems.Add($"{name} must be between {min} and {max} (found {value})");
        }
    }

    public static void NotEmpty(ICollection<string> problems, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{name} must not be empty");
        }
    }

    public static void Nested(ICollection<string> problems, string prefix, IContentData? data)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (data is null)
        {
            problems.Add($"{prefix} is missing");
            return;
        }

        var inner = new List<string>();
        data.Validate(inner);
        foreach (string problem in inner)
        {
            problems.Add($"{prefix}.{problem}");
        }
    }
}
