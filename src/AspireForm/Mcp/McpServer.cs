using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>Transport-agnostic MCP server. Handles the subset of JSON-RPC methods needed for an MCP tools-only server: <c>initialize</c>, <c>tools/list</c>, and <c>tools/call</c>.</summary>
public sealed class McpServer
{
    private readonly ToolRegistry _registry;

    /// <summary>Creates a server bound to the supplied registry.</summary>
    public McpServer(ToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Dispatches a single JSON-RPC request frame and returns the JSON-RPC response frame as a serialised string, or null for a notification (no id).</summary>
    /// <param name="requestJson">UTF-8 JSON text containing one JSON-RPC request object.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The serialised JSON-RPC response, or null when the request was a notification.</returns>
    public async Task<string?> DispatchAsync(string requestJson, CancellationToken ct)
    {
        JsonRpc.Request req;
        try
        {
            req = JsonRpc.ParseRequest(requestJson);
        }
        catch (JsonException ex)
        {
            return JsonRpc.Error(null, JsonRpc.ParseError, ex.Message).ToJsonString();
        }

        if (req.Id is null)
        {
            // Notification — fire-and-forget. We don't currently use notifications.
            return null;
        }

        JsonNode? result;
        try
        {
            result = req.Method switch
            {
                "initialize" => InitializeResult(),
                "tools/list" => _registry.ListToolsPayload(),
                "tools/call" => await CallToolAsync(req.Params, ct),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            return JsonRpc.Error(req.Id, JsonRpc.InternalError, ex.Message,
                new JsonObject { ["type"] = ex.GetType().Name }).ToJsonString();
        }

        if (result is null)
        {
            return JsonRpc.Error(req.Id, JsonRpc.MethodNotFound, $"Method not found: {req.Method}").ToJsonString();
        }

        return JsonRpc.Success(req.Id, result).ToJsonString();
    }

    private static JsonObject InitializeResult() => new()
    {
        ["protocolVersion"] = "2024-11-05",
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "AspireForm",
            ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        },
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject(),
        },
    };

    private async Task<JsonNode> CallToolAsync(JsonObject? @params, CancellationToken ct)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new InvalidOperationException("tools/call requires 'name'.");
        var args = @params?["arguments"] as JsonObject ?? [];
        var handler = _registry.Get(name)
            ?? throw new InvalidOperationException($"Unknown tool: {name}");

        var result = await handler.ExecuteAsync(args, ct);

        var content = new JsonArray();
        foreach (var c in result.Content)
        {
            content.Add(new JsonObject { ["type"] = c.Type, ["text"] = c.Text });
        }

        return new JsonObject
        {
            ["content"] = content,
            ["isError"] = result.IsError,
        };
    }
}
