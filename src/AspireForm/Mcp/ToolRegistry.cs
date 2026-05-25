using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>Name-indexed collection of registered MCP tool handlers.</summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers = new(StringComparer.Ordinal);

    /// <summary>Registers a tool handler. Throws if a handler with the same name is already registered.</summary>
    /// <param name="handler">The handler to register.</param>
    /// <exception cref="InvalidOperationException">A handler with the same name is already registered.</exception>
    public void Register(IToolHandler handler)
    {
        if (!_handlers.TryAdd(handler.Name, handler))
        {
            throw new InvalidOperationException($"Tool '{handler.Name}' is already registered.");
        }
    }

    /// <summary>True when a handler with the supplied name is registered.</summary>
    public bool Contains(string name) => _handlers.ContainsKey(name);

    /// <summary>Retrieves the handler for <paramref name="name"/>, or null when not registered.</summary>
    public IToolHandler? Get(string name) =>
        _handlers.TryGetValue(name, out var h) ? h : null;

    /// <summary>All registered handlers in registration order.</summary>
    public IReadOnlyCollection<IToolHandler> All => _handlers.Values;

    /// <summary>Builds the JSON payload returned from MCP <c>tools/list</c>.</summary>
    public JsonObject ListToolsPayload()
    {
        var tools = new JsonArray();
        foreach (var h in _handlers.Values)
        {
            tools.Add(new JsonObject
            {
                ["name"] = h.Name,
                ["description"] = h.Description,
                ["inputSchema"] = h.InputSchema.DeepClone(),
            });
        }

        return new JsonObject { ["tools"] = tools };
    }
}
