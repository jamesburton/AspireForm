# AspireForm UI Polish + Blazor Blueprint Adoption — Plan 6.0

> **For agentic workers:** Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Ship AspireForm 1.0.0 — adopt BlazorBlueprint.Components as the UI component library, rewrite the layout shell (left sidebar + top action bar), replace the single-theme editor with a multi-theme tweakcn-compatible editor, and polish all pages using Blueprint primitives.

**Architecture:** Blueprint ships `staticwebassets/blazorblueprint.css` — served automatically by ASP.NET at `/_content/BlazorBlueprint.Components/blazorblueprint.css`. No Tailwind CLI, no vendored CSS, no Node runtime. Theme model uses oklch color values (tweakcn-compatible). Dark mode via `.dark` class on `<html>`. Multi-theme persistence under `.aspireform/themes/`.

**Deviations from spec §6.1 (locked at plan time):**
- Theme token values use oklch strings instead of HSL (tweakcn moved to oklch in 2024; oklch is more correct).
- CSS delivery: `/_content/BlazorBlueprint.Components/blazorblueprint.css` replaces spec's `/tailwind.css` (no Tailwind toolchain needed).
- Token editor: hex color picker + hex text input per token instead of HSL sliders (sliders deferred to v1.1).
- Dark mode applied via JS that adds/removes `.dark` class on `<html>` (Blueprint's convention).

**Constraints for all subagents:**
1. SDK is `Microsoft.NET.Sdk.Web`; `<IsPackable>true</IsPackable>` is already set. DO NOT remove.
2. Blazor Server needs `app.UseAntiforgery()` (already in `UiHost.cs`). Keep it.
3. Pages with interactive controls need `@rendermode InteractiveServer`. Required for `@onclick` to wire up.
4. New namespaces need `@using` in `_Imports.razor` — Razor compiler does NOT auto-discover.
5. Always verify with `git log -1 --oneline` before reporting DONE.
6. AwesomeAssertions only — never `Assert.*`.
7. Test runner: `dotnet run --project tests/AspireForm.Tests` (full) or `dotnet run --project tests/AspireForm.Tests -- --filter "FullyQualifiedName~<Class>"`. Never `dotnet test`.
8. Pack flag: `-p:EnableSourceControlManagerQueries=false` (worktree `.git` is non-standard).

---

## File map

**New (production):**
- `src/AspireForm/Ui/Theme/ThemeManifest.cs` — active-theme pointer model
- `src/AspireForm/Ui/Theme/TweakcnImporter.cs` — parse tweakcn JSON → ThemeTokenSet
- `src/AspireForm/Ui/Components/Layout/AppSidebar.razor`
- `src/AspireForm/Ui/Components/Layout/AppTopBar.razor`
- `src/AspireForm/Ui/Components/Layout/ThemeSwitcherDropdown.razor`
- `src/AspireForm/Ui/wwwroot/app.css` — small AspireForm-specific overrides
- `src/AspireForm/Mcp/Tools/ThemeListTool.cs`
- `src/AspireForm/Mcp/Tools/ThemeActivateTool.cs`

**Modified (production):**
- `src/AspireForm/AspireForm.csproj` — bump 0.8.0→1.0.0; add BlazorBlueprint.Components 3.10.2
- `src/AspireForm/Ui/Theme/ThemeToken.cs` — extend for oklch + light/dark buckets + radius
- `src/AspireForm/Ui/Theme/ThemeDefaults.cs` — rewrite: 4 default themes (Light, Dark, Slate Blue, Emerald)
- `src/AspireForm/Ui/Theme/IThemeStore.cs` — rewrite: multi-theme async API
- `src/AspireForm/Ui/Theme/ThemeStore.cs` — rewrite: file-per-theme, `.aspireform/themes/` dir
- `src/AspireForm/Ui/UiHost.cs` — DI for new IThemeStore; `/theme.css` endpoint rewrite; `/themes/set-active` POST; Blueprint JS served
- `src/AspireForm/Ui/Components/_Imports.razor` — add Blueprint usings
- `src/AspireForm/Ui/Components/App.razor` — link Blueprint CSS + app.css
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor` — rewrite: sidebar + top bar shell
- `src/AspireForm/Ui/Components/Pages/Index.razor` — rewrite with Blueprint cards
- `src/AspireForm/Ui/Components/Pages/Entities.razor` — rewrite with Blueprint primitives
- `src/AspireForm/Ui/Components/Pages/Endpoints.razor` — rewrite with Blueprint primitives
- `src/AspireForm/Ui/Components/Pages/Theme.razor` — rewrite: multi-theme picker + token editor
- `src/AspireForm/Ui/Components/Pages/Diagnostics.razor` — rewrite with Blueprint Alert/Table
- `src/AspireForm/Ui/Components/Pages/About.razor` — rewrite with Blueprint Card
- `src/AspireForm/Ui/Components/Dialogs/NewEntityDialog.razor` — use Blueprint Dialog
- `src/AspireForm/Ui/Components/Dialogs/AddPropertyDialog.razor` — use Blueprint Dialog
- `src/AspireForm/Ui/Components/Dialogs/NewEndpointDialog.razor` — use Blueprint Dialog
- `src/AspireForm/Ui/wwwroot/theme-interop.js` — rewrite: apply `.dark` class + CSS variables
- `src/AspireForm/Cli/McpCommand.cs` — register 2 new theme tools (40→42)

**Removed:**
- `src/AspireForm/Ui/wwwroot/site.css`
- `src/AspireForm/Ui/Components/Entity/EntityList.razor` (empty stub)

**New (tests):**
- `tests/AspireForm.Tests/Ui/Theme/TweakcnImporterTests.cs`
- `tests/AspireForm.Tests/Ui/Layout/AppSidebarTests.cs`
- `tests/AspireForm.Tests/Ui/Layout/AppTopBarTests.cs`
- `tests/AspireForm.Tests/Ui/Layout/ThemeSwitcherDropdownTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ThemeListActivateToolTests.cs`

**Modified (tests):**
- `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs` — rewrite for multi-theme
- `tests/AspireForm.Tests/Ui/UiHostSmokeTests.cs` — assert `/_content/` CSS + `/theme.css`
- `tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs` — update count 40→42
- `tests/AspireForm.Tests/Mcp/EndToEndTests.cs` — update if tool count asserted

---

## Task 1: Bump version + add BlazorBlueprint.Components

**Files:** `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1:** Edit `src/AspireForm/AspireForm.csproj`:
  - Change `<Version>0.8.0</Version>` to `<Version>1.0.0</Version>`
  - Add to the existing `<ItemGroup>` with PackageReferences:
    ```xml
    <PackageReference Include="BlazorBlueprint.Components" Version="3.10.2" />
    ```

- [ ] **Step 2:** Build to confirm restore succeeds:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```
  Expected: succeeds. If you see a `NU1701` (net8.0/net10.0 compat warning), that is acceptable — add `<NoWarn>$(NoWarn);NU1701</NoWarn>` to the PropertyGroup and rebuild.

- [ ] **Step 3:** Commit:
  ```
  git add src/AspireForm/AspireForm.csproj
  git -c commit.gpgsign=false commit -m "chore: bump AspireForm to 1.0.0, add BlazorBlueprint.Components 3.10.2"
  ```

---

## Task 2: Rewrite theme model (ThemeToken, ThemeDefaults, ThemeManifest)

**Files:**
- Modify: `src/AspireForm/Ui/Theme/ThemeToken.cs`
- Modify: `src/AspireForm/Ui/Theme/ThemeDefaults.cs`
- Create: `src/AspireForm/Ui/Theme/ThemeManifest.cs`

- [ ] **Step 1:** Rewrite `src/AspireForm/Ui/Theme/ThemeToken.cs`:

```csharp
namespace AspireForm.Ui.Theme;

/// <summary>The full set of design tokens for one theme (light + dark variants + radius).</summary>
/// <param name="Name">Theme display name (e.g., "Slate Blue").</param>
/// <param name="Description">Short description shown in the theme picker.</param>
/// <param name="Light">Light-mode token values keyed by token name.</param>
/// <param name="Dark">Dark-mode token values keyed by token name.</param>
/// <param name="Radius">Border radius in rem (0–1, step 0.25).</param>
public sealed record ThemeDefinition(
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Light,
    IReadOnlyDictionary<string, string> Dark,
    double Radius);

/// <summary>Summary row shown in the theme picker list.</summary>
/// <param name="Name">Theme name (also used as file key).</param>
/// <param name="Description">Short description.</param>
/// <param name="IsActive">Whether this theme is currently active.</param>
public sealed record ThemeSummary(string Name, string Description, bool IsActive);

/// <summary>Pointer to which theme is currently active and whether dark mode is on.</summary>
/// <param name="ActiveName">Name of the active theme.</param>
/// <param name="DarkMode">True if the dark token bucket is applied.</param>
public sealed record ThemeActivation(string ActiveName, bool DarkMode);

/// <summary>Known token names in display order, matching the tweakcn/shadcn vocabulary.</summary>
public static class ThemeTokenNames
{
    /// <summary>All token names in group order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "background", "foreground",
        "primary", "primary-foreground",
        "secondary", "secondary-foreground",
        "muted", "muted-foreground",
        "accent", "accent-foreground",
        "destructive", "destructive-foreground",
        "border", "input", "ring",
        "card", "card-foreground",
        "popover", "popover-foreground",
    ];
}
```

- [ ] **Step 2:** Rewrite `src/AspireForm/Ui/Theme/ThemeDefaults.cs` with 4 built-in themes (oklch values matching tweakcn defaults):

```csharp
namespace AspireForm.Ui.Theme;

