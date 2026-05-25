using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class PlanToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new PlanTool(".");
        tool.Name.Should().Be("aspireform_plan");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Empty_config_returns_no_changes_plan()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new PlanTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_config_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plan-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PlanTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
