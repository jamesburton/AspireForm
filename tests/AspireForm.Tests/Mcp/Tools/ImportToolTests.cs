using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ImportToolTests
{
    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new ImportTool(".");
        tool.Name.Should().Be("aspireform_import");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("block");
    }

    [Fact]
    public async Task Missing_block_returns_tool_level_error()
    {
        var tool = new ImportTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), TestContext.Current.CancellationToken);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'block'");
    }
}
