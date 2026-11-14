using System.Text.Json.Nodes;

namespace Arcade.Core.Content;

/// <summary>
/// The single merge rule used for every JSON data file (JSON Merge Patch, RFC 7386):
/// objects are merged key by key, recursively; any other value (number, string, array) replaces the
/// previous one; an explicit <c>null</c> removes the key.
/// </summary>
public static class JsonMerge
{
    public static void Apply(JsonObject target, JsonObject patch)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(patch);
        foreach (KeyValuePair<string, JsonNode?> property in patch)
        {
            if (property.Value is null)
            {
                target.Remove(property.Key);
            }
            else if (property.Value is JsonObject patchObject && target[property.Key] is JsonObject targetObject)
            {
                Apply(targetObject, patchObject);
            }
            else
            {
                target[property.Key] = property.Value.DeepClone();
            }
        }
    }
}
