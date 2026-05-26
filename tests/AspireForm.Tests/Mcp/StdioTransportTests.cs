using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class StdioTransportTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echo";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    [Fact]
    public async Task Reads_newline_delimited_requests_and_writes_responses()
    {
        var registry = new ToolRegistry();
        registry.Register(new EchoTool());
        var server = new McpServer(registry);

        var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""" + "\n" +
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hello"}}}""" + "\n");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, TestContext.Current.CancellationToken);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2);
        var first = JsonNode.Parse(lines[0]) as JsonObject;
        first!["result"]!["tools"]!.AsArray().Count.Should().Be(1);
        var second = JsonNode.Parse(lines[1]) as JsonObject;
        second!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("hello");
    }

    [Fact]
    public async Task Stops_at_EOF()
    {
        var server = new McpServer(new ToolRegistry());
        var input = new StringReader("");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, TestContext.Current.CancellationToken);

        output.ToString().Should().BeEmpty();
    }
}
