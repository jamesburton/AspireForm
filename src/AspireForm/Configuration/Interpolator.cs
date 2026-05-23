using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AspireForm.Configuration;

/// <summary>Substitutes <c>${VAR}</c> and <c>${VAR:-default}</c> placeholders in string values of a config DOM.</summary>
public static partial class Interpolator
{
    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::-(?<default>[^}]*))?\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Returns a new DOM with every string value interpolated against <paramref name="variables"/>.
    /// An undefined variable without a <c>:-default</c> throws <see cref="ConfigValidationException"/>.
    /// </summary>
    public static JsonObject Apply(JsonObject dom, IReadOnlyDictionary<string, string> variables)
    {
        return (JsonObject)Walk(dom.DeepClone(), variables)!;
    }

    /// <summary>Builds the variable map: <c>.env</c> values overlaid by process environment variables (process wins).</summary>
    public static IReadOnlyDictionary<string, string> BuildVariables(IReadOnlyDictionary<string, string> envFile)
    {
        var merged = new Dictionary<string, string>(envFile, StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            merged[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return merged;
    }

    private static JsonNode? Walk(JsonNode? node, IReadOnlyDictionary<string, string> variables)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    obj[key] = Walk(obj[key], variables);
                }

                return obj;
            }

            case JsonArray array:
            {
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = Walk(array[i], variables);
                }

                return array;
            }

            case JsonValue value when value.TryGetValue(out string? text):
                return JsonValue.Create(Substitute(text, variables));

            default:
                return node;
        }
    }

    private static string Substitute(string text, IReadOnlyDictionary<string, string> variables)
    {
        return PlaceholderRegex().Replace(text, match =>
        {
            var name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }

            if (match.Groups["default"].Success)
            {
                return match.Groups["default"].Value;
            }

            throw new ConfigValidationException(
                $"Configuration variable '{name}' is not defined and has no default (use ${{{name}:-default}}).");
        });
    }
}
