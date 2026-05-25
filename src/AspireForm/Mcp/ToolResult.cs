namespace AspireForm.Mcp;

/// <summary>A single content chunk in a tool result. Mirrors the MCP "content" element.</summary>
/// <param name="Type">Content type. AspireForm tools always emit "text".</param>
/// <param name="Text">The text payload.</param>
public sealed record ToolContent(string Type, string Text);

/// <summary>Result of executing an MCP tool. <see cref="IsError"/> indicates a tool-level (recoverable) failure, distinct from a transport-level JSON-RPC error.</summary>
/// <param name="IsError">When true, the agent sees a tool-level failure but the JSON-RPC call still succeeded.</param>
/// <param name="Content">One or more content chunks describing the result or failure.</param>
public sealed record ToolResult(bool IsError, IReadOnlyList<ToolContent> Content)
{
    /// <summary>Convenience factory for a single-text success result.</summary>
    public static ToolResult Ok(string text) => new(false, [new ToolContent("text", text)]);

    /// <summary>Convenience factory for a single-text tool-level error result.</summary>
    public static ToolResult Fail(string text) => new(true, [new ToolContent("text", text)]);
}
