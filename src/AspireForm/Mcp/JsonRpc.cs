using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>JSON-RPC 2.0 envelope helpers. AspireForm's MCP server speaks the minimum JSON-RPC needed for the MCP tools subset (initialize, tools/list, tools/call).</summary>
public static class JsonRpc
{
    /// <summary>Standard JSON-RPC parse-error code.</summary>
    public const int ParseError = -32700;

    /// <summary>Standard JSON-RPC method-not-found code.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Standard JSON-RPC invalid-params code.</summary>
    public const int InvalidParams = -32602;

    /// <summary>Standard JSON-RPC internal-error code.</summary>
    public const int InternalError = -32603;

    /// <summary>Server-defined error code for known AspireForm exceptions.</summary>
    public const int AspireFormServerError = -32001;

    /// <summary>A parsed JSON-RPC 2.0 request.</summary>
    public sealed record Request(JsonNode? Id, string Method, JsonObject? Params);

    /// <summary>Parses a single JSON-RPC request frame.</summary>
    /// <param name="json">UTF-8 JSON text containing one JSON-RPC request object.</param>
    /// <returns>The parsed request.</returns>
    /// <exception cref="JsonException">Thrown when the input is not a valid JSON-RPC request.</exception>
    public static Request ParseRequest(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("JSON-RPC request must be an object.");
        var method = node["method"]?.GetValue<string>()
            ?? throw new JsonException("JSON-RPC request is missing 'method'.");
        return new Request(
            Id: node["id"]?.DeepClone(),
            Method: method,
            Params: node["params"] as JsonObject);
    }

    /// <summary>Builds a successful JSON-RPC response.</summary>
    /// <param name="id">The request id to echo back. May be null for notifications, but notifications get no response.</param>
    /// <param name="result">The result payload.</param>
    /// <returns>A JsonObject ready to serialise to the transport.</returns>
    public static JsonObject Success(JsonNode? id, JsonNode? result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result,
    };

    /// <summary>Builds a JSON-RPC error response.</summary>
    /// <param name="id">The request id to echo back.</param>
    /// <param name="code">Error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="data">Optional error data payload.</param>
    /// <returns>A JsonObject ready to serialise to the transport.</returns>
    public static JsonObject Error(JsonNode? id, int code, string message, JsonNode? data = null)
    {
        var err = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };
        if (data is not null)
        {
            err["data"] = data;
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = err,
        };
    }
}
