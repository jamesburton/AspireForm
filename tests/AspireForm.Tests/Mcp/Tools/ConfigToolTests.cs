using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ConfigToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new ConfigTool(".");
        tool.Name.Should().Be("aspireform_config");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_merged_config_as_indented_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");

        try
        {
            var tool = new ConfigTool(dir);
            var result = await tool.ExecuteAsync([], TestContext.Current.CancellationToken);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("\"project\": \"Demo\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Returns_tool_level_error_for_missing_config()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-config-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new ConfigTool(dir);
            var result = await tool.ExecuteAsync([], TestContext.Current.CancellationToken);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("Configuration error");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
