# AspireForm Theme Editor — Plan 5.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AspireForm 0.7.0` adding a Theme Editor panel to the existing `aspireform ui` Blazor Server app. The editor lets users adjust 14 CSS design tokens, persists them to `.aspireform/theme.json`, and live-previews via a dynamic `/theme.css` Kestrel endpoint.

**Architecture:** `IThemeStore` (singleton DI) reads/writes `{ProjectDir}/.aspireform/theme.json`. A `MapGet("/theme.css", ...)` endpoint in UiHost converts the token map to `:root { --af-*: ... }` CSS at request time. `site.css` is refactored to `var(--af-*)` throughout. `ThemeTokenEditor.razor` (bUnit-tested) drives the editor UI. JS interop (`theme-interop.js`) forces a CSS link reload after each token save. One read-only MCP tool `aspireform_theme_show` is added.

**Tech stack:** .NET 10, Blazor Server (existing), xUnit v3.2.2 on MTP, AwesomeAssertions 9.4.0, bUnit 1.40.0. No new NuGet packages required.

**Run tests:** `dotnet run --project tests/AspireForm.Tests`

**Important gotchas (from #4a execution):**
- Always verify with `git log -1 --oneline` before reporting DONE.
- `_Imports.razor` needs `@using` for new component namespaces; the Razor compiler does NOT auto-discover them.
- `App.razor` uses `<HeadContent>` / `<head>` — add the theme link carefully; don't break the existing layout.
- Every commit uses `git -c commit.gpgsign=false commit`.

---

## File map

**New (production):**
- `src/AspireForm/Ui/Theme/ThemeToken.cs`
- `src/AspireForm/Ui/Theme/ThemeDefaults.cs`
- `src/AspireForm/Ui/Theme/IThemeStore.cs`
- `src/AspireForm/Ui/Theme/ThemeStore.cs`
- `src/AspireForm/Ui/Components/Pages/Theme.razor`
- `src/AspireForm/Ui/Components/Theme/ThemeTokenEditor.razor`
- `src/AspireForm/Ui/wwwroot/theme-interop.js`
- `src/AspireForm/Mcp/Tools/ThemeShowTool.cs`

**Modified:**
- `src/AspireForm/Ui/UiHost.cs`
- `src/AspireForm/Ui/Components/App.razor`
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor`
- `src/AspireForm/Ui/Components/_Imports.razor`
- `src/AspireForm/Ui/wwwroot/site.css`
- `src/AspireForm/AspireForm.csproj`
- `CHANGELOG.md`
- `README.md`

**New (tests):**
- `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemeCssEndpointTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemeTokenEditorTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemePageTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ThemeShowToolTests.cs`

---

## Tasks

### Task 1 — ThemeToken model + ThemeDefaults

**Files:**
- `src/AspireForm/Ui/Theme/ThemeToken.cs` (NEW)
- `src/AspireForm/Ui/Theme/ThemeDefaults.cs` (NEW)

**Spec:** Define the token vocabulary. `ThemeToken` is an immutable record. `ThemeDefaults` is a static class exposing the complete array of 14 tokens.

```csharp
// src/AspireForm/Ui/Theme/ThemeToken.cs
namespace AspireForm.Ui.Theme;

/// <summary>A single CSS design token managed by the theme editor.</summary>
/// <param name="Name">The token name used as a key in <c>theme.json</c> (e.g., <c>"color-primary"</c>).</param>
/// <param name="CssVar">The CSS custom property name (e.g., <c>"--af-color-primary"</c>).</param>
/// <param name="DefaultValue">The fallback hex color value when no override is stored.</param>
/// <param name="Label">Human-readable display label shown in the editor.</param>
public sealed record ThemeToken(string Name, string CssVar, string DefaultValue, string Label);
```

```csharp
// src/AspireForm/Ui/Theme/ThemeDefaults.cs
namespace AspireForm.Ui.Theme;

/// <summary>The complete set of CSS design tokens defined for AspireForm v1.</summary>
public static class ThemeDefaults
{
    /// <summary>All 14 tokens in display order.</summary>
    public static readonly IReadOnlyList<ThemeToken> Tokens =
    [
        new("color-primary",       "--af-color-primary",       "#1a73e8", "Primary accent / links"),
        new("color-primary-light", "--af-color-primary-light", "#e8f0fe", "Selected item highlight"),
        new("color-text",          "--af-color-text",          "#222222", "Default body text"),
        new("color-text-muted",    "--af-color-text-muted",    "#888888", "De-emphasised text"),
        new("color-text-sub",      "--af-color-text-sub",      "#666666", "Topbar sub-label"),
        new("color-bg",            "--af-color-bg",            "#ffffff", "Page background"),
        new("color-bg-surface",    "--af-color-bg-surface",    "#fafafa", "Topbar / tab-bar background"),
        new("color-bg-sidebar",    "--af-color-bg-sidebar",    "#fcfcfc", "Sidebar background"),
        new("color-bg-hover",      "--af-color-bg-hover",      "#f4f4f4", "Hover state background"),
        new("color-border",        "--af-color-border",        "#dddddd", "Main borders"),
        new("color-border-light",  "--af-color-border-light",  "#eeeeee", "Lighter borders"),
        new("color-danger-bg",     "--af-color-danger-bg",     "#ffeeee", "Danger button background"),
        new("color-danger-text",   "--af-color-danger-text",   "#aa0000", "Danger button text"),
        new("color-banner-bg",     "--af-color-banner-bg",     "#fff3cd", "Warning banner background"),
    ];
}
```

**Tests:** None yet for these pure data types — tested indirectly via ThemeStore tests.

**Commit:** `feat: add ThemeToken model and ThemeDefaults vocabulary`

---

### Task 2 — IThemeStore interface

**Files:**
- `src/AspireForm/Ui/Theme/IThemeStore.cs` (NEW)

**Spec:** DI seam. Implementations return the merged token map (defaults + persisted overrides).

```csharp
// src/AspireForm/Ui/Theme/IThemeStore.cs
namespace AspireForm.Ui.Theme;

/// <summary>Reads and writes the active theme token values for the current AspireForm project.</summary>
public interface IThemeStore
{
    /// <summary>Returns the merged token map: default values overridden by any persisted values.
    /// Keys are token names (e.g., <c>"color-primary"</c>); values are hex strings.</summary>
    IReadOnlyDictionary<string, string> GetTokens();

    /// <summary>Persists a single token override to <c>.aspireform/theme.json</c>.</summary>
    /// <param name="name">Token name (must match a name in <see cref="ThemeDefaults.Tokens"/>).</param>
    /// <param name="value">Hex color value (e.g., <c>"#1a73e8"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveTokenAsync(string name, string value, CancellationToken ct = default);

    /// <summary>Deletes all persisted overrides, restoring all tokens to their defaults.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
```

**Commit:** `feat: add IThemeStore interface`

---

### Task 3 — ThemeStore implementation + tests

**Files:**
- `src/AspireForm/Ui/Theme/ThemeStore.cs` (NEW)
- `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs` (NEW)

**Spec:** Reads `{ProjectDir}/.aspireform/theme.json` on first `GetTokens()` call (lazy). Writes on every `SaveTokenAsync`. Unknown keys in JSON are preserved. Thread safety via `SemaphoreSlim(1,1)`.

```csharp
// src/AspireForm/Ui/Theme/ThemeStore.cs
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>File-backed implementation of <see cref="IThemeStore"/>.
/// Persists overrides to <c>.aspireform/theme.json</c> in the project directory.</summary>
internal sealed class ThemeStore : IThemeStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, string>? _overrides; // null = not yet loaded

    /// <summary>Initialises the store for the given project directory.</summary>
    public ThemeStore(string projectDir)
    {
        _filePath = Path.Combine(projectDir, ".aspireform", "theme.json");
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetTokens()
    {
        EnsureLoaded();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        // Start with defaults.
        foreach (var token in ThemeDefaults.Tokens)
            result[token.Name] = token.DefaultValue;
        // Apply persisted overrides.
        foreach (var kv in _overrides!)
            result[kv.Key] = kv.Value;
        return result;
    }

    /// <inheritdoc/>
    public async Task SaveTokenAsync(string name, string value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            _overrides![name] = value;
            await WriteAsync(ct);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _overrides = [];
            await WriteAsync(ct);
        }
        finally { _lock.Release(); }
    }

    private void EnsureLoaded()
    {
        if (_overrides is not null) return;
        _overrides = [];
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                foreach (var kv in obj)
                {
                    if (kv.Value?.GetValueKind() == JsonValueKind.String)
                        _overrides[kv.Key] = kv.Value.GetValue<string>();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed file — treat as empty overrides, don't crash.
            _overrides = [];
        }
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var obj = new JsonObject();
        foreach (var kv in _overrides!)
            obj[kv.Key] = JsonValue.Create(kv.Value);
        var json = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
```

**Tests:**

```csharp
// tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs
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
            .Select(i => store.SaveTokenAsync($"color-primary", $"#{i:X6}"));
        await Task.WhenAll(tasks);
        // Verify file is valid JSON.
        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, ".aspireform", "theme.json"));
        var act = () => System.Text.Json.JsonDocument.Parse(raw);
        act.Should().NotThrow();
    }
}
```

**Commit:** `feat: add ThemeStore implementation with tests`

---

### Task 4 — Refactor site.css to CSS custom properties

**Files:**
- `src/AspireForm/Ui/wwwroot/site.css` (MODIFY)

**Spec:** Replace every hard-coded color value with the appropriate `var(--af-*)` reference.
Prepend a `:root { }` block with fallback values (so the file still works as a standalone stylesheet
even when `/theme.css` isn't loaded). The fallback block mirrors `ThemeDefaults`.

The refactored `site.css` must start with:

```css
/* AspireForm UI — design tokens. Override via /theme.css (theme editor). */
:root {
  --af-color-primary:       #1a73e8;
  --af-color-primary-light: #e8f0fe;
  --af-color-text:          #222222;
  --af-color-text-muted:    #888888;
  --af-color-text-sub:      #666666;
  --af-color-bg:            #ffffff;
  --af-color-bg-surface:    #fafafa;
  --af-color-bg-sidebar:    #fcfcfc;
  --af-color-bg-hover:      #f4f4f4;
  --af-color-border:        #dddddd;
  --af-color-border-light:  #eeeeee;
  --af-color-danger-bg:     #ffeeee;
  --af-color-danger-text:   #aa0000;
  --af-color-banner-bg:     #fff3cd;
}
```

Then replace all usages:
- `#222` / `#222222` → `var(--af-color-text)`
- `#1a73e8` → `var(--af-color-primary)`
- `#e8f0fe` → `var(--af-color-primary-light)`
- `#fff` / `#ffffff` → `var(--af-color-bg)`
- `#fafafa` → `var(--af-color-bg-surface)`
- `#fcfcfc` → `var(--af-color-bg-sidebar)`
- `#f4f4f4` → `var(--af-color-bg-hover)`
- `#ddd` / `#dddddd` → `var(--af-color-border)`
- `#eee` / `#eeeeee` → `var(--af-color-border-light)`
- `#ccc` (button border) → `var(--af-color-border)`
- `#f0f0f0` (button hover) → `var(--af-color-bg-hover)`
- `#fee` → `var(--af-color-danger-bg)`
- `#a00` → `var(--af-color-danger-text)`
- `#fbb` (danger border) → `var(--af-color-danger-bg)`
- `#fff3cd` → `var(--af-color-banner-bg)`
- `#6a4900` (banner text) — keep as-is (no token defined for banner text in v1)
- `#888` / `#888888` → `var(--af-color-text-muted)`
- `#666` / `#666666` → `var(--af-color-text-sub)`
- `#f5f5f5` (table header bg + kbd bg) → `var(--af-color-bg-hover)` (close enough for v1)

