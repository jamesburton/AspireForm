using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpServerTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echoes input";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    private static McpServer NewServer(params IToolHandler[] handlers)
    {
        var r = new ToolRegistry();
        foreach (var h in handlers) r.Register(h);
        return new McpServer(r);
    }

    [Fact]
    public async Task Initialize_returns_protocol_version_and_capabilities()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["result"]!["protocolVersion"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["result"]!["capabilities"]!["tools"].Should().NotBeNull();
    }

    [Fact]
    public async Task ToolsList_returns_registered_handlers()
    {
        var server = NewServer(new EchoTool());
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        var tools = node!["result"]!["tools"] as JsonArray;
        tools!.Count.Should().Be(1);
        tools[0]!["name"]!.GetValue<string>().Should().Be("echo");
    }

    [Fact]
    public async Task ToolsCall_dispatches_to_handler_and_returns_content()
    {
        var server = NewServer(new EchoTool());
        var resp = await server.DispatchAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        var content = node!["result"]!["content"] as JsonArray;
        content![0]!["type"]!.GetValue<string>().Should().Be("text");
        content[0]!["text"]!.GetValue<string>().Should().Be("hi");
        node["result"]!["isError"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ToolsCall_unknown_tool_returns_internal_error()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync(
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"missing"}}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.InternalError);
        node["error"]!["message"]!.GetValue<string>().Should().Contain("missing");
    }

    [Fact]
    public async Task Unknown_method_returns_method_not_found()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":5,"method":"made/up"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.MethodNotFound);
    }

    [Fact]
    public async Task Parse_error_returns_parse_error_response_with_null_id()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("not json", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.ParseError);
        // JsonObject indexer returns C# null when the JSON value is null, so we can't call GetValueKind() on it.
        node.ContainsKey("id").Should().BeTrue();
        node["id"].Should().BeNull();
    }

    [Fact]
    public async Task Notification_with_no_id_returns_null()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","method":"tools/list"}""", default);
        resp.Should().BeNull();
    }
}
