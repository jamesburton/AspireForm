using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class StateToolsTests
{
    [Fact]
    public void StateListTool_metadata()
    {
        var tool = new StateListTool(".");
        tool.Name.Should().Be("aspireform_state_list");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StateShowTool_metadata_requires_block()
    {
        var tool = new StateShowTool(".");
        tool.Name.Should().Be("aspireform_state_show");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("block");
    }

    [Fact]
    public async Task StateListTool_returns_no_blocks_message_for_empty_state()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new StateListTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No tracked blocks");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task StateShowTool_unknown_block_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-state-show-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new StateShowTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["block"] = "missing" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("is not tracked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
