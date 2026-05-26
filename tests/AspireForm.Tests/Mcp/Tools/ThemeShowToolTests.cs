using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ThemeShowToolTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"af-theme-tool-{Guid.NewGuid():N}");

    public ThemeShowToolTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ThemeShowTool_metadata()
    {
        var tool = new ThemeShowTool(".");
        tool.Name.Should().Be("aspireform_theme_show");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ThemeShowTool_returns_active_theme_info_with_all_tokens()
    {
        var tool = new ThemeShowTool(_dir);
        var result = await tool.ExecuteAsync([], TestContext.Current.CancellationToken);
        result.IsError.Should().BeFalse();

        var json = result.Content[0].Text;
        var node = JsonNode.Parse(json) as JsonObject;
        node.Should().NotBeNull();
        node!["activeName"].Should().NotBeNull();
        node["darkMode"].Should().NotBeNull();
        node["allThemes"].Should().NotBeNull();
        node["tokens"].Should().NotBeNull();
        node["radius"].Should().NotBeNull();

        // Verify light tokens include all 19 known names.
        var lightTokens = node["tokens"]!["light"] as JsonObject;
        lightTokens.Should().NotBeNull();
        lightTokens!["background"].Should().NotBeNull(because: "'background' token must be present");
        lightTokens!["primary"].Should().NotBeNull(because: "'primary' token must be present");
        lightTokens.Count.Should().BeGreaterThanOrEqualTo(19);
    }

    [Fact]
    public async Task ThemeShowTool_uses_projectDir_arg_when_supplied()
    {
        var altDir = Path.Combine(_dir, "alt");
        Directory.CreateDirectory(altDir);

        var tool = new ThemeShowTool(_dir); // default to _dir
        var result = await tool.ExecuteAsync(new JsonObject { ["projectDir"] = altDir }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeFalse();

        var json = result.Content[0].Text;
        var node = JsonNode.Parse(json) as JsonObject;
        node!["activeName"].Should().NotBeNull();
    }
}
