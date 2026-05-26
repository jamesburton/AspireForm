# Changelog

All notable changes to AspireForm are recorded here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] — 2026-05-26

### Added

- **BlazorBlueprint component library** — all pages and components migrated from bespoke CSS to
  [BlazorBlueprint.Components 3.10.2](https://github.com/blazorblueprint/blazorblueprint) (shadcn/ui
  port for Blazor). Tailwind utility classes replace hand-written `site.css`; Blueprint CSS served
  from `/_content/BlazorBlueprint.Components/blazorblueprint.css`.
- **Multi-theme editor** — `aspireform ui /theme` redesigned as a full two-pane editor:
  sidebar for listing, selecting, creating, duplicating, and deleting themes; detail panel for
  activating, editing light/dark token buckets, adjusting border-radius, importing tweakcn JSON,
  and exporting the current theme definition.
- **`TokenBucketEditor`** sub-component — editable token table with live oklch color preview
  swatches for all 19 shadcn/tweakcn token names.
- `aspireform_theme_list` and `aspireform_theme_activate` MCP tools — agent-readable theme
  listing and switching (registry grows from 40 to 42 tools).
- `copyToClipboard()` JavaScript helper in `theme-interop.js` for theme export UX.
- `AppSidebar`, `AppTopBar`, `ThemeSwitcherDropdown`, `DarkModeToggle` layout components —
  persistent nav sidebar and top-bar header; theme switcher dropdown; dark-mode toggle.

### Changed

- `IThemeStore` rewritten from single-theme to multi-theme API: `ListAsync`, `GetAsync`,
  `SaveAsync`, `DeleteAsync`, `DuplicateAsync`, `RenameAsync`, `GetActiveAsync`, `SetActiveAsync`,
  `SetDarkModeAsync`, `ResetToDefaultsAsync`. Ships two built-in themes: "AspireForm Light" and
  "AspireForm Dark".
- `ThemeStore` now persists themes as individual JSON files under
  `.aspireform/themes/<name>.json` with an `_active.json` manifest.
- `ThemeTokenNames.All` replaces `ThemeDefaults.Tokens` — 19 standard shadcn/tweakcn token names.
- `aspireform_theme_show` tool updated — returns full multi-theme JSON with `activeName`,
  `darkMode`, `allThemes`, `tokens.light`, `tokens.dark`, and `radius` fields.
- Layout components (`MainLayout`, pages, dialogs, entity/endpoint sub-components) fully
  refactored to Blueprint components: `BbButton`, `BbCard`, `BbBadge`, `BbAlert`, `BbSeparator`,
  `BbInput`, `BbTabs`, `BbDialog`, `BbDropdownMenu`.

### Breaking

- `IThemeStore`: old single-theme interface is replaced entirely. If you hold a reference to
  `IThemeStore` (only relevant if you registered a custom implementation), implement the new
  10-method interface.
- `ThemeDefaults.Tokens` removed — use `ThemeTokenNames.All` instead.

## [0.8.0] — 2026-05-26

### Added
- **API endpoint builder** — code-first Minimal API authoring powered by Roslyn.
  - `AspireForm.Annotations 0.2.0` adds four new attributes: `[ApiEndpoint(route, method)]`, `[ApiAuth(policy)]`, `[ApiTag(tag)]`, `[ApiSummary(text)]`.
  - `RoslynEndpointScanner` walks `MSBuildWorkspace` semantic model to discover all `[ApiEndpoint]`-decorated methods; extracts route parameters (including constraints and optional markers), auth policy, tags, summary, and method-level attributes.
  - `RoslynEndpointMutator` applies `EndpointChangeRequest` DSL mutations (`CreateEndpoint`, `DeleteEndpoint`, `AddParameter`, `RemoveParameter`, `SetEndpointAttribute`, `ClearEndpointAttribute`, `SetAuthPolicy`) using Roslyn syntax rewriting with transactional file writes. Expression-bodied methods are rejected with a clear diagnostic (V1 constraint).
  - `EndpointEmitter` renders `_Endpoints.g.cs` with a `MapAspireFormEndpoints` extension method using fluent Minimal API chaining.
  - Built-in `api-endpoints` module provider registered in `ProviderRegistry.Default()`. Accepts `inputs.projectPath`; emits the generated file as `OwnershipMode.Managed`.
  - 10 new MCP endpoint tools (registry grows from 30 to 40): `aspireform_endpoint_{list,show,emit,create,delete}`, `aspireform_endpoint_parameter_{add,remove}`, `aspireform_endpoint_auth_set`, `aspireform_endpoint_attribute_{set,clear}`.
  - `/endpoints` page in `aspireform ui`: sidebar with search and "+ New Endpoint", detail pane with Parameters / Auth / Attributes tabs. `NewEndpointDialog` modal for create flow.