/// <summary>Factory themes shipped with AspireForm. Applied on first run when no themes directory exists.</summary>
public static class ThemeDefaults
{
    /// <summary>Returns the four built-in themes.</summary>
    public static IReadOnlyList<ThemeDefinition> BuiltIn() =>
    [
        AspireFormLight(),
        AspireFormDark(),
        SlateBlue(),
        Emerald(),
    ];

    private static ThemeDefinition AspireFormLight() => new(
        Name: "AspireForm Light",
        Description: "Clean white background with blue accents",
        Light: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(1 0 0)",
            ["foreground"]           = "oklch(0.145 0.005 285)",
            ["primary"]              = "oklch(0.546 0.245 262.88)",
            ["primary-foreground"]   = "oklch(0.985 0 0)",
            ["secondary"]            = "oklch(0.967 0.003 264)",
            ["secondary-foreground"] = "oklch(0.208 0.042 265)",
            ["muted"]                = "oklch(0.967 0.003 264)",
            ["muted-foreground"]     = "oklch(0.556 0.014 285)",
            ["accent"]               = "oklch(0.967 0.003 264)",
            ["accent-foreground"]    = "oklch(0.208 0.042 265)",
            ["destructive"]          = "oklch(0.577 0.245 27)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.922 0.004 286)",
            ["input"]                = "oklch(0.922 0.004 286)",
            ["ring"]                 = "oklch(0.546 0.245 262.88)",
            ["card"]                 = "oklch(1 0 0)",
            ["card-foreground"]      = "oklch(0.145 0.005 285)",
            ["popover"]              = "oklch(1 0 0)",
            ["popover-foreground"]   = "oklch(0.145 0.005 285)",
        },
        Dark: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(0.145 0.005 285)",
            ["foreground"]           = "oklch(0.985 0 0)",
            ["primary"]              = "oklch(0.623 0.214 259.82)",
            ["primary-foreground"]   = "oklch(0.15 0 0)",
            ["secondary"]            = "oklch(0.274 0.006 286)",
            ["secondary-foreground"] = "oklch(0.985 0 0)",
            ["muted"]                = "oklch(0.274 0.006 286)",
            ["muted-foreground"]     = "oklch(0.705 0.015 286)",
            ["accent"]               = "oklch(0.274 0.006 286)",
            ["accent-foreground"]    = "oklch(0.985 0 0)",
            ["destructive"]          = "oklch(0.704 0.191 22.2)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.274 0.006 286)",
            ["input"]                = "oklch(0.274 0.006 286)",
            ["ring"]                 = "oklch(0.623 0.214 259.82)",
            ["card"]                 = "oklch(0.145 0.005 285)",
            ["card-foreground"]      = "oklch(0.985 0 0)",
            ["popover"]              = "oklch(0.145 0.005 285)",
            ["popover-foreground"]   = "oklch(0.985 0 0)",
        },
        Radius: 0.5);

    private static ThemeDefinition AspireFormDark() => new(
        Name: "AspireForm Dark",
        Description: "Dark theme with zinc tones",
        Light: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(0.985 0 0)",
            ["foreground"]           = "oklch(0.141 0.004 285.82)",
            ["primary"]              = "oklch(0.21 0.006 285.88)",
            ["primary-foreground"]   = "oklch(0.985 0 0)",
            ["secondary"]            = "oklch(0.967 0.001 286.38)",
            ["secondary-foreground"] = "oklch(0.21 0.006 285.88)",
            ["muted"]                = "oklch(0.967 0.001 286.38)",
            ["muted-foreground"]     = "oklch(0.552 0.016 285.94)",
            ["accent"]               = "oklch(0.967 0.001 286.38)",
            ["accent-foreground"]    = "oklch(0.21 0.006 285.88)",
            ["destructive"]          = "oklch(0.577 0.245 27.33)",
            ["destructive-foreground"] = "oklch(1 0 0)",
            ["border"]               = "oklch(0.92 0.004 286.32)",
            ["input"]                = "oklch(0.92 0.004 286.32)",
            ["ring"]                 = "oklch(0.552 0.016 285.94)",
            ["card"]                 = "oklch(0.985 0 0)",
            ["card-foreground"]      = "oklch(0.141 0.004 285.82)",
            ["popover"]              = "oklch(0.985 0 0)",
            ["popover-foreground"]   = "oklch(0.141 0.004 285.82)",
        },
        Dark: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(0.141 0.004 285.82)",
            ["foreground"]           = "oklch(0.985 0 0)",
            ["primary"]              = "oklch(0.985 0 0)",
            ["primary-foreground"]   = "oklch(0.21 0.006 285.88)",
            ["secondary"]            = "oklch(0.274 0.006 286.03)",
            ["secondary-foreground"] = "oklch(0.985 0 0)",
            ["muted"]                = "oklch(0.274 0.006 286.03)",
            ["muted-foreground"]     = "oklch(0.705 0.015 286.07)",
            ["accent"]               = "oklch(0.274 0.006 286.03)",
            ["accent-foreground"]    = "oklch(0.985 0 0)",
            ["destructive"]          = "oklch(0.704 0.191 22.216)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.274 0.006 286.03)",
            ["input"]                = "oklch(0.274 0.006 286.03)",
            ["ring"]                 = "oklch(0.552 0.016 285.94)",
            ["card"]                 = "oklch(0.141 0.004 285.82)",
            ["card-foreground"]      = "oklch(0.985 0 0)",
            ["popover"]              = "oklch(0.141 0.004 285.82)",
            ["popover-foreground"]   = "oklch(0.985 0 0)",
        },
        Radius: 0.5);

    private static ThemeDefinition SlateBlue() => new(
        Name: "Slate Blue",
        Description: "Cool slate background with blue accents",
        Light: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(1 0 0)",
            ["foreground"]           = "oklch(0.129 0.042 264.695)",
            ["primary"]              = "oklch(0.546 0.245 262.88)",
            ["primary-foreground"]   = "oklch(0.985 0 0)",
            ["secondary"]            = "oklch(0.968 0.007 247.896)",
            ["secondary-foreground"] = "oklch(0.208 0.042 265.755)",
            ["muted"]                = "oklch(0.968 0.007 247.896)",
            ["muted-foreground"]     = "oklch(0.554 0.022 264.364)",
            ["accent"]               = "oklch(0.968 0.007 247.896)",
            ["accent-foreground"]    = "oklch(0.208 0.042 265.755)",
            ["destructive"]          = "oklch(0.577 0.245 27)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.929 0.013 255.508)",
            ["input"]                = "oklch(0.929 0.013 255.508)",
            ["ring"]                 = "oklch(0.546 0.245 262.88)",
            ["card"]                 = "oklch(1 0 0)",
            ["card-foreground"]      = "oklch(0.129 0.042 264.695)",
            ["popover"]              = "oklch(1 0 0)",
            ["popover-foreground"]   = "oklch(0.129 0.042 264.695)",
        },
        Dark: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(0.129 0.042 264.695)",
            ["foreground"]           = "oklch(0.984 0.003 247.858)",
            ["primary"]              = "oklch(0.623 0.214 259.82)",
            ["primary-foreground"]   = "oklch(0.15 0 0)",
            ["secondary"]            = "oklch(0.208 0.042 265.755)",
            ["secondary-foreground"] = "oklch(0.984 0.003 247.858)",
            ["muted"]                = "oklch(0.208 0.042 265.755)",
            ["muted-foreground"]     = "oklch(0.704 0.04 256.788)",
            ["accent"]               = "oklch(0.208 0.042 265.755)",
            ["accent-foreground"]    = "oklch(0.984 0.003 247.858)",
            ["destructive"]          = "oklch(0.704 0.191 22.2)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.208 0.042 265.755)",
            ["input"]                = "oklch(0.208 0.042 265.755)",
            ["ring"]                 = "oklch(0.623 0.214 259.82)",
            ["card"]                 = "oklch(0.129 0.042 264.695)",
            ["card-foreground"]      = "oklch(0.984 0.003 247.858)",
            ["popover"]              = "oklch(0.129 0.042 264.695)",
            ["popover-foreground"]   = "oklch(0.984 0.003 247.858)",
        },
        Radius: 0.5);

    private static ThemeDefinition Emerald() => new(
        Name: "Emerald",
        Description: "Fresh green tones with warm accents",
        Light: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(1 0 0)",
            ["foreground"]           = "oklch(0.153 0.02 160)",
            ["primary"]              = "oklch(0.596 0.145 163.225)",
            ["primary-foreground"]   = "oklch(0.985 0 0)",
            ["secondary"]            = "oklch(0.961 0.02 163)",
            ["secondary-foreground"] = "oklch(0.153 0.02 160)",
            ["muted"]                = "oklch(0.961 0.02 163)",
            ["muted-foreground"]     = "oklch(0.551 0.027 160)",
            ["accent"]               = "oklch(0.961 0.02 163)",
            ["accent-foreground"]    = "oklch(0.153 0.02 160)",
            ["destructive"]          = "oklch(0.577 0.245 27)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.921 0.02 163)",
            ["input"]                = "oklch(0.921 0.02 163)",
            ["ring"]                 = "oklch(0.596 0.145 163.225)",
            ["card"]                 = "oklch(1 0 0)",
            ["card-foreground"]      = "oklch(0.153 0.02 160)",
            ["popover"]              = "oklch(1 0 0)",
            ["popover-foreground"]   = "oklch(0.153 0.02 160)",
        },
        Dark: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"]           = "oklch(0.153 0.02 160)",
            ["foreground"]           = "oklch(0.961 0.02 163)",
            ["primary"]              = "oklch(0.696 0.17 162.48)",
            ["primary-foreground"]   = "oklch(0.15 0 0)",
            ["secondary"]            = "oklch(0.22 0.03 160)",
            ["secondary-foreground"] = "oklch(0.961 0.02 163)",
            ["muted"]                = "oklch(0.22 0.03 160)",
            ["muted-foreground"]     = "oklch(0.672 0.06 160)",
            ["accent"]               = "oklch(0.22 0.03 160)",
            ["accent-foreground"]    = "oklch(0.961 0.02 163)",
            ["destructive"]          = "oklch(0.704 0.191 22.2)",
            ["destructive-foreground"] = "oklch(0.985 0 0)",
            ["border"]               = "oklch(0.22 0.03 160)",
            ["input"]                = "oklch(0.22 0.03 160)",
            ["ring"]                 = "oklch(0.696 0.17 162.48)",
            ["card"]                 = "oklch(0.153 0.02 160)",
            ["card-foreground"]      = "oklch(0.961 0.02 163)",
            ["popover"]              = "oklch(0.153 0.02 160)",
            ["popover-foreground"]   = "oklch(0.961 0.02 163)",
        },
        Radius: 0.5);
}
```

- [ ] **Step 3:** Create `src/AspireForm/Ui/Theme/ThemeManifest.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AspireForm.Ui.Theme;

