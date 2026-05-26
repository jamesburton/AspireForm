using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class HttpTransportTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echo";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    private static int FindFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task POST_mcp_messages_round_trips_a_tools_call()
    {
        var port = FindFreeTcpPort();
        var registry = new ToolRegistry();
        registry.Register(new EchoTool());
        var server = new McpServer(registry);
        var transport = new HttpTransport(port);

        using var cts = new CancellationTokenSource();
        var serverTask = transport.RunAsync(server, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var body = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""";
            var resp = await http.PostAsync("/mcp/messages",
                new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

            resp.IsSuccessStatusCode.Should().BeTrue();
            var text = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var node = JsonNode.Parse(text) as JsonObject;
            node!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("hi");
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task GET_root_returns_404()
    {
        var port = FindFreeTcpPort();
        var server = new McpServer(new ToolRegistry());
        var transport = new HttpTransport(port);

        using var cts = new CancellationTokenSource();
        var serverTask = transport.RunAsync(server, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var resp = await http.GetAsync("/", TestContext.Current.CancellationToken);
            ((int)resp.StatusCode).Should().Be(404);
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
