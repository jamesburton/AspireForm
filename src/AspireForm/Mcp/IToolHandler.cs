using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>The contract every MCP tool implements. The MCP server dispatches <c>tools/call</c> requests to a registered handler matched by <see cref="Name"/>.</summary>
public interface IToolHandler
{
    /// <summary>The tool name surfaced to the agent (e.g. <c>aspireform_plan</c>).</summary>
    string Name { get; }

    /// <summary>Human-readable description surfaced to the agent.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's input arguments. Returned by <c>tools/list</c>.</summary>
    JsonObject InputSchema { get; }

    /// <summary>Executes the tool with the supplied arguments.</summary>
    /// <param name="args">Arguments matching <see cref="InputSchema"/>; never null (use an empty object when there are no args).</param>
    /// <param name="ct">Cancellation token from the transport.</param>
    /// <returns>The structured tool result.</returns>
    Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct);
}