/// <summary>Persisted active-theme pointer. Written to <c>.aspireform/themes/_active.json</c>.</summary>
public sealed class ThemeManifest
{
    /// <summary>Name of the currently active theme.</summary>
    [JsonPropertyName("active")]
    public string Active { get; set; } = "AspireForm Light";

    /// <summary>Whether dark mode is currently enabled.</summary>
    [JsonPropertyName("darkMode")]
    public bool DarkMode { get; set; }
}
```

- [ ] **Step 4:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 5:** Commit:
  ```
  git add src/AspireForm/Ui/Theme/
  git -c commit.gpgsign=false commit -m "feat(theme): rewrite token model for multi-theme + oklch + ThemeManifest"
  ```

---

## Task 3: Rewrite IThemeStore + ThemeStore (multi-theme, file-per-theme)

**Files:**
- Modify: `src/AspireForm/Ui/Theme/IThemeStore.cs`
- Modify: `src/AspireForm/Ui/Theme/ThemeStore.cs`

- [ ] **Step 1:** Rewrite `src/AspireForm/Ui/Theme/IThemeStore.cs`:

```csharp
namespace AspireForm.Ui.Theme;

/// <summary>Multi-theme store for AspireForm. Persists themes under <c>.aspireform/themes/</c> in the project directory.</summary>
public interface IThemeStore
{
    /// <summary>Returns summaries of all available themes, with <see cref="ThemeSummary.IsActive"/> set correctly.</summary>
    Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns the full <see cref="ThemeDefinition"/> for the named theme.</summary>
    /// <exception cref="ThemeLoadException">If the theme file is missing or malformed.</exception>
    Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Saves (upserts) a theme. Creates or overwrites the file named for <paramref name="theme"/>.</summary>
    Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default);

    /// <summary>Deletes the named theme. If it was active, activates the first available theme.</summary>
    Task DeleteAsync(string name, CancellationToken ct = default);

    /// <summary>Duplicates a theme under a new name. Returns the new name.</summary>
    Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default);

    /// <summary>Renames a theme (renames the backing file and updates the active pointer if needed).</summary>
    Task RenameAsync(string oldName, string newName, CancellationToken ct = default);

    /// <summary>Returns the current active-theme name and dark-mode flag.</summary>
    Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets the active theme by name. Throws <see cref="ThemeLoadException"/> if the theme doesn't exist.</summary>
    Task SetActiveAsync(string name, CancellationToken ct = default);

    /// <summary>Toggles dark mode (does not change the active theme).</summary>
    Task SetDarkModeAsync(bool dark, CancellationToken ct = default);

    /// <summary>Resets all themes to the built-in defaults. Overwrites all existing theme files.</summary>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2:** Create `src/AspireForm/Ui/Theme/ThemeLoadException.cs`:

```csharp
namespace AspireForm.Ui.Theme;

/// <summary>Raised when a theme file cannot be loaded or parsed.</summary>
public sealed class ThemeLoadException : Exception
{
    /// <summary>Initialises with a message.</summary>
    public ThemeLoadException(string message) : base(message) { }

    /// <summary>Initialises with a message and inner exception.</summary>
    public ThemeLoadException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 3:** Rewrite `src/AspireForm/Ui/Theme/ThemeStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>File-backed multi-theme store. Themes are stored as individual JSON files under
