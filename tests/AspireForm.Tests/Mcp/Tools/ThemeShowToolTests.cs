using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ThemeShowToolTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"af-theme-tool-{Guid.NewGuid():N}");

    public ThemeShowToolTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ThemeShowTool_metadata()
    {
        var tool = new ThemeShowTool(".");
        tool.Name.Should().Be("aspireform_theme_show");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ThemeShowTool_returns_all_14_default_tokens_when_no_theme_json()
    {
        var tool = new ThemeShowTool(_dir);
        var result = await tool.ExecuteAsync([], default);
        result.IsError.Should().BeFalse();

        var json = result.Content[0].Text;
        var tokens = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        tokens.Should().NotBeNull();
        tokens!.Should().ContainKey("color-primary");
        tokens["color-primary"].Should().Be("#1a73e8");
        tokens.Count.Should().BeGreaterThanOrEqualTo(14);
    }

    [Fact]
    public async Task ThemeShowTool_reflects_custom_override_from_theme_json()
    {
        var aspireformDir = Path.Combine(_dir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        await File.WriteAllTextAsync(Path.Combine(aspireformDir, "theme.json"),
            """{ "color-primary": "#ff1234" }""");

        var tool = new ThemeShowTool(_dir);
        var result = await tool.ExecuteAsync([], default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("#ff1234");
    }

    [Fact]
    public async Task ThemeShowTool_uses_projectDir_arg_when_supplied()
    {
        // Create a secondary dir with a custom theme.
        var altDir = Path.Combine(_dir, "alt");
        Directory.CreateDirectory(altDir);
        var aspireformDir = Path.Combine(altDir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        await File.WriteAllTextAsync(Path.Combine(aspireformDir, "theme.json"),
            """{ "color-bg": "#112233" }""");

        var tool = new ThemeShowTool(_dir); // default to _dir
        var result = await tool.ExecuteAsync(new JsonObject { ["projectDir"] = altDir }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("#112233");
    }
}
