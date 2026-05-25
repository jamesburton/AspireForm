using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Macros;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Macros;

public sealed class MacroToolsTests
{
    [Fact]
    public void ScaffoldAspireAppWithDataTool_metadata()
    {
        var tool = new ScaffoldAspireAppWithDataTool(".");
        tool.Name.Should().Be("scaffold_aspire_app_with_data");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("name");
    }

    [Fact]
    public async Task ScaffoldAspireAppWithDataTool_missing_name_returns_tool_level_error()
    {
        var tool = new ScaffoldAspireAppWithDataTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public void AddCacheLayerTool_metadata()
    {
        var tool = new AddCacheLayerTool(".");
        tool.Name.Should().Be("add_cache_layer");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddCacheLayerTool_missing_config_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new AddCacheLayerTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
