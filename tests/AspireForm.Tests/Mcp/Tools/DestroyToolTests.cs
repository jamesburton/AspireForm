using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class DestroyToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new DestroyTool(".");
        tool.Name.Should().Be("aspireform_destroy");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Empty_state_destroy_all_reports_nothing_to_destroy()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-destroy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new DestroyTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("Nothing to destroy");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Unknown_block_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-destroy-unk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new DestroyTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["block"] = "missing" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("not tracked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
