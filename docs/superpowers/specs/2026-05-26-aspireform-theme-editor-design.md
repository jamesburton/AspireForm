# AspireForm — Theme Editor (Sub-project #5.1) — Design Spec

- **Date:** 2026-05-26
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #5.1 of 5 — a visual token editor panel inside the existing `aspireform ui` Blazor app; ships as AspireForm 0.7.0.
- **Predecessors:**
  - Sub-project #1 (Core Engine) — AspireForm 0.2.0
  - Sub-project #2 (Vertical Catalog + 9 plugins) — AspireForm 0.3.x
  - Sub-project #3 (MCP server) — AspireForm 0.4.0
  - Sub-project #4a (EF Model Builder + Blazor UI shell) — AspireForm 0.5.0
  - Sub-project #4b (API-definition builder) — AspireForm 0.6.0 (separate worktree, reserved)

---

## 1. Context

The `aspireform ui` verb (shipped in 0.5.0) hosts Kestrel + Blazor Server inside the dnx tool process.
`src/AspireForm/Ui/wwwroot/site.css` governs all visual styling. Currently that file uses hard-coded
hex values throughout (`#1a73e8`, `#fff`, `#ddd`, etc.) — there are no CSS custom properties, so
there is nothing for a theme editor to bind to.

This sub-project adds a **Theme Editor** tab to the AspireForm UI shell: the user edits a defined
set of CSS design tokens (colors, border, spacing), sees a live preview, and the tokens persist to
`.aspireform/theme.json` in the active project directory. A Kestrel `/theme.css` endpoint converts
that JSON to a CSS `:root { --af-*: ... }` block at request time, so every page reload reflects the
active theme without static-file caching problems. `site.css` is refactored to `var(--af-*)` throughout.

The theme editor themes **the AspireForm UI shell only** — it does not emit any CSS into the user's
Aspire solution. This boundary is explicit and documented.

---

## 2. Locked design decisions

1. **CSS custom properties are the binding surface.** `site.css` is refactored to use `var(--af-*)` tokens. The token values come from a `:root { ... }` block served dynamically at `/theme.css`.
2. **Token store is `.aspireform/theme.json`.** One JSON file per AspireForm project directory. If absent, defaults apply (the same values the original site.css hard-coded).
3. **Kestrel serves `/theme.css` dynamically.** The endpoint reads `.aspireform/theme.json` at request time and emits the CSS property block. Cache-busting is handled by appending `?v={timestamp-ms}` when the link tag is refreshed after a save.
4. **All 14 tokens defined here are the complete v1 set.** No free-form CSS — the editor renders one `<input type="color">` or text field per token. Unknown tokens in `theme.json` are preserved on round-trip but not shown in the editor.
5. **Live preview via Blazor JS interop.** After each token change the component calls a tiny JS helper (`window.afTheme.reload()`) that swaps the `href` on the `<link id="af-theme">` tag to `?v={now}`, forcing the browser to re-fetch `/theme.css`. No page navigation needed.
6. **No MCP tools for theme mutation in v1.** The theme editor is UI-only. A single read-only `aspireform_theme_show` MCP tool is added so agents can inspect the active theme.
7. **No presets, no dark mode, no import/export in v1.** A "Reset to defaults" button is the only bulk operation.
8. **Version: AspireForm 0.7.0.** Leaves 0.6.0 to sub-project #4b. If #4b never ships, the user may renumber.

---

## 3. Token vocabulary

The following 14 tokens are the complete v1 set. Default values reproduce the visual appearance of `site.css` before this change.

| Token | CSS property name | Default | Description |
|---|---|---|---|
| `color-primary` | `--af-color-primary` | `#1a73e8` | Accent / link color |
| `color-primary-light` | `--af-color-primary-light` | `#e8f0fe` | Selected sidebar item bg |
| `color-text` | `--af-color-text` | `#222222` | Default body text |
| `color-text-muted` | `--af-color-text-muted` | `#888888` | De-emphasised text |
| `color-text-sub` | `--af-color-text-sub` | `#666666` | Topbar sub-label |
| `color-bg` | `--af-color-bg` | `#ffffff` | Page background |
| `color-bg-surface` | `--af-color-bg-surface` | `#fafafa` | Topbar / detail-tabs bg |
| `color-bg-sidebar` | `--af-color-bg-sidebar` | `#fcfcfc` | Sidebar background |
| `color-bg-hover` | `--af-color-bg-hover` | `#f4f4f4` | Hover state on sidebar items |
| `color-border` | `--af-color-border` | `#dddddd` | Main borders |
| `color-border-light` | `--af-color-border-light` | `#eeeeee` | Lighter borders |
| `color-danger-bg` | `--af-color-danger-bg` | `#ffeeee` | Danger button background |
| `color-danger-text` | `--af-color-danger-text` | `#aa0000` | Danger button text |
| `color-banner-bg` | `--af-color-banner-bg` | `#fff3cd` | Warning banner background |