**No tests for the CSS file itself** — visual correctness is verified by running `aspireform ui`
and opening the browser. The existing `UiHostSmokeTests` HTTP test continues to pass.

**Commit:** `refactor: convert site.css hard-coded colors to CSS custom properties`

---

### Task 5 — theme-interop.js + App.razor link tag

**Files:**
- `src/AspireForm/Ui/wwwroot/theme-interop.js` (NEW)
- `src/AspireForm/Ui/Components/App.razor` (MODIFY)

**Spec:**

`theme-interop.js`:
```js
// AspireForm theme live-reload interop.
// Called from Blazor components via IJSRuntime after a token is saved.
window.afTheme = {
    reload: function () {
        var link = document.getElementById('af-theme');
        if (link) {
            link.href = '/theme.css?v=' + Date.now();
        }
    }
};
```

Read the current `App.razor` first, then add to its `<head>` section:

```html
<link id="af-theme" rel="stylesheet" href="/theme.css" />
<script src="/theme-interop.js"></script>
```

The `<link>` must come AFTER `<link rel="stylesheet" href="...blazor...">` (if present) so it has higher specificity priority, or at minimum after the main stylesheet.

Also ensure the `theme-interop.js` file is included in the `<Content>` copy item in `AspireForm.csproj` (the existing wildcard `Ui/wwwroot/**/*` already covers it — no csproj change needed).

