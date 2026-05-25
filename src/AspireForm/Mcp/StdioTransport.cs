namespace AspireForm.Mcp;

/// <summary>JSON-RPC transport over stdin/stdout, framed as newline-delimited JSON (one request per line, one response per line).</summary>
public sealed class StdioTransport : ITransport
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    /// <summary>Creates a stdio transport bound to the process stdin/stdout.</summary>
    public StdioTransport() : this(Console.In, Console.Out) { }

    /// <summary>Creates a stdio transport bound to the supplied reader/writer (used by tests).</summary>
    public StdioTransport(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    /// <inheritdoc />
    public async Task RunAsync(McpServer server, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _input.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (line is null)
            {
                return; // EOF — the client disconnected.
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = await server.DispatchAsync(line, ct);
            if (response is not null)
            {
                await _output.WriteLineAsync(response.AsMemory(), ct);
                await _output.FlushAsync(ct);
            }
        }
    }
}
