namespace AspireForm.Mcp;

/// <summary>A transport that hands the server one JSON-RPC frame at a time and writes responses back.</summary>
public interface ITransport
{
    /// <summary>Runs the transport read/dispatch loop until the input is closed or <paramref name="ct"/> fires.</summary>
    /// <param name="server">The server that dispatches each request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RunAsync(McpServer server, CancellationToken ct);
}
