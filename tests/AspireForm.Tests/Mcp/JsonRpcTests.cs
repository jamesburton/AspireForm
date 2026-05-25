using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class JsonRpcTests
{
    [Fact]
    public void Parse_request_extracts_id_method_params()
    {
        var json = """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"x"}}""";
        var req = JsonRpc.ParseRequest(json);
        req.Id!.GetValue<int>().Should().Be(7);
        req.Method.Should().Be("tools/call");
        req.Params!["name"]!.GetValue<string>().Should().Be("x");
    }

    [Fact]
    public void Success_response_serialises_with_result()
    {
        var resp = JsonRpc.Success(JsonNode.Parse("3"), new JsonObject { ["ok"] = true });
        var json = resp.ToJsonString();
        json.Should().Contain("\"jsonrpc\":\"2.0\"")
            .And.Contain("\"id\":3")
            .And.Contain("\"result\":{\"ok\":true}");
    }

    [Fact]
    public void Error_response_serialises_with_code_and_message()
    {
        var resp = JsonRpc.Error(JsonNode.Parse("4"), -32601, "method not found");
        var json = resp.ToJsonString();
        json.Should().Contain("\"id\":4")
            .And.Contain("\"code\":-32601")
            .And.Contain("\"message\":\"method not found\"");
    }
}
