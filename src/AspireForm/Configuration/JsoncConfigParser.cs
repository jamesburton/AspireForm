using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Parses JSON-with-comments configuration text into a <see cref="JsonObject"/>.</summary>
public sealed class JsoncConfigParser : IConfigParser
{
    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = false };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <inheritdoc />
    public JsonObject Parse(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, NodeOptions, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException($"Invalid JSONC configuration: {ex.Message}", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new ConfigValidationException("The configuration root must be an object.");
        }

        return obj;
    }
}
