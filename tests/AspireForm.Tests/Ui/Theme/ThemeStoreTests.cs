using AspireForm.Ui.Theme;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"af-theme-{Guid.NewGuid():N}");

    public ThemeStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void GetTokens_returns_all_defaults_when_no_file_exists()
    {
        var store = new ThemeStore(_dir);
        var tokens = store.GetTokens();
        tokens.Should().ContainKey("color-primary");
        tokens["color-primary"].Should().Be("#1a73e8");
        tokens.Count.Should().BeGreaterThanOrEqualTo(14);
    }

    [Fact]
    public async Task SaveTokenAsync_persists_override_and_GetTokens_returns_it()
    {
        var store = new ThemeStore(_dir);
        await store.SaveTokenAsync("color-primary", "#ff0000");
        var tokens = store.GetTokens();
        tokens["color-primary"].Should().Be("#ff0000");
    }

    [Fact]
    public async Task SaveTokenAsync_creates_aspireform_directory_if_absent()
    {
        var dir = Path.Combine(_dir, "subdir");
        var store = new ThemeStore(dir);
        await store.SaveTokenAsync("color-primary", "#0000ff");
        File.Exists(Path.Combine(dir, ".aspireform", "theme.json")).Should().BeTrue();
    }

    [Fact]
    public async Task ResetToDefaultsAsync_removes_all_overrides()
    {
        var store = new ThemeStore(_dir);
        await store.SaveTokenAsync("color-primary", "#ff0000");
        await store.ResetToDefaultsAsync();
        store.GetTokens()["color-primary"].Should().Be("#1a73e8");
    }

    [Fact]
    public async Task Unknown_keys_in_json_are_preserved_on_roundtrip()
    {
        var aspireformDir = Path.Combine(_dir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        await File.WriteAllTextAsync(Path.Combine(aspireformDir, "theme.json"),
            """{ "color-primary": "#ff0000", "future-token": "#123456" }""");
        var store = new ThemeStore(_dir);
        await store.SaveTokenAsync("color-text", "#333333");
        var raw = await File.ReadAllTextAsync(Path.Combine(aspireformDir, "theme.json"));
        raw.Should().Contain("future-token");
    }

    [Fact]
    public void GetTokens_returns_defaults_when_file_is_malformed_json()
    {
        var aspireformDir = Path.Combine(_dir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        File.WriteAllText(Path.Combine(aspireformDir, "theme.json"), "{ not valid json");
        var store = new ThemeStore(_dir);
        var tokens = store.GetTokens();
        tokens["color-primary"].Should().Be("#1a73e8"); // default
    }

    [Fact]
    public async Task Concurrent_saves_do_not_corrupt_the_file()
    {
        var store = new ThemeStore(_dir);
        var tasks = Enumerable.Range(0, 10)
            .Select(i => store.SaveTokenAsync("color-primary", $"#{i:X6}"));
        await Task.WhenAll(tasks);

        // Verify file is valid JSON.
        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, ".aspireform", "theme.json"));
        var act = () => System.Text.Json.JsonDocument.Parse(raw);
        act.Should().NotThrow();
    }
}
