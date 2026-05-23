using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Deep-merges configuration override DOMs onto a base DOM.</summary>
public static class JsonObjectMerge
{
    /// <summary>
    /// Returns a new object: <paramref name="overrideObj"/> deep-merged onto <paramref name="baseObj"/>.
    /// Mappings merge recursively; arrays and scalars replace; an explicit null override removes the key.
    /// Neither input is mutated.
    /// </summary>
    public static JsonObject Merge(JsonObject baseObj, JsonObject overrideObj)
    {
        var result = (JsonObject)baseObj.DeepClone();

        foreach (var (key, overrideValue) in overrideObj)
        {
            if (overrideValue is null)
            {
                result.Remove(key);
                continue;
            }

            if (result.TryGetPropertyValue(key, out var baseValue)
                && baseValue is JsonObject baseChild
                && overrideValue is JsonObject overrideChild)
            {
                result[key] = Merge(baseChild, overrideChild);
            }
            else
            {
                result[key] = overrideValue.DeepClone();
            }
        }

        return result;
    }
}
