using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class NewToolTests
{
    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new NewTool(".");
        tool.Name.Should().Be("aspireform_new");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("name");
    }

    [Fact]
    public async Task Missing_name_returns_tool_level_error()
    {
        var tool = new NewTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task Existing_directory_returns_tool_level_error()
    {
        var outDir = Path.Combine(Path.GetTempPath(), $"af-mcp-new-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo"));
        try
        {
            var tool = new NewTool(outDir);
            var result = await tool.ExecuteAsync(new JsonObject { ["name"] = "Demo" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("Refusing to scaffold");
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }
}