/// <c>{projectDir}/.aspireform/themes/</c>. The active pointer lives at <c>_active.json</c>.</summary>
internal sealed class ThemeStore : IThemeStore
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string _themesDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initialises the store for the given project directory.</summary>
    public ThemeStore(string projectDir)
    {
        _themesDir = Path.Combine(projectDir, ".aspireform", "themes");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var activation = await ReadManifestAsync(ct);
        var files = Directory.GetFiles(_themesDir, "*.json")
            .Where(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var summaries = new List<ThemeSummary>(files.Count);
        foreach (var f in files)
        {
            try
            {
                var def = await ReadThemeFileAsync(f, ct);
                summaries.Add(new ThemeSummary(def.Name, def.Description,
                    string.Equals(def.Name, activation.ActiveName, StringComparison.OrdinalIgnoreCase)));
            }
            catch (ThemeLoadException)
            {
                // Surface broken themes as disabled entries (name from filename).
                var broken = Path.GetFileNameWithoutExtension(f);
                summaries.Add(new ThemeSummary(broken, "(malformed)", false));
            }
        }
        return summaries;
    }

    /// <inheritdoc/>
    public async Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var path = ThemePath(name);
        if (!File.Exists(path))
            throw new ThemeLoadException($"Theme '{name}' not found at '{path}'.");
        return await ReadThemeFileAsync(path, ct);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_themesDir);
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(theme, JsonOpts);
            await File.WriteAllTextAsync(ThemePath(theme.Name), json, ct);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var path = ThemePath(name);
            if (File.Exists(path)) File.Delete(path);

            var activation = await ReadManifestAsync(ct);
            if (string.Equals(activation.ActiveName, name, StringComparison.OrdinalIgnoreCase))
            {
                // Fall back to first available theme.
                var remaining = Directory.GetFiles(_themesDir, "*.json")
                    .Where(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (remaining is not null)
                {
                    var fallback = await ReadThemeFileAsync(remaining, ct);
                    await WriteManifestAsync(new ThemeManifest { Active = fallback.Name, DarkMode = activation.DarkMode }, ct);
                }
            }
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default)
    {
        var source = await GetAsync(sourceName, ct);
        var copy = source with { Name = newName, Description = $"Copy of {source.Description}" };
        await SaveAsync(copy, ct);
        return newName;
    }

    /// <inheritdoc/>
    public async Task RenameAsync(string oldName, string newName, CancellationToken ct = default)
    {
        var source = await GetAsync(oldName, ct);
        var renamed = source with { Name = newName };
        await SaveAsync(renamed, ct);

        var oldPath = ThemePath(oldName);
        if (File.Exists(oldPath)) File.Delete(oldPath);

        var activation = await ReadManifestAsync(ct);
        if (string.Equals(activation.ActiveName, oldName, StringComparison.OrdinalIgnoreCase))
            await WriteManifestAsync(new ThemeManifest { Active = newName, DarkMode = activation.DarkMode }, ct);
    }

    /// <inheritdoc/>
    public async Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var manifest = await ReadManifestAsync(ct);
        return new ThemeActivation(manifest.Active, manifest.DarkMode);
    }

    /// <inheritdoc/>
    public async Task SetActiveAsync(string name, CancellationToken ct = default)
    {
        if (!File.Exists(ThemePath(name)))
            throw new ThemeLoadException($"Theme '{name}' does not exist.");
        var current = await ReadManifestAsync(ct);
        await WriteManifestAsync(new ThemeManifest { Active = name, DarkMode = current.DarkMode }, ct);
    }

    /// <inheritdoc/>
    public async Task SetDarkModeAsync(bool dark, CancellationToken ct = default)
    {
        var current = await ReadManifestAsync(ct);
        await WriteManifestAsync(new ThemeManifest { Active = current.Active, DarkMode = dark }, ct);
    }

    /// <inheritdoc/>
    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_themesDir);
        foreach (var theme in ThemeDefaults.BuiltIn())
            await SaveAsync(theme, ct);
        await WriteManifestAsync(new ThemeManifest { Active = "AspireForm Light", DarkMode = false }, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_themesDir);
        if (!Directory.GetFiles(_themesDir, "*.json")
                .Any(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase)))
        {
            await MigrateLegacyIfExistsAsync(ct);
            foreach (var theme in ThemeDefaults.BuiltIn())
                await SaveAsync(theme, ct);
            if (!File.Exists(ActivePath()))
                await WriteManifestAsync(new ThemeManifest { Active = "AspireForm Light" }, ct);
        }
    }

    private async Task MigrateLegacyIfExistsAsync(CancellationToken ct)
    {
        // Migrate v0.7 single-file theme.json → "Migrated v0.7" theme.
        var legacyPath = Path.Combine(Path.GetDirectoryName(_themesDir)!, "theme.json");
        if (!File.Exists(legacyPath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(legacyPath, ct);
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj) return;
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in obj)
            {
                if (kv.Value?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    tokens[kv.Key] = kv.Value.GetValue<string>();
            }
            if (tokens.Count > 0)
            {
                // Best-effort: use the legacy tokens as both light and dark.
                var migrated = new ThemeDefinition("Migrated v0.7", "Imported from legacy theme.json", tokens, tokens, 0.5);
                await SaveAsync(migrated, ct);
                await WriteManifestAsync(new ThemeManifest { Active = "Migrated v0.7" }, ct);
            }
        }
        catch
        {
            // Legacy migration is best-effort — don't block startup.
        }
    }

    private string ThemePath(string name)
    {
        // Sanitize: replace spaces with hyphens, lowercase, keep alphanumeric/-
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        slug = new string(slug.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
        return Path.Combine(_themesDir, $"{slug}.json");
    }

    private string ActivePath() => Path.Combine(_themesDir, "_active.json");

    private async Task<ThemeManifest> ReadManifestAsync(CancellationToken ct)
    {
        var path = ActivePath();
        if (!File.Exists(path)) return new ThemeManifest();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ThemeManifest>(json, JsonOpts) ?? new ThemeManifest();
        }
        catch { return new ThemeManifest(); }
    }

    private async Task WriteManifestAsync(ThemeManifest manifest, CancellationToken ct)
    {
        Directory.CreateDirectory(_themesDir);
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(ActivePath(), json, ct);
    }

    private static async Task<ThemeDefinition> ReadThemeFileAsync(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var def = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOpts);
            if (def is null) throw new ThemeLoadException($"Null deserialization result from '{path}'.");
            return def;
        }
        catch (ThemeLoadException) { throw; }
        catch (Exception ex) { throw new ThemeLoadException($"Failed to load theme from '{path}': {ex.Message}", ex); }
    }
}
```

- [ ] **Step 4:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 5:** Commit:
  ```
  git add src/AspireForm/Ui/Theme/
  git -c commit.gpgsign=false commit -m "feat(theme): rewrite IThemeStore + ThemeStore for multi-theme file-per-theme persistence"
  ```

---

## Task 4: ThemeStore tests + TweakcnImporter

**Files:**
- Rewrite: `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs`
- Create: `src/AspireForm/Ui/Theme/TweakcnImporter.cs`
- Create: `tests/AspireForm.Tests/Ui/Theme/TweakcnImporterTests.cs`

- [ ] **Step 1:** Rewrite `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs`:

```csharp
using AspireForm.Ui.Theme;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly ThemeStore _store;

    public ThemeStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"af-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _store = new ThemeStore(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ListAsync_installs_defaults_on_first_call()
    {
        var list = await _store.ListAsync();
        list.Should().HaveCountGreaterThanOrEqualTo(4);
        list.Should().Contain(s => s.Name == "AspireForm Light");
        list.Should().Contain(s => s.Name == "Emerald");
    }

    [Fact]
    public async Task GetActiveAsync_returns_default_theme_on_fresh_store()
    {
        var activation = await _store.GetActiveAsync();
        activation.ActiveName.Should().Be("AspireForm Light");
        activation.DarkMode.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_and_GetAsync_round_trip_a_theme()
    {
        var theme = new ThemeDefinition("Test Theme", "desc",
            Light: new Dictionary<string, string> { ["background"] = "oklch(1 0 0)" },
            Dark: new Dictionary<string, string> { ["background"] = "oklch(0.1 0 0)" },
            Radius: 0.5);
        await _store.SaveAsync(theme);
        var loaded = await _store.GetAsync("Test Theme");
        loaded.Name.Should().Be("Test Theme");
        loaded.Light["background"].Should().Be("oklch(1 0 0)");
    }

    [Fact]
    public async Task SetActiveAsync_changes_active_theme()
    {
        await _store.ListAsync(); // ensure defaults
        await _store.SetActiveAsync("Emerald");
        var activation = await _store.GetActiveAsync();
        activation.ActiveName.Should().Be("Emerald");
    }

    [Fact]
    public async Task SetDarkModeAsync_toggles_dark_flag()
    {
        await _store.SetDarkModeAsync(true);
        var activation = await _store.GetActiveAsync();
        activation.DarkMode.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_removes_theme_file()
    {
        var theme = new ThemeDefinition("ToDelete", "desc",
            new Dictionary<string, string>(), new Dictionary<string, string>(), 0.5);
        await _store.SaveAsync(theme);
        await _store.DeleteAsync("ToDelete");
        var list = await _store.ListAsync();
        list.Should().NotContain(s => s.Name == "ToDelete");
    }

    [Fact]
    public async Task DuplicateAsync_creates_copy_with_new_name()
    {
        await _store.ListAsync(); // ensure defaults
        await _store.DuplicateAsync("AspireForm Light", "My Copy");
        var copy = await _store.GetAsync("My Copy");
        copy.Name.Should().Be("My Copy");
        copy.Light.Should().ContainKey("background");
    }

    [Fact]
    public async Task RenameAsync_renames_theme_and_updates_active_pointer()
    {
        await _store.ListAsync();
        await _store.SetActiveAsync("Emerald");
        await _store.RenameAsync("Emerald", "Renamed Emerald");
        var activation = await _store.GetActiveAsync();
        activation.ActiveName.Should().Be("Renamed Emerald");
    }

    [Fact]
    public async Task ResetToDefaultsAsync_restores_four_factory_themes()
    {
        await _store.ResetToDefaultsAsync();
        var list = await _store.ListAsync();
        list.Should().HaveCountGreaterThanOrEqualTo(4);
        list.Should().Contain(s => s.Name == "AspireForm Light");
    }
}
```

- [ ] **Step 2:** Create `src/AspireForm/Ui/Theme/TweakcnImporter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>Parses a tweakcn-exported JSON string into a <see cref="ThemeDefinition"/>.</summary>
public static class TweakcnImporter
{
    /// <summary>Parses <paramref name="json"/> as a tweakcn theme export.
    /// Accepts both HSL (<c>hsl(222 84% 5%)</c>) and oklch (<c>oklch(0.14 0.04 265)</c>) value formats.</summary>
    /// <param name="json">The raw JSON from tweakcn's "Copy code" export.</param>
    /// <param name="themeName">Display name for the imported theme.</param>
    /// <returns>A new <see cref="ThemeDefinition"/>.</returns>
    /// <exception cref="TweakcnImportException">If the JSON is malformed or missing required structure.</exception>
    public static ThemeDefinition Parse(string json, string themeName = "Imported Theme")
    {
        JsonObject root;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj)
                throw new TweakcnImportException("Root element must be a JSON object.");
            root = obj;
        }
        catch (JsonException ex)
        {
            throw new TweakcnImportException($"Malformed JSON: {ex.Message}", ex);
        }

        var light = ExtractTokens(root, "light");
        var dark = ExtractTokens(root, "dark");

        double radius = 0.5;
        if (root["radius"] is JsonValue rv && rv.TryGetValue<double>(out var r))
            radius = r;

        return new ThemeDefinition(
            Name: themeName,
            Description: root["description"]?.GetValue<string>() ?? "Imported from tweakcn",
            Light: light,
            Dark: dark,
            Radius: radius);
    }

    private static IReadOnlyDictionary<string, string> ExtractTokens(JsonObject root, string bucket)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        // tweakcn exports: { "tokens": { "light": { ... }, "dark": { ... } } }
        // or flat: { "light": { ... } }
        var bucketNode = root["tokens"]?[bucket] ?? root[bucket];
        if (bucketNode is not JsonObject obj) return tokens;

        foreach (var kv in obj)
        {
            if (kv.Value?.GetValueKind() == JsonValueKind.String)
                tokens[kv.Key] = kv.Value.GetValue<string>();
        }
        return tokens;
    }
}

/// <summary>Raised by <see cref="TweakcnImporter"/> when the import JSON is malformed or missing required fields.</summary>
public sealed class TweakcnImportException : Exception
{
    /// <summary>Initialises with a message.</summary>
    public TweakcnImportException(string message) : base(message) { }

