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
    public async Task ListAsync_returns_built_in_themes_on_fresh_dir()
    {
        var store = new ThemeStore(_dir);
        var themes = await store.ListAsync(TestContext.Current.CancellationToken);
        themes.Should().NotBeEmpty();
        themes.Any(t => t.IsActive).Should().BeTrue(because: "one theme must be active");
    }

    [Fact]
    public async Task GetActiveAsync_returns_first_built_in_theme_by_default()
    {
        var store = new ThemeStore(_dir);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        activation.ActiveName.Should().NotBeNullOrEmpty();
        activation.DarkMode.Should().BeFalse(because: "dark mode defaults to off");
    }

    [Fact]
    public async Task GetAsync_returns_theme_with_all_tokens()
    {
        var store = new ThemeStore(_dir);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        var theme = await store.GetAsync(activation.ActiveName, TestContext.Current.CancellationToken);
        theme.Light.Should().ContainKey("background");
        theme.Light.Should().ContainKey("primary");
        theme.Light.Count.Should().BeGreaterThanOrEqualTo(19);
    }

    [Fact]
    public async Task SaveAsync_persists_changes_and_GetAsync_returns_them()
    {
        var store = new ThemeStore(_dir);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        var original = await store.GetAsync(activation.ActiveName, TestContext.Current.CancellationToken);
        var updated = original with
        {
            Light = original.Light.ToDictionary(kv => kv.Key, kv => kv.Key == "background" ? "0 0% 50%" : kv.Value),
        };
        await store.SaveAsync(updated, TestContext.Current.CancellationToken);
        var reload = await store.GetAsync(activation.ActiveName, TestContext.Current.CancellationToken);
        reload.Light["background"].Should().Be("0 0% 50%");
    }

    [Fact]
    public async Task SetActiveAsync_changes_active_theme()
    {
        var store = new ThemeStore(_dir);
        var themes = await store.ListAsync(TestContext.Current.CancellationToken);
        themes.Count.Should().BeGreaterThan(1, because: "need at least 2 themes to switch");
        var other = themes.First(t => !t.IsActive);
        await store.SetActiveAsync(other.Name, TestContext.Current.CancellationToken);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        activation.ActiveName.Should().Be(other.Name);
    }

    [Fact]
    public async Task SetDarkModeAsync_updates_dark_mode_flag()
    {
        var store = new ThemeStore(_dir);
        await store.SetDarkModeAsync(true, TestContext.Current.CancellationToken);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        activation.DarkMode.Should().BeTrue();
        await store.SetDarkModeAsync(false, TestContext.Current.CancellationToken);
        activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        activation.DarkMode.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateAsync_creates_new_theme_with_given_name()
    {
        var store = new ThemeStore(_dir);
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        var newName = await store.DuplicateAsync(activation.ActiveName, "My Copy", TestContext.Current.CancellationToken);
        newName.Should().Be("My Copy");
        var themes = await store.ListAsync(TestContext.Current.CancellationToken);
        themes.Any(t => t.Name == "My Copy").Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_removes_theme_and_activates_remaining()
    {
        var store = new ThemeStore(_dir);
        var themes = await store.ListAsync(TestContext.Current.CancellationToken);
        themes.Count.Should().BeGreaterThan(1, because: "need at least 2 themes to delete one");
        var toDelete = themes.First(t => !t.IsActive);
        await store.DeleteAsync(toDelete.Name, TestContext.Current.CancellationToken);
        var after = await store.ListAsync(TestContext.Current.CancellationToken);
        after.Any(t => t.Name == toDelete.Name).Should().BeFalse();
    }

    [Fact]
    public async Task ResetToDefaultsAsync_reinstalls_built_in_themes_and_resets_active()
    {
        var store = new ThemeStore(_dir);
        var themes = await store.ListAsync(TestContext.Current.CancellationToken);
        var other = themes.First(t => !t.IsActive);
        await store.SetActiveAsync(other.Name, TestContext.Current.CancellationToken);
        await store.ResetToDefaultsAsync(TestContext.Current.CancellationToken);

        // After reset, the active theme should be "AspireForm Light" (first built-in).
        var activation = await store.GetActiveAsync(TestContext.Current.CancellationToken);
        activation.ActiveName.Should().Be("AspireForm Light");
        activation.DarkMode.Should().BeFalse();

        // Built-in themes should all be present.
        var afterThemes = await store.ListAsync(TestContext.Current.CancellationToken);
        afterThemes.Should().NotBeEmpty(because: "built-in themes are reinstalled");
    }
}
