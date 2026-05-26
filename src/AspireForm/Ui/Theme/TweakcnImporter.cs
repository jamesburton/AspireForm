using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>Parses a tweakcn-exported JSON string into a <see cref="ThemeDefinition"/>.</summary>
public static class TweakcnImporter
{
    /// <summary>Parses <paramref name="json"/> as a tweakcn theme export.
    /// Accepts both HSL (<c>hsl(222 84% 5%)</c>) and oklch (<c>oklch(0.14 0.04 265)</c>) value formats.</summary>
    /// <param name="json">The raw JSON from tweakcn's "Copy code" export.</param>
    /// <param name="themeName">Display name for the imported theme.</param>
    /// <returns>A new <see cref="ThemeDefinition"/>.</returns>
    /// <exception cref="TweakcnImportException">If the JSON is malformed or missing required structure.</exception>
    public static ThemeDefinition Parse(string json, string themeName = "Imported Theme")
    {
        JsonObject root;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj)
                throw new TweakcnImportException("Root element must be a JSON object.");
            root = obj;
        }
        catch (JsonException ex)
        {
            throw new TweakcnImportException($"Malformed JSON: {ex.Message}", ex);
        }

        var light = ExtractTokens(root, "light");
        var dark = ExtractTokens(root, "dark");

        double radius = 0.5;
        if (root["radius"] is JsonValue rv && rv.TryGetValue<double>(out var r))
            radius = r;

        var description = root["description"]?.GetValue<string>() ?? "Imported from tweakcn";

        return new ThemeDefinition(
            Name: themeName,
            Description: description,
            Light: light,
            Dark: dark,
            Radius: radius);
    }

    private static IReadOnlyDictionary<string, string> ExtractTokens(JsonObject root, string bucket)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        /* tweakcn exports: { "tokens": { "light": { ... }, "dark": { ... } } }
           or flat:         { "light": { ... }, "dark": { ... } } */
        var bucketNode = root["tokens"]?[bucket] ?? root[bucket];
        if (bucketNode is not JsonObject obj) return tokens;

        foreach (var kv in obj)
        {
            if (kv.Value?.GetValueKind() == JsonValueKind.String)
                tokens[kv.Key] = kv.Value.GetValue<string>();
        }
        return tokens;
    }
}

/// <summary>Raised by <see cref="TweakcnImporter"/> when the import JSON is malformed or missing required fields.</summary>
public sealed class TweakcnImportException : Exception
{
    /// <summary>Initialises with a message.</summary>
    public TweakcnImportException(string message) : base(message) { }

    /// <summary>Initialises with a message and inner exception.</summary>
    public TweakcnImportException(string message, Exception inner) : base(message, inner) { }
}
