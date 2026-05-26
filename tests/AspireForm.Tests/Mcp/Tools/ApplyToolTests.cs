using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ApplyToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new ApplyTool(".");
        tool.Name.Should().Be("aspireform_apply");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Apply_on_empty_config_reports_no_changes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new ApplyTool(dir);
            var result = await tool.ExecuteAsync([], TestContext.Current.CancellationToken);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No changes");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
