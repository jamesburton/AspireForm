using System.Collections;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace AspireForm.Configuration;

/// <summary>Parses YAML configuration text into the same <see cref="JsonObject"/> DOM that <see cref="JsoncConfigParser"/> produces.</summary>
public sealed class YamlConfigParser : IConfigParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    /// <inheritdoc />
    public JsonObject Parse(string text)
    {
        object? graph;
        try
        {
            graph = Deserializer.Deserialize<object?>(text);
        }
        catch (YamlException ex)
        {
            throw new ConfigValidationException($"Invalid YAML configuration: {ex.Message}", ex);
        }

        if (graph is null)
        {
            // An empty document is treated as an empty configuration object.
            return new JsonObject();
        }

        var node = ConvertToJsonNode(graph);
        if (node is not JsonObject obj)
        {
            throw new ConfigValidationException("The configuration root must be a mapping.");
        }

        return obj;
    }

    private static JsonNode? ConvertToJsonNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;

            // Mappings must be checked before IEnumerable since IDictionary<object,object> is also enumerable.
            case IDictionary<object, object> map:
            {
                var obj = new JsonObject();
                foreach (var (key, item) in map)
                {
                    obj[key?.ToString() ?? string.Empty] = ConvertToJsonNode(item);
                }

                return obj;
            }

            // String must be checked before IEnumerable — string implements IEnumerable<char>, not IEnumerable<object>,
            // but the non-generic IEnumerable check below would match it. Explicit string case prevents that.
            case string s:
                return JsonValue.Create(s);

            case IEnumerable sequence:
            {
                var array = new JsonArray();
                foreach (var item in sequence)
                {
                    array.Add(ConvertToJsonNode(item));
                }

                return array;
            }

            case bool b:
                return JsonValue.Create(b);

            // For numeric types, parse via JsonNode.Parse so the backing store is a JsonElement — the same
            // representation that System.Text.Json produces when parsing JSON text. JsonElement-backed nodes
            // support widening/narrowing conversions (e.g. GetValue<int>() on a node storing 1 as long),
            // whereas JsonValue.Create<long>() produces a CLR-typed node locked to that exact CLR type.
            case byte or sbyte or short or ushort or int or uint or long:
                return JsonNode.Parse(Convert.ToInt64(value).ToString())!;

            case ulong ul:
                return JsonNode.Parse(ul.ToString())!;

            case float or double:
                return JsonNode.Parse(Convert.ToDouble(value).ToString("R", System.Globalization.CultureInfo.InvariantCulture))!;

            case decimal d:
                return JsonNode.Parse(d.ToString(System.Globalization.CultureInfo.InvariantCulture))!;

            default:
                return JsonValue.Create(value.ToString());
        }
    }
}
