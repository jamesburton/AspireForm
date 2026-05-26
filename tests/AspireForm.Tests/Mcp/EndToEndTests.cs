using System.Text.Json.Nodes;
using AspireForm.Cli;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class EndToEndTests
{
    [Fact]
    public async Task Initialize_then_tools_list_then_doctor_tools_call_round_trips_via_stdio()
    {
        var registry = McpCommand.BuildRegistry(".");
        var server = new McpServer(registry);

        var inputLines = new[]
        {
            """{"jsonrpc":"2.0","id":1,"method":"initialize"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"aspireform_doctor","arguments":{}}}""",
        };
        var input = new StringReader(string.Join('\n', inputLines) + "\n");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, default);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3);

        var initResp = JsonNode.Parse(lines[0]) as JsonObject;
        initResp!["result"]!["serverInfo"]!["name"]!.GetValue<string>().Should().Be("AspireForm");

        var listResp = JsonNode.Parse(lines[1]) as JsonObject;
        listResp!["result"]!["tools"]!.AsArray().Count.Should().Be(30); // 14 + 12 entity + 3 macros + 1 theme

        var callResp = JsonNode.Parse(lines[2]) as JsonObject;
        callResp!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }
}
