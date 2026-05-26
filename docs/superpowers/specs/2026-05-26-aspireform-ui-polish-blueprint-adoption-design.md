# AspireForm — UI Polish + Blazor Blueprint Adoption (Sub-project #6) — Design Spec

- **Date:** 2026-05-26
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #6 — adopt [Blazor Blueprint UI](https://blazorblueprintui.com) as the component library, rewrite the layout shell, replace the ad-hoc theme editor with a multi-theme tweakcn-compatible editor, and polish the visual style across the entire `aspireform ui` Blazor app.
- **Predecessors:**
  - Sub-project #1 (Core Engine) — **AspireForm 0.2.0**
  - Sub-project #2 (Vertical Catalog + 9 plugins) — **AspireForm 0.3.x**
  - Sub-project #3 (MCP server) — **AspireForm 0.4.0**
  - Sub-project #4a (EF Model Builder) — **AspireForm 0.5.0**
  - Sub-project #5.1 (Theme Editor v1) — **AspireForm 0.7.0** (rewritten by this spec)
  - Sub-project #4b (API-definition Builder) — **AspireForm 0.8.0**
  - **Hotfix on 0.8.0** — added `@rendermode InteractiveServer` to interactive pages so buttons actually wire to a SignalR circuit; cached scan snapshots on `IEntityCatalogService` so `/diagnostics` no longer re-scans

---

## 1. Context

The `aspireform ui` Blazor app shipped progressively across #4a, #4b, and #5.1 with hand-rolled CSS and `<button>`-on-`<div>` styling. UAT against `main` surfaced three issues that this spec addresses:

1. **The site looks unfinished.** Hand-rolled colors, no proper spacing scale, no professional component vocabulary.
2. **The theme editor is too limited.** A flat 14-token list with no preset/palette concept, no light/dark dual-edit, no multi-theme support, no interop with the shadcn/ui ecosystem.
3. **Layout is thin.** A single horizontal nav bar with text links; no persistent navigation, no project context, no breadcrumbs.