Spacing and typography tokens are deferred to v2 (the existing relative units in site.css are fine for v1).

---

## 4. Architecture

```
aspireform ui (existing UiHost.cs)
    │
    ├── GET /theme.css             NEW Kestrel endpoint — emits :root { --af-*: ... } from theme.json
    │
    └── Blazor Server
        ├── Layout/MainLayout.razor   MODIFY — add <link id="af-theme" href="/theme.css?v=..."> in <head>
        ├── Pages/Theme.razor         NEW — @page "/theme", injects IThemeStore
        └── Components/Theme/
            ├── ThemeTokenEditor.razor   NEW — one color picker per token
            └── ThemePreviewPanel.razor  NEW — iframe or style-isolated preview (optional, v1 may omit)

src/AspireForm/Ui/Theme/
├── ThemeToken.cs               NEW — record: Name, CssVar, DefaultValue, Category
├── ThemeDefaults.cs            NEW — static array of all 14 ThemeToken instances
├── ThemeStore.cs               NEW — reads/writes .aspireform/theme.json; thread-safe
└── IThemeStore.cs              NEW — DI seam

wwwroot/site.css                MODIFY — replace all hard-coded color values with var(--af-*) calls
wwwroot/theme-interop.js        NEW — window.afTheme.reload() helper (swaps <link> href)
```

### 4.1 `/theme.css` endpoint

Registered in `UiHost.cs` via `app.MapGet("/theme.css", ...)`. The handler reads `IThemeStore`,
merges defaults with persisted values, and writes:

```css
:root {
  --af-color-primary: #1a73e8;
  /* ... one line per token ... */
}
```

Content-Type: `text/css`. Cache-Control: `no-store` (so each request is fresh).

### 4.2 `ThemeStore`

```csharp
public interface IThemeStore
{
    IReadOnlyDictionary<string, string> GetTokens();   // merged: defaults + persisted overrides
    Task SaveTokenAsync(string name, string value, CancellationToken ct = default);
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
```

`ThemeStore` is a singleton registered in `UiHost.cs`. It reads `{ProjectDir}/.aspireform/theme.json`
on first access (lazy) and writes on each `SaveTokenAsync`. Unknown keys in the JSON file are
preserved on read/write (round-trip safe). Thread safety: a `SemaphoreSlim(1,1)` guards writes.

### 4.3 Theme page and editor component

`Pages/Theme.razor` (`@page "/theme"`) renders a two-column layout:

- **Left column:** `<ThemeTokenEditor>` — a `<table>` with one row per token showing the token name,
  a `<input type="color">` (for color tokens), the current hex value in a text `<input>`, and a Reset
  link per row. On change: calls `IThemeStore.SaveTokenAsync`, then invokes JS `afTheme.reload()`.
- **Right column:** a short live-preview div that exercises the main CSS classes (`topbar`,
  `sidebar-item active`, `detail-tab active`, `button`, `button.danger`) so the user can see the
  effect without switching pages.

### 4.4 Navigation

`MainLayout.razor` gets a "Theme" link in the topbar `<nav>`:

```html
<a href="/theme">Theme</a>
```

### 4.5 JS interop

`wwwroot/theme-interop.js` (loaded via `<script>` in `App.razor`):

```js
window.afTheme = {
  reload: () => {
    const link = document.getElementById('af-theme');
    if (link) link.href = '/theme.css?v=' + Date.now();
  }
};
```

Called from `ThemeTokenEditor` after each save via `IJSRuntime.InvokeVoidAsync("afTheme.reload")`.

---

## 5. Error model

