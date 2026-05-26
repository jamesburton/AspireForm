using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class DoctorToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new DoctorTool();
        tool.Name.Should().Be("aspireform_doctor");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_at_least_one_check_in_the_report()
    {
        var tool = new DoctorTool();
        var result = await tool.ExecuteAsync([], TestContext.Current.CancellationToken);
        result.Content[0].Text.Should().NotBeNullOrWhiteSpace();
    }
}
