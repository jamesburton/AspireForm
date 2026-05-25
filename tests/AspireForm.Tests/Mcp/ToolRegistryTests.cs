using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class ToolRegistryTests
{
    private sealed class FakeTool(string name) : IToolHandler
    {
        public string Name => name;
        public string Description => $"fake {name}";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok($"called {name}"));
    }

    [Fact]
    public void Register_then_Get_returns_handler()
    {
        var r = new ToolRegistry();
        var t = new FakeTool("aspireform_test");
        r.Register(t);
        r.Get("aspireform_test").Should().BeSameAs(t);
        r.Contains("aspireform_test").Should().BeTrue();
    }

    [Fact]
    public void Register_duplicate_throws()
    {
        var r = new ToolRegistry();
        r.Register(new FakeTool("dup"));
        var act = () => r.Register(new FakeTool("dup"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_missing_returns_null()
    {
        var r = new ToolRegistry();
        r.Get("missing").Should().BeNull();
    }

    [Fact]
    public void ListToolsPayload_emits_each_handler_metadata()
    {
        var r = new ToolRegistry();
        r.Register(new FakeTool("a"));
        r.Register(new FakeTool("b"));
        var payload = r.ListToolsPayload();
        var tools = payload["tools"] as JsonArray;
        tools.Should().NotBeNull();
        tools!.Count.Should().Be(2);
        tools[0]!["name"]!.GetValue<string>().Should().Be("a");
        tools[1]!["name"]!.GetValue<string>().Should().Be("b");
        tools[0]!["description"]!.GetValue<string>().Should().Be("fake a");
        (tools[0]!["inputSchema"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("object");
    }
}
