using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class PluginToolsTests
{
    [Fact]
    public void All_four_tools_have_aspireform_plugin_prefix()
    {
        new PluginListTool(".").Name.Should().Be("aspireform_plugin_list");
        new PluginInstallTool(".").Name.Should().Be("aspireform_plugin_install");
        new PluginUpdateTool(".").Name.Should().Be("aspireform_plugin_update");
        new PluginRemoveTool(".").Name.Should().Be("aspireform_plugin_remove");
    }

    [Fact]
    public async Task PluginListTool_empty_lockfile_returns_no_plugins_message()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plug-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PluginListTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No plugins installed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PluginInstallTool_missing_name_returns_tool_level_error()
    {
        var tool = new PluginInstallTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task PluginUpdateTool_missing_name_returns_tool_level_error()
    {
        var tool = new PluginUpdateTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task PluginRemoveTool_unknown_plugin_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plug-rm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PluginRemoveTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["name"] = "DoesNotExist" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("not installed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
