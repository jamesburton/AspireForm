using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class AddToolTests
{
    private static string MakeTempProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-add-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        return dir;
    }

    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new AddTool(".");
        tool.Name.Should().Be("aspireform_add");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("type");
    }

    [Fact]
    public async Task Adds_resource_block()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["type"] = "sqlserver" }, default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("Added resource 'sqlserver'");
            File.ReadAllText(Path.Combine(dir, "aspireform.yaml")).Should().Contain("sqlserver");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Adds_module_block_with_dependsOn()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            var args = new JsonObject
            {
                ["type"] = "ef-data",
                ["name"] = "data",
                ["module"] = true,
                ["dependsOn"] = new JsonArray("sql"),
            };
            var result = await tool.ExecuteAsync(args, default);
            result.IsError.Should().BeFalse();
            var yaml = File.ReadAllText(Path.Combine(dir, "aspireform.yaml"));
            yaml.Should().Contain("data:").And.Contain("ef-data").And.Contain("sql");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Duplicate_block_returns_tool_level_error()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            await tool.ExecuteAsync(new JsonObject { ["type"] = "redis" }, default);
            var second = await tool.ExecuteAsync(new JsonObject { ["type"] = "redis" }, default);
            second.IsError.Should().BeTrue();
            second.Content[0].Text.Should().Contain("already exists");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