**Commit:** `feat: add theme-interop.js and af-theme link tag to App.razor`

---

### Task 6 — /theme.css Kestrel endpoint + IThemeStore DI registration

**Files:**
- `src/AspireForm/Ui/UiHost.cs` (MODIFY)

**Spec:** Register `ThemeStore` as singleton `IThemeStore`. Map a `/theme.css` GET endpoint.

```csharp
// In UiHost.RunAsync, after builder.Services.AddSingleton(opts):
builder.Services.AddSingleton<IThemeStore>(_ => new ThemeStore(opts.ProjectDir));
```

```csharp
// After app.UseAntiforgery():
app.MapGet("/theme.css", (IThemeStore themeStore) =>
{
    var tokens = themeStore.GetTokens();
    var sb = new System.Text.StringBuilder();
    sb.AppendLine(":root {");
    foreach (var kv in tokens)
        sb.AppendLine($"  --af-{kv.Key}: {kv.Value};");
    sb.AppendLine("}");
    return Results.Content(sb.ToString(), "text/css");
});
```

Add `using AspireForm.Ui.Theme;` at the top.

**Tests:** See Task 7.

**Commit:** `feat: register IThemeStore and map /theme.css endpoint in UiHost`

---

### Task 7 — ThemeCssEndpointTests