> **Note on version numbering:** The API endpoint builder was developed in parallel with the Theme Editor (#5.1) under sub-project #4b. It was originally planned to ship as 0.6.0 but the Theme Editor work merged first as 0.7.0, so the API endpoint builder ships as 0.8.0. There is no 0.6.0 release.

## [0.7.0] — 2026-05-26

### Added
- **Theme Editor** — `aspireform ui` now includes a `/theme` page for editing the AspireForm UI color
  tokens. Changes are saved to `.aspireform/theme.json` and take effect immediately via a live CSS reload.
- `aspireform_theme_show` MCP tool (read-only) — returns the active theme token map as JSON.

### Changed
- `site.css` refactored to CSS custom properties (`var(--af-*)`); all 14 color tokens are now themeable.

## [0.5.0] - 2026-05-25

### Added
- New `aspireform ui` verb — Kestrel + Blazor Server EF model builder. Localhost-only.
- 12 new MCP entity tools: `aspireform_entity_{list,show,create,delete}`, `aspireform_property_{add,remove,rename}`, `aspireform_attribute_{set,clear}`, `aspireform_relationship_{add,remove}`, `aspireform_dbcontext_list`. Registry grows from 17 to 29 tools.
- `EntityCatalog` namespace with Roslyn-backed scanner and mutator (`MSBuildWorkspace`, semantic-safe rename, transactional file writes).
- New sibling package `AspireForm.Annotations 0.1.0` with `[DabExpose]`, `[DabPath]`, `[DabPermission]`, `[DabRestOnly]`, `[DabGraphqlOnly]`, `[DabHidden]`, `[OnDelete]` attributes for code-first entity authoring.

### Changed
- Built-in `ef-data` module provider rewritten to use the entity catalog. The provider now emits a real `DbContext` (DbSet per entity) and — when any entity carries `[DabExpose]` — a sibling `dab-config.json`.
- `<FrameworkReference Include="Microsoft.AspNetCore.App" />` added to the AspireForm tool package; `<RollForward>LatestMajor</RollForward>` set for shared-framework compatibility.

### Breaking
- `ef-data` block inputs changed: removed `database` + `contextName`; added `projectPath` (required), `dbContext` (optional), `emitDabConfig` (optional), `dabConfigPath` (optional). Migration:
  - Before (0.4.0):
    ```yaml
    modules:
      data:
        type: ef-data
        dependsOn: [sql]
        inputs:
          database: appdb
          contextName: AppDbContext
    ```
  - After (0.5.0):
    ```yaml
    modules:
      data:
        type: ef-data
        dependsOn: [sql]
        inputs:
          projectPath: ./Demo.Data/Demo.Data.csproj
    ```
  When upgrading, AspireForm throws a clear `InvalidOperationException` from `ef-data plan` that points back to this entry.

## [0.4.0] - 2026-05-25

### Added
- `aspireform mcp` verb: starts an MCP (Model Context Protocol) server exposing AspireForm's verbs as tools to AI agents.
  - Two transports: stdio (default) and HTTP on localhost (`--http --port N`).
  - 14 low-level tools (one per CLI verb): `aspireform_{new, add, config, plan, apply, destroy, import, state_list, state_show, doctor, plugin_list, plugin_install, plugin_update, plugin_remove}`.
  - 3 curated macros: `scaffold_aspire_app_with_data`, `add_cache_layer`, `add_authentication`.
  - In-process: tool handlers call the same internal services the CLI commands use — no shell-out.
- README "Use with an agent" section with Claude Code config snippet.

## [0.3.2] - 2026-05-24

Plugin shape #2: `.cs`-script plugins.

### Added

- Drop a `.cs` file into `.aspireform/scripts/` and AspireForm compiles + loads it via Roslyn
  into the same `AspireFormPlugins` context as NuGet plugins. No package authoring needed.
- `#:package <id>[@<version>]` directives at the top of a script declare NuGet dependencies;
  AspireForm restores them via the same `PluginRestorer` path used for NuGet plugins.
- Source-hash compile cache at `.aspireform/scripts/.cache/<sha256>/` — unchanged scripts skip
  recompile across runs.
- Script-provided block types take priority over NuGet auto-restore for the same `type`.

## [0.3.1] - 2026-05-24

Plugin loader follow-ups from the 0.3.0 review.

### Added

- **`AssemblyDependencyResolver`** wired into `PluginAssemblyLoader`. Plugins may now depend on
  transitive NuGet packages — the loader resolves them via the plugin's `.deps.json` plus a
  filename-match fallback across all loaded plugin directories. The 0.3.0 limitation noted in
  the changelog is lifted.

### Changed

- `PluginListCommand` column widths now auto-size from the data — long plugin names no longer
  misalign the table.
- `PluginManager` manifest read is now properly async.
- `PluginInstallCommand` treats an empty version after `@` (e.g. `Redis@`) as floating, same
  as omitting `@` entirely.

### Removed

- `PluginLockEntry.Source` field — was hard-coded to nuget.org regardless of the actual feed
  used. Real feed-tracking can return when there's a clear use case.

## [0.3.0] - 2026-05-24

Plugin loader — AspireForm now supports external Resource and Module providers shipped as
separate NuGet packages.

### Added

- **External plugin loader.** Plugins are NuGet packages with `<PackageType>AspireFormPlugin</PackageType>`
  containing an `aspireform-plugin.json` manifest. AspireForm shells out to `dotnet restore` to fetch
  plugin packages into the global NuGet cache, then loads their assemblies into an isolated
  `AssemblyLoadContext`. No `NuGet.Protocol` is embedded into AspireForm itself — the SDK handles
  dependency resolution.
- **Auto-restore on first use.** Declaring a block `type` not provided by a built-in provider
  triggers an automatic restore of `AspireForm.Plugin.<Name>` on the next `plan`/`apply`. Pinned
  versions are recorded in `.aspireform/plugins.lock.yaml` (committed to git).
- **`aspireform plugin list / install / update / remove`** commands for explicit lifecycle
  management (pinning, offline use, CI cache warmup).
- **First dogfooded plugin: AspireForm.Plugin.Redis 0.1.0** — Redis Resource provider with optional
  `withDataVolume` input.
- **Cross-plugin CI workflow** (`.github/workflows/ci.yml`) builds the entire solution and runs every
  test project on every push and PR to main; the release workflow now handles
  `plugin/<Name>/v<version>` tags for per-plugin publishing.

### Notes

- Plugins declare `minAspireFormVersion` in their manifest; the loader refuses incompatible plugins
  with a clear error.
- Plugin assemblies remain loaded for the AspireForm-invocation lifetime — `plugin remove` clears the
  lockfile entry but does not unload an already-loaded plugin until next run.
- `.cs`-script plugin support is a follow-up plan (Plan 2.0.5).
- **Plugin transitive dependency limitation:** plugins in 0.3.0 must depend only on AspireForm
  and the BCL. Transitive NuGet dependencies are not yet resolved by the loader — a plugin
  declaring `<PackageReference Include="ThirdParty" />` will fail at runtime when its assembly
  tries to use ThirdParty types. AssemblyDependencyResolver wiring arrives in 0.3.1; the first
  vertical needing it (likely Mailpit or Hangfire) will drive that work.

## [0.2.0] - 2026-05-24

Plan 3 of 3 — Core Engine complete. The full plan/apply reconciliation loop now ships.

### Added

- **`aspireform apply`** — executes the plan after an interactive approval gate (or
  `--yes` to skip). Persists `.aspireform/state.json` after each successful block so partial
  progress survives later failures. Refuses to proceed when drift is detected unless
  `--force-drift` is supplied.
- **`aspireform destroy [block]`** — removes one block (or every block in state when no
  argument is given). Module blocks are destroy-protected; pass `--allow-module-destroy` to
  override.
- **`aspireform new <name>`** — scaffolds a new Aspire AppHost (via `dotnet new aspire-apphost`)
  and writes a starter `aspireform.yaml`.
- **`aspireform add <type> [name]`** — appends a Resource (default) or Module (`--module`) block
  to the config. Comments and original formatting are not preserved (the config is round-tripped
  through the canonical DOM and re-serialised).
- **`aspireform import <block>`** — adopts an existing setup into AspireForm state without
  executing, recording each provider-emitted file path with its current checksum.
- **`aspireform state list`** and **`aspireform state show <block>`** — inspect the tracked state.
- `IAspireCli.RunAsync(args, workingDir)` — the executor's shell-out seam to the `aspire` CLI.
- File-snapshot end-to-end test for `apply` and a Docker-gated Aspire-Test-Framework boot test.

### Notes

- `BlockState.Inputs` now records the resolved inputs the executor saw, enabling Plan 3's
  drift / re-apply logic and future change-detection.
- State paths are stored repo-relative for git portability; the executor performs the
  absolute↔relative conversion via `PathUtilities`.
- The `ef-data` Module remains intentionally minimal (DbContext scaffold + a managed marker
  region in `AppHost.cs`). Full DI / migration wiring is a richer-reference concern.

## [0.1.0] - 2026-05-23

Initial release. Foundation of the AspireForm Core Engine (Plan 1 of 3).

### Added

- `dnx`-runnable .NET 10 tool packaged as `DotnetTool` (`dnx AspireForm <verb>`).
- `aspireform config [--project-dir DIR] [--env ENV]` — prints the fully
  merged and interpolated desired-state configuration as indented JSON.
- `aspireform doctor` — checks the .NET 10 SDK and the `aspire` CLI
  prerequisites, with remediation guidance on failure.
- Format-agnostic configuration pipeline: YAML and JSONC both normalize to
  a `System.Text.Json.Nodes.JsonObject` DOM; parity is enforced by test.
- Docker-Compose-style override file layering (`aspireform.<env>.yaml` is
  deep-merged over the base; mappings recurse, sequences replace, explicit
  null in the override removes the key).
- `${VAR}` and `${VAR:-default}` interpolation, sourced from `.env` plus
  process environment variables (process environment wins on collision).
- Schema-validating model binder producing the canonical `ProjectModel`,
  with `Resources` and `Modules` blocks and a plugin-ready `Inputs` bag
  per block. Friendly error messages on type mismatches and bad `dependsOn`.
- State store on `.aspireform/state.json` (consumed by the planner in
  Plan 2), including per-block `Inputs` for drift detection.
- Minimal `IAspireCli` seam (extended by Plan 3).
- `examples/sample/` fixture and end-to-end CLI smoke tests.

### Known limitations / not in 0.1

The Plan 1 scope ships only `config` and `doctor`. The reconciliation
verbs (`new`, `add`, `plan`, `apply`, `destroy`, `import`, `state`) arrive
in Plans 2 and 3. See `docs/superpowers/specs/` for the full design and
`docs/superpowers/plans/` for the staged implementation plans.