    /// <summary>Initialises with a message and inner exception.</summary>
    public TweakcnImportException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 3:** Create `tests/AspireForm.Tests/Ui/Theme/TweakcnImporterTests.cs`:

```csharp
using AspireForm.Ui.Theme;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui.Theme;

public sealed class TweakcnImporterTests
{
    [Fact]
    public void Parse_valid_nested_tweakcn_json_returns_theme_definition()
    {
        var json = """
            {
              "tokens": {
                "light": { "background": "oklch(1 0 0)", "foreground": "oklch(0.14 0.005 285)" },
                "dark":  { "background": "oklch(0.14 0.005 285)", "foreground": "oklch(0.985 0 0)" }
              },
              "radius": 0.5
            }
            """;

        var def = TweakcnImporter.Parse(json, "Test Import");

        def.Name.Should().Be("Test Import");
        def.Light["background"].Should().Be("oklch(1 0 0)");
        def.Dark["background"].Should().Be("oklch(0.14 0.005 285)");
        def.Radius.Should().Be(0.5);
    }

    [Fact]
    public void Parse_flat_tweakcn_json_also_works()
    {
        var json = """
            {
              "light": { "primary": "oklch(0.546 0.245 262.88)" },
              "dark":  { "primary": "oklch(0.623 0.214 259.82)" }
            }
            """;

        var def = TweakcnImporter.Parse(json, "Flat Import");
        def.Light["primary"].Should().Be("oklch(0.546 0.245 262.88)");
    }

    [Fact]
    public void Parse_malformed_json_throws_TweakcnImportException()
    {
        var act = () => TweakcnImporter.Parse("not json at all", "Bad");
        act.Should().Throw<TweakcnImportException>();
    }

    [Fact]
    public void Parse_non_object_root_throws_TweakcnImportException()
    {
        var act = () => TweakcnImporter.Parse("[1,2,3]", "Array");
        act.Should().Throw<TweakcnImportException>();
    }

    [Fact]
    public void Parse_missing_buckets_returns_empty_token_dicts()
    {
        var json = """{ "radius": 0.25 }""";
        var def = TweakcnImporter.Parse(json, "Empty");
        def.Light.Should().BeEmpty();
        def.Dark.Should().BeEmpty();
        def.Radius.Should().Be(0.25);
    }
}
```

- [ ] **Step 4:** Run theme tests:
  ```
  dotnet run --project tests/AspireForm.Tests -- --filter "FullyQualifiedName~AspireForm.Tests.Ui.Theme"
  ```
  Expected: all pass.

- [ ] **Step 5:** Commit:
  ```
  git add src/AspireForm/Ui/Theme/ tests/AspireForm.Tests/Ui/Theme/
  git -c commit.gpgsign=false commit -m "feat(theme): add TweakcnImporter + rewrite ThemeStore tests for multi-theme"
  ```

---

## Task 5: Rewrite UiHost (Blueprint DI, /theme.css, /themes/set-active)

**Files:** `src/AspireForm/Ui/UiHost.cs`

- [ ] **Step 1:** Rewrite `src/AspireForm/Ui/UiHost.cs`:

```csharp
using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using AspireForm.Ui.Theme;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace AspireForm.Ui;

/// <summary>Hosts Kestrel + Blazor Server inside the dnx tool process.</summary>
internal static class UiHost
{
    /// <summary>Runs the host until <paramref name="ct"/> fires or Ctrl-C is received.</summary>
    public static async Task RunAsync(UiOptions opts, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Services.AddSingleton<IThemeStore>(_ => new ThemeStore(opts.ProjectDir));
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // Serve wwwroot (app.css, theme-interop.js, etc.).
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
        }

        app.UseAntiforgery();

        // /theme.css — emits :root { --background: ...; ... } for the active theme.
        app.MapGet("/theme.css", async (IThemeStore themeStore) =>
        {
            var activation = await themeStore.GetActiveAsync();
            var theme = await themeStore.GetAsync(activation.ActiveName);
            var tokens = activation.DarkMode ? theme.Dark : theme.Light;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(":root {");
            foreach (var kv in tokens)
                sb.AppendLine($"  --{kv.Key}: {kv.Value};");
            sb.AppendLine($"  --radius: {theme.Radius.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}rem;");
            sb.AppendLine("}");

            if (activation.DarkMode)
                sb.AppendLine("html { color-scheme: dark; }");

            return Results.Content(sb.ToString(), "text/css");
        });

        // POST /themes/set-active — switches the active theme (called from ThemeSwitcherDropdown JS).
        app.MapPost("/themes/set-active", async (IThemeStore themeStore, SetActiveRequest req) =>
        {
            try
            {
                await themeStore.SetActiveAsync(req.Name);
                return Results.Ok(new { ok = true });
            }
            catch (ThemeLoadException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // POST /themes/set-dark-mode — toggles dark mode.
        app.MapPost("/themes/set-dark-mode", async (IThemeStore themeStore, SetDarkModeRequest req) =>
        {
            await themeStore.SetDarkModeAsync(req.Dark);
            return Results.Ok(new { ok = true });
        });

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        var url = $"http://localhost:{opts.Port}";
        Console.Out.WriteLine($"AspireForm UI listening at {url} (project-dir: {opts.ProjectDir})");
        Console.Out.WriteLine("Press Ctrl+C to stop.");
        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await app.RunAsync(ct);
    }

    private sealed record SetActiveRequest(string Name);
    private sealed record SetDarkModeRequest(bool Dark);
}
```

- [ ] **Step 2:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 3:** Commit:
  ```
  git add src/AspireForm/Ui/UiHost.cs
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite UiHost for multi-theme DI + /theme.css + /themes/set-active"
  ```

---

## Task 6: Update App.razor, _Imports.razor, theme-interop.js, remove site.css, add app.css

**Files:**
- Modify: `src/AspireForm/Ui/Components/App.razor`
- Modify: `src/AspireForm/Ui/Components/_Imports.razor`
- Modify: `src/AspireForm/Ui/wwwroot/theme-interop.js`
- Create: `src/AspireForm/Ui/wwwroot/app.css`
- Delete: `src/AspireForm/Ui/wwwroot/site.css`

- [ ] **Step 1:** Read the current `App.razor` to see its structure, then rewrite it adding Blueprint CSS link + app.css link. The new `App.razor` must include:
  - Link to `/_content/BlazorBlueprint.Components/blazorblueprint.css`
  - Link to `/theme.css` (the dynamic theme endpoint)
  - Link to `/app.css` (AspireForm overrides)