**Files:**
- `tests/AspireForm.Tests/Ui/Theme/ThemeCssEndpointTests.cs` (NEW)

**Spec:** Spin up UiHost on an ephemeral port (same pattern as `UiHostSmokeTests`). Verify that
`GET /theme.css` returns `200 OK`, `Content-Type: text/css`, and contains all 14 `--af-*` property names.
Also verify that after saving a custom value via the ThemeStore file, a subsequent request reflects it.

```csharp
using AspireForm.Ui;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeCssEndpointTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"af-theme-ep-{Guid.NewGuid():N}");

    public ThemeCssEndpointTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static int FindFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task ThemeCss_returns_css_with_all_14_tokens()
    {
        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/theme.css"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }
            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            resp.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("--af-color-primary:");
            body.Should().Contain("--af-color-bg:");
            body.Should().Contain("--af-color-border:");
            // Check all 14 tokens are present.
            AspireForm.Ui.Theme.ThemeDefaults.Tokens
                .Should().AllSatisfy(t => body.Should().Contain(t.CssVar));
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }

    [Fact]
    public async Task ThemeCss_reflects_custom_token_saved_to_theme_json()
    {
        // Pre-seed the theme.json file directly.
        var aspireformDir = Path.Combine(_dir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        await File.WriteAllTextAsync(Path.Combine(aspireformDir, "theme.json"),
            """{ "color-primary": "#aabbcc" }""");

        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/theme.css"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }
            var body = await resp!.Content.ReadAsStringAsync();
            body.Should().Contain("#aabbcc");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }
}
```