| Scenario | Behaviour |
|---|---|
| `.aspireform/` directory absent | `ThemeStore` creates it on first `SaveTokenAsync`. `GetTokens()` returns all defaults. |
| `theme.json` malformed JSON | `ThemeStore` logs a warning, treats file as empty (all defaults). Does not crash. |
| Color value invalid (non-`#RRGGBB`) | Blazor `<input type="color">` enforces valid hex. Text input validates on blur; saves are rejected with an inline error message if the value doesn't match `^#[0-9a-fA-F]{6}$`. |
| `/theme.css` endpoint throws | Returns HTTP 500 with a plain-text error body (no CSS). The browser retains the previously loaded theme. |
| Concurrent saves (two browser tabs) | `SemaphoreSlim` serialises writes; last write wins per token. |

---

## 6. MCP tool

One read-only MCP tool added to the existing tool registry:

**`aspireform_theme_show`** — returns the currently active token map as a JSON object
(`{ "color-primary": "#1a73e8", ... }`). No write tools in v1.

---

## 7. Testing

Mirror the bUnit pattern from `tests/AspireForm.Tests/Ui/`. All UI component tests use `Bunit.TestContext`, `AwesomeAssertions`, and `InternalsVisibleTo`.

| Test class | What it tests |
|---|---|
| `ThemeStoreTests` | `GetTokens` returns defaults when no file; `SaveTokenAsync` persists + round-trips; unknown keys preserved; concurrent saves don't corrupt. |
| `ThemeCssEndpointTests` | `/theme.css` returns `text/css`; contains all 14 `--af-*` properties; custom value overrides default. Uses `UiHost.RunAsync` on ephemeral port (mirroring `UiHostSmokeTests`). |
| `ThemeTokenEditorTests` | bUnit: renders one row per token; color input reflects current value; changing a value triggers `IThemeStore.SaveTokenAsync` (via a mock). |
| `ThemePageTests` | bUnit: `@page "/theme"` renders without error when `IThemeStore` returns defaults. |

---

## 8. Scope boundaries

**In scope:**
- Refactor `site.css` to CSS custom properties (all 14 tokens).
- `IThemeStore` + `ThemeStore` implementation.
- `/theme.css` Kestrel endpoint.
- `ThemeTokenEditor` Blazor component + `Theme.razor` page.
- Navigation link in `MainLayout.razor`.
- `theme-interop.js` for live reload.
- `aspireform_theme_show` MCP tool (read-only).
- Full test coverage per §7.
- `App.razor` `<link id="af-theme">` tag with cache-buster.

**Out of scope for v1:**
- Dark mode / light mode toggle.
- Preset themes (save / load named themes).
- Import/export as a standalone `.css` or `.json` file.
- Spacing or typography tokens (font size, font family, border-radius, spacing scale).
- Applying the theme to the user's own Blazor project or generated scaffold files.
- Per-project vs global themes — always per-project (`.aspireform/theme.json`).
- MCP write tools (`aspireform_theme_set`, `aspireform_theme_reset`).

---

## 9. Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Browser caches `/theme.css` despite `Cache-Control: no-store` | Low | Cache-busting query string (`?v={timestamp}`) is a belt-and-suspenders fallback |
| `App.razor` already uses `<head>` differently after #4a changes | Low | Read `App.razor` before writing; add `<link>` only if absent |
| `.aspireform/` directory requires elevated permissions in some CI environments | Very low | `ThemeStore` catches `UnauthorizedAccessException` and falls back to in-memory defaults |
| MS Blazor Server CSP headers interfere with dynamically loaded CSS | Low | Localhost-only dev tool; no CSP headers in UiHost |
| `<input type="color">` UX on Windows (system color picker) | Low | Acceptable for a dev tool; text input alongside it handles edge cases |

---

## 10. File map

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
- `src/AspireForm/Ui/UiHost.cs` — register `IThemeStore`, map `/theme.css` endpoint
- `src/AspireForm/Ui/Components/App.razor` — add `<link id="af-theme">` tag
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor` — add "Theme" nav link
- `src/AspireForm/Ui/Components/_Imports.razor` — add `@using AspireForm.Ui.Components.Theme`
- `src/AspireForm/Ui/wwwroot/site.css` — replace hard-coded values with `var(--af-*)` tokens
- `src/AspireForm/AspireForm.csproj` — version 0.5.0 → 0.7.0
- `CHANGELOG.md` — add `[0.7.0]` entry

**New (tests):**
- `tests/AspireForm.Tests/Ui/Theme/ThemeStoreTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemeCssEndpointTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemeTokenEditorTests.cs`
- `tests/AspireForm.Tests/Ui/Theme/ThemePageTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ThemeShowToolTests.cs`