Adopting [Blazor Blueprint UI](https://github.com/blazorblueprintui/ui) (a shadcn/ui port for Blazor, 115+ components, Apache 2.0, Tailwind-based) gives us a coherent component library, a tweakcn-compatible theming model, and a professional baseline visual style. This sub-project ships as **AspireForm 1.0.0** — the "first polished release."

---

## 2. Locked design decisions

1. **Component library** — `BlazorBlueprint.Components` NuGet, Apache 2.0. Replaces all hand-rolled primitives.
2. **Tailwind delivery** — pre-built static CSS bundled with the dnx tool. No Node runtime; no CDN runtime fetch.
3. **Migration scope** — big-bang: layout shell + primitives + page rewrites + theme editor, all in one release.
4. **Layout shell** — left sidebar nav (with section labels + icons) + top action bar (breadcrumbs + primary action + theme switcher).
5. **Theme model** — tweakcn-compatible JSON shape, multi-theme support, project-scoped. Each theme has light + dark variants.
6. **Theme switcher** — dropdown in the top action bar, right-aligned. Switching is instant (live CSS reload).
7. **Theme editor** — full token swatch editor with light/dark dual-edit + radius preset + tweakcn import/export + new/rename/delete/duplicate.
8. **Default themes shipped** — 4: "AspireForm Light", "AspireForm Dark", "Slate Blue", "Emerald".
9. **Dark mode** — per-theme (each theme has its own dark variant tokens); global toggle in top bar applies to the active theme.
10. **Existing CSS** — `site.css` is replaced. Tailwind output is the primary stylesheet; a small `app.css` carries AspireForm-specific overrides (typography, density tweaks).
11. **Version** — AspireForm 1.0.0. AspireForm.Annotations stays at 0.2.0 (untouched).
12. **Out of scope (v1)** — mobile responsive (dev tool, desktop-only), density/font controls in the editor, accessibility audit beyond reasonable use of Blueprint's accessible primitives.

---

## 3. Architecture

```
src/AspireForm/
├── AspireForm.csproj                                 MODIFY — bump 0.8.0 → 1.0.0; add BlazorBlueprint.Components
├── Ui/
│   ├── UiHost.cs                                     MODIFY — register IThemeStore (multi-theme), wire Blueprint theme tokens to /theme.css endpoint, register theme-switcher service
│   ├── BrowserLauncher.cs                            UNCHANGED
│   ├── Theme/
│   │   ├── ThemeToken.cs                             MODIFY — extend to match tweakcn shape (HSL color values, named buckets, radius)
│   │   ├── ThemeDefaults.cs                          REWRITE — ship 4 default themes (Light, Dark, Slate Blue, Emerald)
│   │   ├── IThemeStore.cs                            REWRITE — multi-theme API (list, get, save, delete, rename, duplicate, getActive, setActive)
│   │   ├── ThemeStore.cs                             REWRITE — file-per-theme persistence under .aspireform/themes/
│   │   ├── ThemeManifest.cs                          NEW — represents the active-theme pointer (.aspireform/themes/_active.json)
│   │   └── TweakcnImporter.cs                        NEW — parse tweakcn JSON → ThemeTokenSet (light + dark)
│   ├── Components/
│   │   ├── _Imports.razor                            MODIFY — add Blueprint usings
│   │   ├── App.razor                                 MODIFY — Tailwind link tag + Blueprint base script
│   │   ├── Routes.razor                              UNCHANGED
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor                      REWRITE — left sidebar + top action bar shell
│   │   │   ├── AppSidebar.razor                      NEW — sidebar nav component (entities, endpoints, theme, diagnostics, about)
│   │   │   ├── AppTopBar.razor                       NEW — breadcrumbs + primary action slot + theme switcher
│   │   │   └── ThemeSwitcherDropdown.razor           NEW — dropdown showing all themes; active highlighted
│   │   ├── Pages/
│   │   │   ├── Index.razor                           REWRITE — project picker landing with Blueprint cards
│   │   │   ├── Entities.razor                        REWRITE — Blueprint master-detail (Card, Table, Tabs, Dialog)
│   │   │   ├── Endpoints.razor                       REWRITE — same pattern
│   │   │   ├── Theme.razor                           REWRITE — multi-theme editor (see §6)
│   │   │   ├── Diagnostics.razor                     REWRITE — Blueprint Table + Alert components
│   │   │   └── About.razor                           REWRITE — Blueprint typography + version card
│   │   ├── Entity/                                   MODIFY — replace hand-rolled primitives in each sub-component
│   │   ├── Endpoint/                                 MODIFY — same
│   │   ├── Dialogs/                                  REWRITE — use Blueprint Dialog component
│   │   └── Theme/                                    REWRITE — see §6 for full component breakdown
│   └── wwwroot/
│       ├── tailwind.css                              NEW (vendored) — pre-built Tailwind output + Blueprint base styles
│       ├── app.css                                   NEW — small AspireForm-specific overrides
│       ├── theme-interop.js                          MODIFY — switch theme + apply HSL variables
│       └── site.css                                  REMOVE
└── Mcp/Tools/
    ├── ThemeShowTool.cs                              MODIFY — return active theme + list of all theme names
    ├── ThemeListTool.cs                              NEW — list all themes in current project
    ├── ThemeActivateTool.cs                          NEW — switch the active theme
    └── (export/import tools deferred to #6.1)

tests/AspireForm.Tests/
├── Ui/
│   ├── Theme/
│   │   ├── ThemeStoreTests.cs                        REWRITE — multi-theme persistence, active-pointer
│   │   ├── TweakcnImporterTests.cs                   NEW — parse tweakcn JSON fixtures
│   │   └── ...                                       MODIFY existing tests
│   ├── Layout/
│   │   ├── AppSidebarTests.cs                        NEW (bUnit)
│   │   ├── AppTopBarTests.cs                         NEW (bUnit)
│   │   └── ThemeSwitcherDropdownTests.cs             NEW (bUnit)
│   └── UiHostSmokeTests.cs                           MODIFY — assert Tailwind CSS served at /tailwind.css
└── Mcp/Tools/
    └── ThemeToolsTests.cs                            EXPAND — list + activate + show
```

### 3.1 Tailwind delivery pipeline

The Tailwind output is **generated once during development** (offline) and **vendored into source control** at `src/AspireForm/Ui/wwwroot/tailwind.css`. The vendored file ships with the dnx tool.

Generation procedure (documented for maintenance):

1. Install Tailwind CLI: `npm install -D tailwindcss @tailwindcss/cli` in a `tooling/tailwind/` directory (not in the .NET project — kept separate to avoid Node entering the C# dev loop).
2. Configure `tailwind.config.js` with content paths pointing at `src/AspireForm/Ui/**/*.razor` and the Blueprint sources.
3. Run `npx tailwindcss -i ./tooling/tailwind/input.css -o ../../src/AspireForm/Ui/wwwroot/tailwind.css --minify`.
4. Commit the regenerated CSS.

**Implementation note:** Blueprint may ship a pre-built CSS bundle directly. The agent should check on first task; if a usable pre-built file exists in the Blueprint package (e.g., as a static asset), vendoring it directly is simpler than running Tailwind ourselves. The vendored-output approach is the fallback.

### 3.2 Data flow — page render with Blueprint + active theme

```
Browser request
    ↓
Kestrel + Blazor Server
    ↓ (SSR pre-render, then InteractiveServer over SignalR)
MainLayout.razor: links /tailwind.css + /theme.css + /app.css + /_framework/blazor.web.js
    ↓
Theme.css endpoint (in UiHost): reads active theme via IThemeStore, emits :root { --tw-* } block
    ↓
Page renders with Blueprint components using Tailwind utility classes + CSS variables
    ↓
User toggles theme via ThemeSwitcherDropdown → POST to switch endpoint → JS reloads /theme.css link
```

---

## 4. Blazor Blueprint integration

Add to `src/AspireForm/AspireForm.csproj`:

```xml
<PackageReference Include="BlazorBlueprint.Components" Version="<latest>" />
```

Add to `_Imports.razor`:

```razor
@using BlazorBlueprint.Components
```

Blueprint components used in v1 (representative — agent verifies during execution):

| Blueprint component | Replaces |
|---|---|
| `<Button>` | All hand-rolled `<button>` elements |
| `<Card>` / `<CardHeader>` / `<CardContent>` | Detail panels, sidebar items |
| `<Tabs>` / `<TabsList>` / `<TabsTrigger>` / `<TabsContent>` | The hand-rolled tab strips in Entities + Endpoints |
| `<Dialog>` / `<DialogTrigger>` / `<DialogContent>` | NewEntityDialog, NewEndpointDialog, AddPropertyDialog |
| `<Input>` / `<Textarea>` / `<Label>` | All form inputs |
| `<Select>` / `<SelectTrigger>` / `<SelectContent>` / `<SelectItem>` | Dropdowns including ThemeSwitcher |
| `<Table>` / `<TableHeader>` / `<TableBody>` / `<TableRow>` / `<TableCell>` | The entity/endpoint property tables |
| `<Alert>` / `<AlertDescription>` | Banner-style messages (load errors, diagnostics) |
| `<Toast>` / `<Toaster>` | Action confirmations (deferred to v1.1 if not trivial) |
| `<Badge>` | Cardinality labels, severity badges, etc. |
| `<Separator>` | Visual dividers |
| `<ScrollArea>` | Sidebar lists |
| `<DropdownMenu>` | Theme switcher, per-row actions |

---

## 5. Layout shell

### 5.1 `MainLayout.razor` structure

```razor
@inherits LayoutComponentBase

<div class="h-screen flex bg-background text-foreground">
    <AppSidebar />
    <div class="flex-1 flex flex-col overflow-hidden">
        <AppTopBar />
        <main class="flex-1 overflow-auto">@Body</main>
    </div>
</div>
```

### 5.2 `AppSidebar.razor`

Persistent left sidebar:
- Top: AspireForm logo + version badge
- Section list (Entities, Endpoints, Theme, Diagnostics) — each a `NavLink` styled as a Blueprint sidebar item with icon (Lucide icons via Blueprint) + label + active highlight
- Bottom: "About" + a placeholder for future "Settings" entry

### 5.3 `AppTopBar.razor`

Single-row top bar:
- Left: dynamic breadcrumb (e.g., "Entities › Book")
- Center: empty (room for future actions)
- Right: primary action slot (e.g., "+ New Entity" rendered by the active page) + `ThemeSwitcherDropdown` + dark-mode toggle

The primary action slot is wired via a cascading value (`AppPageActions`) so each page can register its own. If a page provides no action, the slot is empty.

### 5.4 `ThemeSwitcherDropdown.razor`

A Blueprint `<DropdownMenu>` showing all themes returned by `IThemeStore.ListAsync()`. Active theme has a checkmark. Selecting a theme calls `IThemeStore.SetActiveAsync(name)` and triggers a JS reload of `/theme.css`. Includes a "Manage themes →" link to `/theme`.

---

## 6. Theme model + editor

### 6.1 Theme JSON shape (tweakcn-compatible)

```json
{
  "name": "Slate Blue",
  "description": "Cool slate background, blue accents",
  "radius": 0.5,
  "tokens": {
    "light": {
      "background": "hsl(0 0% 100%)",
      "foreground": "hsl(222.2 84% 4.9%)",
      "primary": "hsl(221.2 83.2% 53.3%)",
      "primary-foreground": "hsl(210 40% 98%)",
      "secondary": "hsl(210 40% 96.1%)",
      "...": "..."
    },
    "dark": {
      "background": "hsl(222.2 84% 4.9%)",
      "foreground": "hsl(210 40% 98%)",
      "...": "..."
    }
  }
}
```

Token names match tweakcn's vocabulary: `background`, `foreground`, `primary`, `primary-foreground`, `secondary`, `secondary-foreground`, `muted`, `muted-foreground`, `accent`, `accent-foreground`, `destructive`, `destructive-foreground`, `border`, `input`, `ring`, `card`, `card-foreground`, `popover`, `popover-foreground` (≈20 tokens).

### 6.2 Persistence

`.aspireform/themes/` per-project directory:

- `aspireform-light.json` (default, shipped on first run)
- `aspireform-dark.json` (default)
- `slate-blue.json` (default)
- `emerald.json` (default)
- `_active.json` — `{ "active": "aspireform-light", "darkMode": false }`

Renaming a theme renames the file; deleting removes the file (with confirm). The active pointer is updated automatically when a deletion removes the active theme (falls back to "aspireform-light").

### 6.3 `IThemeStore` API

```csharp
public interface IThemeStore
{
    Task<IReadOnlyList<ThemeSummary>> ListAsync();        // names + descriptions
    Task<Theme> GetAsync(string name);
    Task SaveAsync(Theme theme);                          // upsert by name
    Task DeleteAsync(string name);
    Task<string> DuplicateAsync(string sourceName, string newName);
    Task RenameAsync(string oldName, string newName);
    Task<ThemeActivation> GetActiveAsync();               // { name, darkMode }
    Task SetActiveAsync(string name);
    Task SetDarkModeAsync(bool dark);
    Task ResetToDefaultsAsync();                          // reset to factory themes
}
```

### 6.4 Theme editor UI (`/theme`)

Single page with two main sections:

**Top:** Theme picker row
- Dropdown of all themes (active highlighted)
- "+ New" button (duplicates current)
- "Rename" / "Delete" / "Duplicate" buttons
- "Import tweakcn JSON" button (paste modal)
- "Export tweakcn JSON" button (copies to clipboard)
- "Set as active" button (only enabled when editing a non-active theme)

**Body:** Token editor
- Light/dark toggle pill at top
- Token swatches grouped by purpose (Base, Primary, Secondary, Accent, Destructive, Borders & Inputs, Card, Popover)
- Each swatch shows the current color + hex input + HSL sliders (H/S/L)
- Radius slider (0 → 1, 0.25 step) — visible at the top
- Live preview panel showing how a Blueprint Card, Button, and Input look with the current edits

**Save semantics:** edits are local until "Save" is clicked. Switching themes prompts to discard or save unsaved changes.

---

## 7. MCP tool additions

| Tool | Inputs | Returns |
|---|---|---|
| `aspireform_theme_show` (MODIFY) | none | active theme name + tokens (light + dark) + radius |
| `aspireform_theme_list` (NEW) | none | array of `{ name, description, isActive }` |
| `aspireform_theme_activate` (NEW) | `name` | confirmation; switches active |

The full registry grows from **40 to 42** tools (existing 40 + 2 new).

Import/export tools (write-side) deferred to #6.1.

---

## 8. Migration mechanics

### 8.1 Files removed

- `src/AspireForm/Ui/wwwroot/site.css` — replaced
- `src/AspireForm/Ui/Components/Entity/EntityList.razor` — empty stub from #4a; was reserved for future use; remove since the sidebar list is consolidated into `Entities.razor` rewrite
- Hand-rolled CSS classes throughout the tree

### 8.2 Files heavily modified

- All `.razor` files under `src/AspireForm/Ui/Components/` — components rewritten in terms of Blueprint primitives
- `MainLayout.razor` — completely new
- `_Imports.razor` — adds Blueprint usings; keeps existing ones
- `UiHost.cs` — DI changes for new theme model; new `/theme.css` content; new `/themes/set-active` endpoint

### 8.3 Files added

- All `Layout/*` components
- All `Theme/Components/*` (token editor, swatches, sliders)
- `tooling/tailwind/` directory (source for the vendored CSS — kept out of `src/` since it's a build-time-only artifact)

### 8.4 Tests

bUnit tests should EXIST for the new components but be **less prescriptive about exact markup**, since Blueprint internals can change. Focus tests on behavior (clicking a swatch updates the bound token; pressing "Set as active" calls the service) rather than CSS class strings.

`UiHostSmokeTests.cs` should assert that `/tailwind.css` returns 200 with `Content-Type: text/css` and `/theme.css` returns 200 with a `:root { --tw-* }` block reflecting the active theme.

Target: ~30 new/rewritten tests, total suite stays ≥ 380.

---

## 9. Error model

- **Tailwind CSS missing**: if `wwwroot/tailwind.css` isn't present at startup (shouldn't happen — it's vendored), UiHost logs a clear error and serves an empty stylesheet (UI degrades to unstyled HTML but still renders).
- **Active theme missing**: if `_active.json` points at a deleted theme, `IThemeStore.GetActiveAsync` returns the first available theme and rewrites the pointer.
- **Malformed theme JSON**: `ThemeStore.GetAsync` throws `ThemeLoadException` with the file path. The theme picker shows the broken theme as disabled with a hover tooltip explaining; users can delete or fix manually.
- **tweakcn import malformed**: `TweakcnImporter.Parse` throws `TweakcnImportException` with line/property pointing at the issue. The import dialog catches and displays inline.

---

## 10. Testing strategy

- **`ThemeStore`**: file-IO fixture tests against a per-test temp dir. Cover save/load/delete/rename/duplicate/setActive + defaults installation.
- **`TweakcnImporter`**: fixture JSON files in `tests/.../Fixtures/tweakcn/` — valid sample, missing-tokens sample, malformed sample.
- **`IThemeStore` integration**: scenario test — create new theme, switch active, edit, save, delete.
- **Blazor components**: bUnit tests for `ThemeSwitcherDropdown` (lists themes, calls service on select), `AppSidebar` (active route highlighted), `AppTopBar` (renders primary action slot).
- **Pages**: bUnit tests for `Theme.razor` (token editor renders), `Entities.razor` (rewritten — sidebar + master-detail), `Endpoints.razor` (same), `Diagnostics.razor` (uses Blueprint Table/Alert).
- **Smoke**: `UiHostSmokeTests` — Tailwind CSS served; theme CSS served; root page returns 200.
- **End-to-end**: the existing `PlanSmokeTests` and MCP `EndToEndTests` should continue to pass unchanged (no provider/CLI behavior change).

---

## 11. Scope boundaries — explicitly NOT in v1

- **Mobile / tablet responsive layout** — dev tool, desktop-only assumption
- **Density / font-size controls in the editor** — Blueprint exposes them but UI surface deferred
- **Accessibility audit beyond the accessible defaults Blueprint provides** — best-effort
- **Multi-project theme sharing** (a theme used across many projects) — themes are per-project; copy-paste tweakcn JSON to share
- **Theme inheritance / extends** — each theme is standalone; no parent themes
- **Custom logo / branding upload** — deferred
- **i18n / localization** — English only
- **Toast notifications** — Blueprint has them but binding to all action sites is deferred; v1 keeps inline error banners
- **Drag-and-drop entity canvas** — that's sub-project #5.3, separate

---

## 12. Risks & open questions

1. **Blueprint package availability / API stability.** Blueprint is a newer project. The agent verifies the package exists, lists its current public API, and pins the version. If a critical component is missing (e.g., no DropdownMenu yet), the spec includes a fallback to hand-rolling that one component while using Blueprint for the rest.
2. **Pre-built Tailwind CSS not shipped by Blueprint.** Fallback: generate locally via Tailwind CLI in a `tooling/tailwind/` directory; commit the output. Process documented in §3.1.
3. **tweakcn JSON schema drift.** tweakcn isn't a formal spec — the JSON shape is whatever tweakcn currently emits. Pin to the current shape (capture a snapshot in the spec / tests); document the import as best-effort across schema versions.
4. **Bundle size.** Tailwind output + Blueprint CSS could land at 200-500 KB compressed. Acceptable for a dev tool but worth measuring during pack — flag in the agent's final report.
5. **Hot-reload of Tailwind during development.** With the vendored-CSS approach, adding a new Tailwind class to a Razor file requires regenerating `tailwind.css` and committing. Acceptable; documented in CONTRIBUTING-like notes.
6. **Multi-theme state on disk.** Defaults are installed on first run. If the user already has a `theme.json` from the v0.7 single-theme model, we migrate it to a new theme named "Migrated v0.7" and set it active. Migration runs once on `IThemeStore` initialization.

---

## 13. Definition of done (sub-project #6 / AspireForm 1.0.0)

- All pages render using Blazor Blueprint components; no hand-rolled `<button>` or `<div role="button">` remain in the UI tree
- Left sidebar + top action bar shell working across all pages
- Multi-theme editor: 4 default themes ship; user can create / rename / delete / duplicate / import-tweakcn / export-tweakcn / set active / edit tokens (light + dark) / edit radius
- Theme switcher dropdown in top bar; switching is instant
- `aspireform_theme_list` + `aspireform_theme_activate` MCP tools registered; existing `aspireform_theme_show` updated to include active-name + all-themes summary
- Test suite green; total ≥ 380 tests
- `dotnet pack` produces `AspireForm.1.0.0.nupkg`; vendored `tailwind.css` is included
- README updated with screenshots and a "Theming AspireForm" section explaining the multi-theme model + tweakcn round-trip
- CHANGELOG `[1.0.0]` entry covers the Blueprint adoption, multi-theme system, layout shell, and the prior 0.8.0 hotfix
- Bundle size measured and noted in the final report