**Commit:** `test: add ThemeCssEndpointTests verifying /theme.css Kestrel endpoint`

---

### Task 8 — ThemeTokenEditor Blazor component

**Files:**
- `src/AspireForm/Ui/Components/Theme/ThemeTokenEditor.razor` (NEW)

**Spec:** Renders a table of all 14 tokens. Each row has a color picker + text input + per-row Reset link.
On change: calls `IThemeStore.SaveTokenAsync` then invokes `afTheme.reload()` JS interop.

```razor
@inject IThemeStore ThemeStore
@inject IJSRuntime JS

<table class="entities" style="max-width: 640px;">
    <thead>
        <tr>
            <th>Token</th>
            <th>Color</th>
            <th>Value</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
        @foreach (var token in ThemeDefaults.Tokens)
        {
            var name = token.Name;
            var current = _values.TryGetValue(name, out var v) ? v : token.DefaultValue;
            <tr>
                <td title="@token.CssVar">@token.Label</td>
                <td>
                    <input type="color" value="@current"
                           @onchange="e => OnColorChange(name, e.Value?.ToString() ?? current)" />
                </td>
                <td>
                    <input type="text" value="@current" style="width: 90px; font-family: monospace;"
                           @onchange="e => OnTextChange(name, e.Value?.ToString() ?? current)" />
                </td>
                <td>
                    @if (_values.ContainsKey(name))
                    {
                        <button @onclick="() => ResetToken(name)" title="Reset to default">↺</button>
                    }
                </td>
            </tr>
        }
    </tbody>
</table>

@if (_errorMessage is not null)
{
    <div class="banner" style="margin-top: .5rem;">@_errorMessage</div>
}

@code {
    private Dictionary<string, string> _values = [];
    private string? _errorMessage;

    // Valid 6-digit hex color regex.
    private static readonly System.Text.RegularExpressions.Regex HexColor =
        new(@"^#[0-9a-fA-F]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    protected override void OnInitialized()
    {
        var all = ThemeStore.GetTokens();
        var defaults = ThemeDefaults.Tokens.ToDictionary(t => t.Name, t => t.DefaultValue);
        // Only store values that differ from defaults.
        foreach (var kv in all)
        {
            if (defaults.TryGetValue(kv.Key, out var def) && kv.Value != def)
                _values[kv.Key] = kv.Value;
        }
    }

    private async Task OnColorChange(string name, string value)
    {
        if (!HexColor.IsMatch(value)) return; // color picker always gives valid hex — safety guard
        await SaveAsync(name, value);
    }

    private async Task OnTextChange(string name, string value)
    {
        if (!HexColor.IsMatch(value))
        {
            _errorMessage = $"Invalid color value '{value}'. Use 6-digit hex (e.g. #1a73e8).";
            return;
        }
        _errorMessage = null;
        await SaveAsync(name, value);
    }

    private async Task SaveAsync(string name, string value)
    {
        await ThemeStore.SaveTokenAsync(name, value);
        _values[name] = value;
        await JS.InvokeVoidAsync("afTheme.reload");
    }

    private async Task ResetToken(string name)
    {
        var def = ThemeDefaults.Tokens.First(t => t.Name == name).DefaultValue;
        await ThemeStore.SaveTokenAsync(name, def);
        _values.Remove(name);
        _errorMessage = null;
        await JS.InvokeVoidAsync("afTheme.reload");
    }
}
```

**Commit:** `feat: add ThemeTokenEditor Blazor component`

---

### Task 9 — Theme.razor page + navigation + _Imports.razor

**Files:**
- `src/AspireForm/Ui/Components/Pages/Theme.razor` (NEW)
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor` (MODIFY)
- `src/AspireForm/Ui/Components/_Imports.razor` (MODIFY)

**Spec:**

`Theme.razor`:
```razor
@page "/theme"

<PageTitle>Theme — AspireForm</PageTitle>