  Keep `<Routes />`, `<script src="/_framework/blazor.web.js"></script>`, and any existing `HeadOutlet`.

- [ ] **Step 2:** Edit `_Imports.razor` — add these lines after existing usings:
  ```razor
  @using BlazorBlueprint.Components
  @using BlazorBlueprint.Components.Layout
  @using BlazorBlueprint.Components.Navigation
  ```
  (Verify namespace names after building — if Blueprint uses a flat namespace, `@using BlazorBlueprint.Components` alone may suffice.)

- [ ] **Step 3:** Rewrite `src/AspireForm/Ui/wwwroot/theme-interop.js`:

```javascript
// theme-interop.js — Dark mode toggle + theme CSS reload for AspireForm UI.

/**
 * Apply or remove the 'dark' class on <html> and reload the /theme.css link.
 * @param {boolean} isDark
 */
export function setDarkMode(isDark) {
    const root = document.documentElement;
    if (isDark) {
        root.classList.add('dark');
    } else {
        root.classList.remove('dark');
    }
    reloadThemeCss();
}

/**
 * Force the browser to re-fetch /theme.css by toggling a cache-busting query param.
 */
export function reloadThemeCss() {
    const link = document.querySelector('link[href^="/theme.css"]');
    if (link) {
        const url = new URL(link.href);
        url.searchParams.set('v', Date.now().toString());
        link.href = url.toString();
    }
}

/**
 * Switch the active theme via POST, then reload the theme CSS.
 * @param {string} themeName
 */
export async function switchTheme(themeName) {
    try {
        await fetch('/themes/set-active', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: themeName }),
        });
    } catch { /* best-effort */ }
    reloadThemeCss();
}
```

- [ ] **Step 4:** Create `src/AspireForm/Ui/wwwroot/app.css` (small AspireForm-specific overrides):

```css
/* app.css — AspireForm-specific overrides on top of Blueprint */

/* Ensure the sidebar never wraps its nav items */
.af-sidebar-nav a {
    white-space: nowrap;
}

/* Two-pane layout used by Entities + Endpoints pages */
.af-two-pane {
    display: flex;
    height: 100%;
    overflow: hidden;
}

.af-two-pane-sidebar {
    width: 240px;
    flex-shrink: 0;
    border-right: 1px solid hsl(var(--border));
    overflow-y: auto;
}

.af-two-pane-detail {
    flex: 1;
    overflow-y: auto;
    padding: 1.5rem;
}
```

- [ ] **Step 5:** Delete `site.css`:
  ```
  git rm src/AspireForm/Ui/wwwroot/site.css
  ```

- [ ] **Step 6:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 7:** Commit:
  ```
  git add src/AspireForm/Ui/Components/App.razor src/AspireForm/Ui/Components/_Imports.razor
  git add src/AspireForm/Ui/wwwroot/
  git -c commit.gpgsign=false commit -m "feat(ui): adopt Blueprint CSS, add app.css, rewrite theme-interop.js, remove site.css"
  ```

---

## Task 7: New layout shell (AppSidebar, AppTopBar, ThemeSwitcherDropdown, MainLayout)

**Files:**
- Create: `src/AspireForm/Ui/Components/Layout/AppSidebar.razor`
- Create: `src/AspireForm/Ui/Components/Layout/AppTopBar.razor`
- Create: `src/AspireForm/Ui/Components/Layout/ThemeSwitcherDropdown.razor`
- Rewrite: `src/AspireForm/Ui/Components/Layout/MainLayout.razor`

- [ ] **Step 1:** Create `AppSidebar.razor`:

```razor
@inject NavigationManager Nav

<aside class="h-screen w-56 flex flex-col border-r border-border bg-card shrink-0">
    <div class="p-4 border-b border-border">
        <span class="font-semibold text-foreground">AspireForm</span>
        <span class="ml-2 text-xs text-muted-foreground">v1.0</span>
    </div>

    <nav class="flex-1 p-2 space-y-1 af-sidebar-nav overflow-y-auto">
        <SidebarNavItem Href="/" Icon="home" Label="Home" />
        <SidebarNavItem Href="/entities" Icon="database" Label="Entities" />
        <SidebarNavItem Href="/endpoints" Icon="globe" Label="Endpoints" />
        <SidebarNavItem Href="/theme" Icon="palette" Label="Theme" />
        <SidebarNavItem Href="/diagnostics" Icon="activity" Label="Diagnostics" />
    </nav>

    <div class="p-2 border-t border-border">
        <SidebarNavItem Href="/about" Icon="info" Label="About" />
    </div>
</aside>

@code {
    // Navigation active state is handled by SidebarNavItem using NavLink.
}
```

- [ ] **Step 2:** Create a `SidebarNavItem.razor` helper inside the Layout folder:

```razor
@inject NavigationManager Nav

<NavLink href="@Href" Match="@(Href == "/" ? NavLinkMatch.All : NavLinkMatch.Prefix)"
         class="flex items-center gap-2 px-3 py-2 rounded-md text-sm text-foreground hover:bg-accent hover:text-accent-foreground transition-colors"
         ActiveClass="bg-accent text-accent-foreground font-medium">
    <LucideIcon Name="@Icon" class="h-4 w-4 shrink-0" />
    <span>@Label</span>
</NavLink>

@code {
    [Parameter, EditorRequired] public string Href { get; set; } = "/";
    [Parameter, EditorRequired] public string Icon { get; set; } = "circle";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
}
```

- [ ] **Step 3:** Create `AppTopBar.razor`:

```razor
<header class="h-12 flex items-center border-b border-border bg-background px-4 gap-4 shrink-0">
    <div class="flex-1 text-sm text-muted-foreground">@Title</div>
    <div class="flex items-center gap-2">
        @if (PrimaryAction is not null)
        {
            @PrimaryAction
        }
        <ThemeSwitcherDropdown />
        <DarkModeToggle />
    </div>
</header>

@code {
    [CascadingParameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? PrimaryAction { get; set; }
}
```

- [ ] **Step 4:** Create `DarkModeToggle.razor` inside Layout folder:

```razor
@inject IThemeStore ThemeStore
@inject IJSRuntime Js
@rendermode InteractiveServer

<Button Variant="ghost" Size="icon" @onclick="ToggleAsync" title="Toggle dark mode">
    <LucideIcon Name="@(_isDark ? "sun" : "moon")" class="h-4 w-4" />
</Button>

@code {
    private bool _isDark;
    private IJSObjectReference? _module;

    protected override async Task OnInitializedAsync()
    {
        var activation = await ThemeStore.GetActiveAsync();
        _isDark = activation.DarkMode;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "/theme-interop.js");
    }

    private async Task ToggleAsync()
    {
        _isDark = !_isDark;
        await ThemeStore.SetDarkModeAsync(_isDark);
        if (_module is not null)
            await _module.InvokeVoidAsync("setDarkMode", _isDark);
    }
}
```

- [ ] **Step 5:** Create `ThemeSwitcherDropdown.razor`:

```razor
@inject IThemeStore ThemeStore
@inject IJSRuntime Js
@rendermode InteractiveServer

<DropdownMenu>
    <DropdownMenuTrigger>
        <Button Variant="ghost" Size="sm" class="gap-1">
            <LucideIcon Name="palette" class="h-4 w-4" />
            <span>@_activeName</span>
        </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent Align="end">
        @foreach (var theme in _themes)
        {
            <DropdownMenuItem @onclick="() => SwitchAsync(theme.Name)" class="gap-2">
                @if (theme.IsActive)
                {
                    <LucideIcon Name="check" class="h-4 w-4" />
                }
                else
                {
                    <span class="w-4" />
                }
                @theme.Name
            </DropdownMenuItem>
        }
        <DropdownMenuSeparator />
        <DropdownMenuItem>
            <a href="/theme" class="flex items-center gap-2 w-full">
                <LucideIcon Name="settings" class="h-4 w-4" />
                Manage themes
            </a>
        </DropdownMenuItem>
    </DropdownMenuContent>
</DropdownMenu>

@code {
    private IReadOnlyList<ThemeSummary> _themes = [];
    private string _activeName = "";
    private IJSObjectReference? _module;

    protected override async Task OnInitializedAsync()
    {
        _themes = await ThemeStore.ListAsync();
        _activeName = _themes.FirstOrDefault(t => t.IsActive)?.Name ?? "Theme";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "/theme-interop.js");
    }

    private async Task SwitchAsync(string name)
    {
        await ThemeStore.SetActiveAsync(name);
        _activeName = name;
        _themes = await ThemeStore.ListAsync();
        if (_module is not null)
            await _module.InvokeVoidAsync("switchTheme", name);
        StateHasChanged();
    }
}
```

- [ ] **Step 6:** Rewrite `MainLayout.razor`:

```razor
@inherits LayoutComponentBase
@inject IThemeStore ThemeStore

<CascadingValue Value="@_pageTitle">
    <div class="h-screen flex bg-background text-foreground overflow-hidden">
        <AppSidebar />
        <div class="flex-1 flex flex-col overflow-hidden">
            <AppTopBar />
            <main class="flex-1 overflow-auto p-0">
                @Body
            </main>
        </div>
    </div>
</CascadingValue>

@code {
    private string _pageTitle = "AspireForm";
}
```

- [ ] **Step 7:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```
  If Blueprint component names differ (e.g., `LucideIcon` vs `BbLucideIcon`), check the assembly XML and fix names. Blueprint uses `Bb`-prefixed names (e.g., `BbButton`, `BbCard`). Adjust all component names accordingly.

- [ ] **Step 8:** Commit:
  ```
  git add src/AspireForm/Ui/Components/Layout/
  git -c commit.gpgsign=false commit -m "feat(ui): add AppSidebar, AppTopBar, ThemeSwitcherDropdown, DarkModeToggle, rewrite MainLayout"
  ```

---

## Task 8: Rewrite Index.razor + About.razor + Diagnostics.razor

**Files:**
- Rewrite: `src/AspireForm/Ui/Components/Pages/Index.razor`
- Rewrite: `src/AspireForm/Ui/Components/Pages/About.razor`
- Rewrite: `src/AspireForm/Ui/Components/Pages/Diagnostics.razor`

- [ ] **Step 1:** Rewrite `Index.razor` using Blueprint cards:

```razor
@page "/"
@inject UiOptions Options

<PageTitle>AspireForm</PageTitle>

<div class="p-6 space-y-6">
    <div>
        <h1 class="text-2xl font-bold text-foreground">AspireForm</h1>
        <p class="text-muted-foreground mt-1">Project: @Options.ProjectDir</p>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <a href="/entities">
            <Card class="hover:bg-accent/50 transition-colors cursor-pointer">
                <CardHeader>
                    <CardTitle class="flex items-center gap-2">
                        <LucideIcon Name="database" class="h-5 w-5" /> Entities
                    </CardTitle>
                    <CardDescription>Browse and edit EF entity classes</CardDescription>
                </CardHeader>
            </Card>
        </a>
        <a href="/endpoints">
            <Card class="hover:bg-accent/50 transition-colors cursor-pointer">
                <CardHeader>
                    <CardTitle class="flex items-center gap-2">
                        <LucideIcon Name="globe" class="h-5 w-5" /> Endpoints
                    </CardTitle>
                    <CardDescription>Design and emit API endpoints</CardDescription>
                </CardHeader>
            </Card>
        </a>
        <a href="/theme">
            <Card class="hover:bg-accent/50 transition-colors cursor-pointer">
                <CardHeader>
                    <CardTitle class="flex items-center gap-2">
                        <LucideIcon Name="palette" class="h-5 w-5" /> Theme Editor
                    </CardTitle>
                    <CardDescription>Customise colors, radius, dark mode</CardDescription>
                </CardHeader>
            </Card>
        </a>
        <a href="/diagnostics">
            <Card class="hover:bg-accent/50 transition-colors cursor-pointer">
                <CardHeader>
                    <CardTitle class="flex items-center gap-2">
                        <LucideIcon Name="activity" class="h-5 w-5" /> Diagnostics
                    </CardTitle>
                    <CardDescription>View scan results and catalog health</CardDescription>
                </CardHeader>
            </Card>
        </a>
    </div>
</div>
```

- [ ] **Step 2:** Rewrite `About.razor`:

```razor
@page "/about"

<PageTitle>About — AspireForm</PageTitle>

<div class="p-6 max-w-lg space-y-4">
    <h1 class="text-2xl font-bold text-foreground">About AspireForm</h1>

    <Card>
        <CardContent class="pt-6 space-y-2">
            <div class="flex justify-between text-sm">
                <span class="text-muted-foreground">Version</span>
                <span class="font-medium">1.0.0</span>
            </div>
            <Separator />
            <div class="flex justify-between text-sm">
                <span class="text-muted-foreground">UI library</span>
                <span class="font-medium">Blazor Blueprint 3.10.2</span>
            </div>
            <Separator />
            <div class="flex justify-between text-sm">
                <span class="text-muted-foreground">License</span>
                <span class="font-medium">MIT</span>
            </div>
        </CardContent>
    </Card>