<div style="padding: 1rem">
    <h2>Theme Editor</h2>
    <p class="muted">Adjust the color tokens used by the AspireForm UI. Changes are saved to
    <span class="kbd">.aspireform/theme.json</span> and take effect immediately.</p>

    <ThemeTokenEditor />

    <p style="margin-top: 1.5rem;">
        <button @onclick="ResetAll">Reset all to defaults</button>
    </p>
</div>

@code {
    [Inject] private IThemeStore ThemeStore { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    private async Task ResetAll()
    {
        await ThemeStore.ResetToDefaultsAsync();
        await JS.InvokeVoidAsync("afTheme.reload");
    }
}
```

Add `<a href="/theme">Theme</a>` to the nav in `MainLayout.razor` (after "About"):

```html
<nav class="topbar-nav">
    <a href="/entities">Entities</a>
    <a href="/diagnostics">Diagnostics</a>
    <a href="/theme">Theme</a>
    <a href="/about">About</a>
</nav>
```

Add to `_Imports.razor`:
```razor
@using AspireForm.Ui.Components.Theme
@using AspireForm.Ui.Theme
```

**Commit:** `feat: add Theme page, navigation link, and _Imports.razor update`

---

### Task 10 — ThemeTokenEditorTests and ThemePageTests (bUnit)

**Files:**
- `tests/AspireForm.Tests/Ui/Theme/ThemeTokenEditorTests.cs` (NEW)
- `tests/AspireForm.Tests/Ui/Theme/ThemePageTests.cs` (NEW)

**Spec:** Mirror the bUnit pattern from `EntitiesPageTests`. Use a fake `IThemeStore` implementation.

```csharp
// tests/AspireForm.Tests/Ui/Theme/ThemeTokenEditorTests.cs
using AspireForm.Ui.Theme;
using AspireForm.Ui.Components.Theme;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeTokenEditorTests
{
    private sealed class FakeThemeStore : IThemeStore
    {
        private readonly Dictionary<string, string> _data = [];

        public IReadOnlyDictionary<string, string> GetTokens()
        {
            var result = ThemeDefaults.Tokens.ToDictionary(t => t.Name, t => t.DefaultValue);
            foreach (var kv in _data) result[kv.Key] = kv.Value;
            return result;
        }

        public Task SaveTokenAsync(string name, string value, CancellationToken ct = default)
        {
            _data[name] = value;
            return Task.CompletedTask;
        }

        public Task ResetToDefaultsAsync(CancellationToken ct = default)
        {
            _data.Clear();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void ThemeTokenEditor_renders_row_for_each_token()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<ThemeTokenEditor>();
        // 14 token rows — check a sample of label text.
        cut.Markup.Should().Contain("Primary accent / links");
        cut.Markup.Should().Contain("Page background");
        cut.Markup.Should().Contain("Main borders");
    }

    [Fact]
    public void ThemeTokenEditor_shows_default_color_values()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<ThemeTokenEditor>();
        cut.Markup.Should().Contain("#1a73e8"); // default primary color
    }
}
```

```csharp
// tests/AspireForm.Tests/Ui/Theme/ThemePageTests.cs
using AspireForm.Ui.Theme;
using AspireForm.Ui.Components.Pages;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemePageTests
{
    private sealed class FakeThemeStore : IThemeStore
    {
        public IReadOnlyDictionary<string, string> GetTokens() =>
            ThemeDefaults.Tokens.ToDictionary(t => t.Name, t => t.DefaultValue);

        public Task SaveTokenAsync(string name, string value, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ResetToDefaultsAsync(CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void ThemePage_renders_heading_and_editor()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<Theme>();
        cut.Markup.Should().Contain("Theme Editor");
        cut.Markup.Should().Contain("theme.json");
    }
}
```

**Commit:** `test: add bUnit tests for ThemeTokenEditor and Theme page`

---

### Task 11 — aspireform_theme_show MCP tool + tests

**Files:**
- `src/AspireForm/Mcp/Tools/ThemeShowTool.cs` (NEW)
- `tests/AspireForm.Tests/Mcp/Tools/ThemeShowToolTests.cs` (NEW)

**Spec:** Look at an existing read-only MCP tool (e.g., `StateListCommand.cs`) for the tool shape.
`aspireform_theme_show` takes no arguments and returns the token map as a JSON object.

Read `src/AspireForm/Mcp/` to understand the tool registration pattern before writing this task.
Register the tool in the same place as other tools (likely `McpServer.cs` or `ToolRegistry.cs`).

The tool returns a JSON string like `{"color-primary":"#1a73e8","color-bg":"#ffffff",...}`.

The `IThemeStore` should be resolved from the `IServiceProvider` available in the tool context.
If it is not registered (e.g., in MCP mode without `aspireform ui`), return an error message
explaining that the theme store is only available when running via `aspireform ui`.

**Commit:** `feat: add aspireform_theme_show MCP tool with tests`

---

### Task 12 — Bump version, update CHANGELOG + README

**Files:**
- `src/AspireForm/AspireForm.csproj` (MODIFY — `0.5.0` → `0.7.0`)
- `CHANGELOG.md` (MODIFY — add `[0.7.0]` section)
- `README.md` (MODIFY — update status + add Theme Editor section)

**Spec:**

`AspireForm.csproj`: change `<Version>0.5.0</Version>` to `<Version>0.7.0</Version>`.

`CHANGELOG.md` new section at the top (after the existing header):

```markdown
## [0.7.0] — 2026-05-26

### Added
- **Theme Editor** — `aspireform ui` now includes a `/theme` page for editing the AspireForm UI color
  tokens. Changes are saved to `.aspireform/theme.json` and take effect immediately via a live CSS reload.
- `aspireform_theme_show` MCP tool (read-only) — returns the active theme token map as JSON.

### Changed
- `site.css` refactored to CSS custom properties (`var(--af-*)`); all 14 color tokens are now themeable.
```

`README.md` update the **Status** line:

```markdown
v0.7.0 — Theme Editor. `aspireform ui` now includes a visual theme editor for the UI color tokens.
```

Add a **Use the theme editor** section after **Use the entity builder**:

```markdown
## Use the theme editor

`aspireform ui` includes a **Theme** tab where you can adjust the color tokens that govern the
AspireForm UI shell. Changes are saved to `.aspireform/theme.json` in your project directory and
take effect immediately (no page reload required).

```bash
aspireform ui           # then navigate to http://localhost:5050/theme
```

The token map can also be read by an agent via the `aspireform_theme_show` MCP tool.

> **Scope:** The theme editor styles the AspireForm UI shell only. It does not modify your
> project's own CSS or scaffold any code into your Aspire solution.
```

**Commit:** `chore: bump version to 0.7.0 and update CHANGELOG + README`

---

### Task 13 — Full test suite run + dotnet pack

**Goal:** Verify green tests and produce the NuGet artifact.

```bash
dotnet run --project tests/AspireForm.Tests
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
```

If any tests fail: fix them in this task (one follow-up commit per fix).

**Commit (only if fixes needed):** `fix: <description of what was broken>`

After pack: report the artifact paths.

---

## Task ordering for parallel dispatch

**Wave 1 (independent foundations):**
- Task 1 — ThemeToken + ThemeDefaults
- Task 2 — IThemeStore interface
- Task 4 — Refactor site.css

**Wave 2 (depends on Wave 1):**
- Task 3 — ThemeStore + ThemeStoreTests (depends on Task 1, 2)
- Task 5 — theme-interop.js + App.razor (independent)

**Wave 3 (depends on Wave 2):**
- Task 6 — UiHost endpoint + DI (depends on Task 2, 3)
- Task 8 — ThemeTokenEditor.razor (depends on Tasks 1, 2)

**Wave 4 (depends on Wave 3):**
- Task 7 — ThemeCssEndpointTests (depends on Task 6)
- Task 9 — Theme.razor page + nav (depends on Task 8)
- Task 11 — ThemeShowTool + tests (depends on Task 6)

**Wave 5 (depends on Wave 4):**
- Task 10 — bUnit ThemeTokenEditor + ThemePage tests (depends on Task 9)

**Wave 6 (sequential, at the end):**
- Task 12 — version bump + CHANGELOG + README
- Task 13 — full test run + pack