    <p class="text-sm text-muted-foreground">
        AspireForm constructs and configures .NET Aspire applications declaratively,
        layering a Terraform-style plan/apply loop and tweakcn-compatible theming
        on top of the official aspire CLI.
    </p>
</div>
```

- [ ] **Step 3:** Rewrite `Diagnostics.razor` using Blueprint Table + Alert. Keep all existing `@code` logic; only rewrite the markup. Replace `<div class="banner">` with `<Alert>` and the property tables with Blueprint `<Table>`.

- [ ] **Step 4:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 5:** Commit:
  ```
  git add src/AspireForm/Ui/Components/Pages/
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite Index, About, Diagnostics with Blueprint components"
  ```

---

## Task 9: Rewrite Entities.razor (Blueprint master-detail)

**Files:**
- Rewrite: `src/AspireForm/Ui/Components/Pages/Entities.razor`
- Delete: `src/AspireForm/Ui/Components/Entity/EntityList.razor`

- [ ] **Step 1:** Rewrite `Entities.razor`. Keep all `@code` logic intact — only rewrite the markup. Replace:
  - `<div class="two-pane">` → `<div class="af-two-pane">`
  - `<aside class="sidebar">` → `<div class="af-two-pane-sidebar">`
  - `<button @onclick="...">` → `<Button @onclick="...">`
  - `<input>` search → `<Input>`
  - `<div class="banner">` → `<Alert><AlertDescription>...</AlertDescription></Alert>`
  - `<div class="detail-tabs">` / `<div class="detail-tab">` → `<Tabs>` / `<TabsList>` / `<TabsTrigger>` / `<TabsContent>`
  - Sidebar item divs → Blueprint `<Button Variant="ghost">` or styled divs

- [ ] **Step 2:** Delete `EntityList.razor` (empty stub):
  ```
  git rm src/AspireForm/Ui/Components/Entity/EntityList.razor
  ```

- [ ] **Step 3:** Build:
  ```
  dotnet build src/AspireForm/AspireForm.csproj --nologo -v q
  ```

- [ ] **Step 4:** Commit:
  ```
  git add src/AspireForm/Ui/Components/Pages/Entities.razor
  git rm src/AspireForm/Ui/Components/Entity/EntityList.razor
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite Entities.razor with Blueprint master-detail, remove EntityList stub"
  ```

---

## Task 10: Rewrite Endpoints.razor (Blueprint master-detail)

Same pattern as Task 9 but for `Endpoints.razor`. Keep all `@code` logic; only rewrite markup using Blueprint primitives (Button, Input, Tabs, Alert, Table).

**Files:** Rewrite `src/AspireForm/Ui/Components/Pages/Endpoints.razor`

- [ ] **Step 1:** Rewrite markup, keeping code-behind intact.
- [ ] **Step 2:** Build + commit:
  ```
  git add src/AspireForm/Ui/Components/Pages/Endpoints.razor
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite Endpoints.razor with Blueprint components"
  ```

---

## Task 11: Rewrite dialog components

**Files:**
- Rewrite: `src/AspireForm/Ui/Components/Dialogs/NewEntityDialog.razor`
- Rewrite: `src/AspireForm/Ui/Components/Dialogs/AddPropertyDialog.razor`
- Rewrite: `src/AspireForm/Ui/Components/Dialogs/NewEndpointDialog.razor`

- [ ] **Step 1:** For each dialog, keep `@code` logic; rewrite markup wrapping the form in Blueprint `<Dialog>` / `<DialogContent>` / `<DialogHeader>` / `<DialogTitle>`. Replace `<input>` elements with `<Input>`, `<select>` with `<Select>`, `<button>` with `<Button>`.

- [ ] **Step 2:** Build + commit:
  ```
  git add src/AspireForm/Ui/Components/Dialogs/
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite dialogs with Blueprint Dialog component"
  ```

---

## Task 12: Rewrite sub-components (Entity/*, Endpoint/*)

**Files:** All `.razor` files under `src/AspireForm/Ui/Components/Entity/` and `src/AspireForm/Ui/Components/Endpoint/` except `EntityList.razor` (already deleted).

- [ ] **Step 1:** For each sub-component (EntityHeader, EntityPropertiesTab, EntityRelationshipsTab, EntityAttributesTab, EntityDabTab, EndpointHeader, EndpointParametersTab, EndpointAttributesTab, EndpointAuthTab, EndpointList): keep `@code` logic; replace hand-rolled `<div>`, `<button>`, `<input>` with Blueprint equivalents (Card, Table, TableRow, TableCell, Button, Input, Badge, etc.).

- [ ] **Step 2:** Build + run full suite:
  ```
  dotnet run --project tests/AspireForm.Tests
  ```

- [ ] **Step 3:** Commit:
  ```
  git add src/AspireForm/Ui/Components/Entity/ src/AspireForm/Ui/Components/Endpoint/
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite Entity/* and Endpoint/* sub-components with Blueprint primitives"
  ```

---

## Task 13: Rewrite Theme.razor (multi-theme editor)

**Files:**
- Rewrite: `src/AspireForm/Ui/Components/Pages/Theme.razor`

The multi-theme editor page. Contains:
1. **Theme picker row**: dropdown of all themes, "+ New" (duplicates), "Rename", "Delete", "Duplicate", "Import tweakcn JSON", "Export", "Set as active" buttons.
2. **Token editor body**: Light/Dark toggle pill; token list showing each token name + color swatch + hex input + `<input type="color">` picker; radius slider (`<input type="range" min="0" max="1" step="0.25">`).
3. **Save/discard** semantics: edits are local until "Save" is clicked.

- [ ] **Step 1:** Write the full `Theme.razor`. It must:
  - Be `@rendermode InteractiveServer`
  - Inject `IThemeStore`
  - Load themes on `OnInitializedAsync`
  - Keep local `Dictionary<string, string>` for in-progress edits (light + dark)
  - Have a `bool _editingDark` flag for the toggle pill
  - "Save" calls `ThemeStore.SaveAsync` with the edited token dict
  - "Import" shows a simple `<textarea>` modal (inline hidden/shown) that calls `TweakcnImporter.Parse` and sets local edits
  - "Export" serializes current theme as tweakcn-compatible JSON and shows in a copy modal
  - Blueprint `<Card>` wrapping each section, `<Button>`, `<Input>`, `<Select>` for dropdowns
  - Show `<Alert>` for any error messages

- [ ] **Step 2:** Build + commit:
  ```
  git add src/AspireForm/Ui/Components/Pages/Theme.razor
  git -c commit.gpgsign=false commit -m "feat(ui): rewrite Theme.razor as multi-theme editor with tweakcn import/export"
  ```

---

## Task 14: MCP theme tools (ThemeListTool, ThemeActivateTool, update ThemeShowTool)

**Files:**
- Create: `src/AspireForm/Mcp/Tools/ThemeListTool.cs`
- Create: `src/AspireForm/Mcp/Tools/ThemeActivateTool.cs`
- Modify: `src/AspireForm/Mcp/Tools/ThemeShowTool.cs`
- Modify: `src/AspireForm/Cli/McpCommand.cs`

- [ ] **Step 1:** Create `ThemeListTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Ui.Theme;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists all themes available in the current AspireForm project.</summary>
public sealed class ThemeListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ThemeListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_theme_list";

    /// <inheritdoc />
    public string Description =>
        "Lists all themes available in the current AspireForm project. " +
        "Returns an array of { name, description, isActive }.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var store = new ThemeStore(projectDir);
            var themes = await store.ListAsync(ct);
            var json = JsonSerializer.Serialize(themes, new JsonSerializerOptions { WriteIndented = true });
            return ToolResult.Ok(json);
        });
}
```

- [ ] **Step 2:** Create `ThemeActivateTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Ui.Theme;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: switches the active theme for the current AspireForm project.</summary>
public sealed class ThemeActivateTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ThemeActivateTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_theme_activate";

    /// <inheritdoc />
    public string Description =>
        "Switches the active theme by name. Use aspireform_theme_list to see available themes.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(
        new Dictionary<string, JsonObject>
        {
            ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
            ["name"]       = ToolBase.Str("Name of the theme to activate."),
        },
        required: ["name"]);

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var name = args["name"]?.GetValue<string>()
                ?? throw new ArgumentException("'name' is required.");
            var store = new ThemeStore(projectDir);
            await store.SetActiveAsync(name, ct);
            return ToolResult.Ok($"Theme '{name}' is now active.");
        });
}
```

- [ ] **Step 3:** Update `ThemeShowTool.cs` — change the `ExecuteAsync` to use the new multi-theme `ThemeStore` API: load the active theme, return its name + full token sets (light + dark) + radius:

```csharp
public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
    ToolBase.CatchKnownAsync(async () =>
    {
        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var store = new ThemeStore(projectDir);
        var activation = await store.GetActiveAsync(ct);
        var theme = await store.GetAsync(activation.ActiveName, ct);
        var themes = await store.ListAsync(ct);
        var result = new
        {
            activeName = activation.ActiveName,
            darkMode = activation.DarkMode,
            allThemes = themes.Select(t => t.Name).ToArray(),
            tokens = new { light = theme.Light, dark = theme.Dark },
            radius = theme.Radius,
        };
        return ToolResult.Ok(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    });
```

- [ ] **Step 4:** Update `McpCommand.cs` — in `BuildRegistry`, after the existing `ThemeShowTool` line, add:
  ```csharp
  r.Register(new ThemeListTool(projectDir));
  r.Register(new ThemeActivateTool(projectDir));
  ```
  Update the XML doc comment to say 42 total.

- [ ] **Step 5:** Build + commit:
  ```
  git add src/AspireForm/Mcp/Tools/ src/AspireForm/Cli/McpCommand.cs
  git -c commit.gpgsign=false commit -m "feat(mcp): add aspireform_theme_list + aspireform_theme_activate; update theme_show"
  ```

---

## Task 15: Update test assertions (McpCommandRegistrationTests, UiHostSmokeTests, EndToEndTests)

**Files:**
- Modify: `tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs`
- Modify: `tests/AspireForm.Tests/Ui/UiHostSmokeTests.cs`
- Check: `tests/AspireForm.Tests/Mcp/EndToEndTests.cs` (update if it asserts tool count)

- [ ] **Step 1:** Update `McpCommandRegistrationTests.cs`:
  - Change the method name and count assertion to 42
  - Add assertions for `aspireform_theme_list` and `aspireform_theme_activate`

- [ ] **Step 2:** Update `UiHostSmokeTests.cs` — add a test that asserts `/theme.css` returns 200 with `Content-Type: text/css`. Keep the existing index page test.

  ```csharp
  [Fact]
  public async Task UiHost_serves_theme_css_at_theme_css_endpoint()
  {
      var port = FindFreeTcpPort();
      var dir = Path.Combine(Path.GetTempPath(), $"af-ui-smoke-{Guid.NewGuid():N}");
      Directory.CreateDirectory(dir);
      var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

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
          resp.Content.Headers.ContentType?.MediaType.Should().Be("text/css");
          var body = await resp.Content.ReadAsStringAsync();
          body.Should().Contain(":root");
      }
      finally
      {
          cts.Cancel();
          try { await hostTask; } catch (OperationCanceledException) { } catch { }
          Directory.Delete(dir, recursive: true);
      }
  }
  ```

- [ ] **Step 3:** Check `EndToEndTests.cs` — if it asserts `r.All.Count == 40`, update to 42.

- [ ] **Step 4:** Run full test suite:
  ```
  dotnet run --project tests/AspireForm.Tests
  ```
  Target: ≥ 380 passing. The pre-existing `PluginLoaderE2ETests` failure (missing `project.assets.json` for Redis plugin) is a known infrastructure issue in the worktree — not introduced by this work; acceptable to have 1 skip/fail on that test.

- [ ] **Step 5:** Commit:
  ```
  git add tests/AspireForm.Tests/
  git -c commit.gpgsign=false commit -m "test: update McpCommandRegistration (42 tools), UiHostSmoke (/theme.css), EndToEnd tool count"
  ```

---

## Task 16: bUnit tests for new layout components

**Files:**
- Create: `tests/AspireForm.Tests/Ui/Layout/AppSidebarTests.cs`
- Create: `tests/AspireForm.Tests/Ui/Layout/ThemeSwitcherDropdownTests.cs`

- [ ] **Step 1:** Create `AppSidebarTests.cs`:

```csharp
using AspireForm.Ui.Components.Layout;
using AspireForm.Ui.Theme;
using AwesomeAssertions;
using Bunit;
using Xunit;

namespace AspireForm.Tests.Ui.Layout;

public sealed class AppSidebarTests : TestContext
{
    [Fact]
    public void AppSidebar_renders_navigation_links()
    {
        // AppSidebar uses NavLink which requires NavigationManager — bUnit provides it automatically.
        var cut = RenderComponent<AppSidebar>();
        var html = cut.Markup;
        html.Should().Contain("Entities");
        html.Should().Contain("Endpoints");
        html.Should().Contain("Theme");
    }
}
```

- [ ] **Step 2:** Create `ThemeSwitcherDropdownTests.cs`:

```csharp
using AspireForm.Ui.Components.Layout;
using AspireForm.Ui.Theme;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace AspireForm.Tests.Ui.Layout;

public sealed class ThemeSwitcherDropdownTests : TestContext
{
    [Fact]
    public async Task ThemeSwitcherDropdown_renders_theme_names_from_store()
    {
        var store = Substitute.For<IThemeStore>();
        store.ListAsync(default).Returns(new List<ThemeSummary>
        {
            new("Theme A", "desc", true),
            new("Theme B", "desc", false),
        });
        store.GetActiveAsync(default).Returns(new ThemeActivation("Theme A", false));
        Services.AddSingleton(store);

        var cut = RenderComponent<ThemeSwitcherDropdown>();
        var html = cut.Markup;
        html.Should().Contain("Theme A");
    }
}
```

  Note: if bUnit + NSubstitute are not already in the test project, check that `NSubstitute` is referenced (it likely is, given existing bUnit tests). If not, use a manual fake implementing `IThemeStore` instead.

- [ ] **Step 3:** Build + run layout tests:
  ```
  dotnet run --project tests/AspireForm.Tests -- --filter "FullyQualifiedName~AspireForm.Tests.Ui.Layout"
  ```

- [ ] **Step 4:** Commit:
  ```
  git add tests/AspireForm.Tests/Ui/Layout/
  git -c commit.gpgsign=false commit -m "test(ui): add bUnit tests for AppSidebar and ThemeSwitcherDropdown"
  ```

---

## Task 17: Update README + CHANGELOG

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1:** Add `[1.0.0]` entry to `CHANGELOG.md` at the top (after the header, before `[0.8.0]`):

```markdown
## [1.0.0] - 2026-05-26

### Added
- **Blazor Blueprint UI** — adopted BlazorBlueprint.Components 3.10.2 as the component library; all pages now use Blueprint Card, Table, Button, Dialog, Tabs, Alert, Input, Select, Badge, Separator, DropdownMenu, LucideIcon.
- **Left sidebar + top action bar** — persistent left-nav (Entities, Endpoints, Theme, Diagnostics, About) and a top bar with breadcrumb slot, primary action slot, theme switcher dropdown, and dark-mode toggle.
- **Multi-theme system** — 4 built-in themes: "AspireForm Light", "AspireForm Dark", "Slate Blue", "Emerald". Themes stored per-project under `.aspireform/themes/`. Token vocabulary is tweakcn/shadcn-compatible (oklch color values).
- **Theme editor** — create, rename, delete, duplicate themes; edit token colors (hex picker); adjust border radius; toggle light/dark preview; import from tweakcn JSON; export to tweakcn JSON.
- **Theme switcher** — dropdown in the top action bar for instant theme switching; dark mode toggle.
- **MCP tools** — `aspireform_theme_list` and `aspireform_theme_activate` added; `aspireform_theme_show` updated to include all theme names. Registry grows from 40 to 42 tools.
- **v0.7 migration** — if a legacy `.aspireform/theme.json` exists, it is automatically imported as "Migrated v0.7" theme on first run.

### Changed
- All Blazor pages rewritten to use Blueprint components; no hand-rolled `<button>` or `<div role="button">` remain.
- Layout changed from horizontal nav bar to left-sidebar + top-action-bar shell.
- `site.css` replaced by Blueprint's pre-built CSS (`/_content/BlazorBlueprint.Components/blazorblueprint.css`) + a small `app.css`.
- Theme token model upgraded: tweakcn-compatible vocabulary with oklch values; light + dark token buckets; per-theme radius.

### Removed
- `site.css` (replaced by Blueprint CSS + app.css)
- `EntityList.razor` stub (consolidated into `Entities.razor`)

### Notes
- Theme token values use oklch (tweakcn's current format, more accurate than HSL). HSL values in tweakcn imports are accepted as-is.
- `0.8.0 hotfix` (included): added `@rendermode InteractiveServer` to interactive pages; cached entity scan snapshots.
```

- [ ] **Step 2:** Add a "Theming AspireForm" section to `README.md` and update the UI screenshot reference. Add after the existing "Use the entity builder" section:

```markdown
## Theming AspireForm

AspireForm ships 4 built-in themes: **AspireForm Light**, **AspireForm Dark**, **Slate Blue**, and **Emerald**.

### Switching themes
Use the theme switcher dropdown in the top-right action bar, or via MCP:
```
aspireform_theme_list      # list all themes
aspireform_theme_activate  # switch active theme
```

### Editing themes
Open `aspireform ui` and navigate to **Theme**. From there you can:
- Edit token colors (hex input + color picker per token)
- Toggle between light and dark token buckets
- Adjust border radius
- Create, duplicate, rename, and delete themes
- Import/export tweakcn-compatible JSON (for sharing with shadcn/tweakcn tooling)

### Theme storage
Themes are stored per-project under `.aspireform/themes/` as JSON files. The `_active.json` file records the currently active theme.
```

- [ ] **Step 3:** Commit:
  ```
  git add README.md CHANGELOG.md
  git -c commit.gpgsign=false commit -m "docs: update README + CHANGELOG for 1.0.0 Blueprint adoption"
  ```

---

## Task 18: Pack + verify

**Files:** artifacts directory

- [ ] **Step 1:** Pack:
  ```
  dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts -p:EnableSourceControlManagerQueries=false
  ```
  Expected: `artifacts/AspireForm.1.0.0.nupkg` created.

- [ ] **Step 2:** Check bundle size:
  ```
  ls -lh ./artifacts/AspireForm.1.0.0.nupkg
  unzip -l ./artifacts/AspireForm.1.0.0.nupkg | grep -E "(css|wwwroot|staticwebassets|app)"
  ```

- [ ] **Step 3:** Verify Blueprint CSS is accessible (it ships in the package as a static web asset referenced via `/_content/BlazorBlueprint.Components/blazorblueprint.css`). The `.nupkg` should include the `build/Microsoft.AspNetCore.StaticWebAssets.props` entry from BlazorBlueprint.

- [ ] **Step 4:** Commit artifacts note (don't commit the binary):
  ```
  git -c commit.gpgsign=false commit --allow-empty -m "chore: AspireForm 1.0.0 packaged"
  ```

---

## Execution notes

**Push after every 3-5 task batches.** After task 5, after task 10, after task 15, after task 18.

**Blueprint component name prefix:** Blueprint uses `Bb`-prefixed component names (`BbButton`, `BbCard`, `BbTable`, etc.) not bare names. Verify by checking `BlazorBlueprint.Components.dll` public API or by checking a build error and adjusting. Every task that uses Blueprint components must confirm names compile.

**If a Blueprint component used in the spec doesn't exist** (e.g., no `BbDropdownMenu` yet in 3.10.2): fall back to a styled `<div>` equivalent for that specific component. Document in the final report. Do NOT block.

**PluginLoaderE2ETests** has a pre-existing failure due to missing project.assets.json for the Redis plugin fixture. This is not introduced by this work. The target is ≥ 380 tests passing with at most that 1 known failure.
