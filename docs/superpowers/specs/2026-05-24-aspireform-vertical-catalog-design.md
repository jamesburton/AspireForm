# AspireForm — Vertical Catalog (Sub-project #2) — Design Spec

- **Date:** 2026-05-24
- **Status:** Approved (design); pending implementation plans
- **Scope:** Sub-project #2 of 5 — the Vertical Catalog: external plugin loader plus ten built-as-plugin verticals (Redis, Mailpit, Hangfire, DAB, Reporting, Auth × 4, ETL).
- **Predecessor:** Sub-project #1 (Core Engine) shipped as **AspireForm 0.2.0** on NuGet. The `IProvider` contract introduced there is the foundation every plugin builds on.

---

## 1. Context

The Core Engine completed the `plan` / `apply` reconciliation loop and shipped two built-in providers (`sqlserver`, `ef-data`). The `IProvider` contract was designed "plugin-ready" from day one — what's missing is the **loader** that lets a NuGet package or `.cs` script contribute new providers without recompiling AspireForm itself.

Sub-project #2 ships that loader **and then dogfoods it** by re-implementing every catalog vertical as a separate plugin package. The main `AspireForm` package stays slim; users install verticals on demand (auto-restore by default).

Roadmap reference: `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` §13.

Research notes: `docs/research/verticals-and-integrations.md` (verticals technology survey), `docs/research/dnx-and-file-based-apps.md` (.NET 10 `.cs` script feature).

---

## 2. Locked design decisions

These were settled during brainstorming and are the fixed premises of this spec.

1. **Decomposition** — ten plans, every vertical its own plan + the loader. Finest grain; each plan ships as a tagged NuGet release with its own version.
2. **Plugin shapes** — Plan 2.0 ships **both** NuGet-package plugins (production) and `.cs`-script plugins (quick local extension, .NET 10 file-based apps).
3. **Vertical packaging** — every new vertical ships as a **separate NuGet plugin package**, dogfooding the loader. The main `AspireForm` package only adds the loader, not new providers.
4. **Plugin discovery UX** — **auto-restore by default**: declaring `type: <name>` in `aspireform.yaml` triggers a NuGet restore on the next `plan`/`apply` (resolved via plugin-naming convention). **Opt-in `aspireform install-plugin <name>`** for explicit pinning, offline use, or CI cache warmup. `.aspireform/plugins.lock.yaml` records exact-version pins.
5. **Auth model** — **shared base + three thin plugins**: `AspireForm.Plugin.Auth.Common` provides scaffolder helpers + AppHost-wiring conventions; `AspireForm.Plugin.Auth.{ApiKey,MagicLink,Entra}` depend on it. Four plans for the auth area.
6. **Reporting scope** — DAB-curated GraphQL reports: scaffolds dab-config entries exposing selected database views as read-only REST/GraphQL endpoints. Depends on the DAB plugin.
7. **ETL scope** — CSV/Excel import endpoint + a directory watcher (Hangfire-driven) that calls it on file additions. Designed to extend later toward full ETL stages.
8. **Repo layout** — mono-repo: plugins live in `src/Plugins/AspireForm.Plugin.<Name>/` alongside the main `src/AspireForm/`.
9. **Plugin contract versioning** — plugins declare a minimum AspireForm version in their nuspec; the loader refuses incompatible plugins with a clear error.
10. **Sandbox / signing** — full trust, no signing in this sub-project. Capability restrictions and publisher verification deferred to a post-v1 hardening pass.

---

## 3. Plan 2.0 — Plugin loader (the foundational plan)

The first plan in this sub-project adds the loader and proves it end-to-end with one trivial vertical (Redis). Subsequent plans (2.1–2.9) are repeated applications of the same pattern.

### 3.1 Plugin package convention

A NuGet package is recognized as an AspireForm plugin when **any** of these is true (loader tries them in order):

1. The nuspec declares `<packageType name="AspireFormPlugin" />`.
2. The package id matches `AspireForm.Plugin.<Name>` (first-party convention).
3. The package id matches `<Vendor>.AspireForm.Plugin.<Name>` (third-party convention).
4. The package id is explicitly declared in `aspireform.plugins.yaml` (user override / private feeds).

A custom MSBuild property `<PackAsAspireFormPlugin>true</PackAsAspireFormPlugin>` in the plugin csproj sets the nuspec `packageType` automatically.

### 3.2 Plugin manifest

Every plugin package contains an `aspireform-plugin.json` at its root, declaring:

```json
{
  "name": "Redis",
  "version": "0.1.0",
  "minAspireFormVersion": "0.2.0",
  "providers": [
    { "type": "redis", "kind": "resource", "className": "AspireForm.Plugin.Redis.RedisResourceProvider" }
  ]
}
```

The loader reads this BEFORE loading any assembly, so an incompatible plugin can be rejected without executing code.

### 3.3 NuGet plugin restore

- On `plan` / `apply`, the loader inspects `aspireform.yaml`'s `resources` and `modules` for unknown `type` values.
- For each unknown type, the loader resolves a candidate package id via the conventions in §3.1.
- The NuGet client API restores into `.aspireform/plugins/<id>/<version>/`.
- The resolved (name, version) pair is recorded in `.aspireform/plugins.lock.yaml`.
- Subsequent runs use the locked version; `aspireform plugin update <name>` bumps it.

### 3.4 `.cs`-script plugin restore

- Any `.cs` file in `.aspireform/scripts/` is compiled at startup via Roslyn.
- Files may use .NET 10 file-based-app `#:package` directives for NuGet deps.
- Compiled into the same `AssemblyLoadContext` used for NuGet plugins.

### 3.5 Loader architecture

- A single `AssemblyLoadContext` per `AspireForm` invocation, named `AspireFormPlugins`.
- The loader runs **after** `ConfigLoader` (so it knows what types are referenced) but **before** the planner (so the planner sees all providers).
- `ProviderRegistry.Default()` is enriched at startup with discovered plugin providers.
- Plugin load failures are non-fatal *unless* a config block references the failing plugin's type — in which case the failure is reported with the plugin's name + version + load error.

### 3.6 New CLI verbs (Plan 2.0)

- `aspireform plugin list` — show installed plugins + versions.
- `aspireform plugin install <name>[@version]` — explicit install (opt-in).
- `aspireform plugin update <name>` — bump locked version.
- `aspireform plugin remove <name>` — uninstall.

### 3.7 First plugin: `AspireForm.Plugin.Redis`

The simplest possible vertical, used to prove the loader end-to-end.

- **Type:** `redis` (Resource).
- **CLI action:** `aspire add redis`.
- **File action:** managed marker region in `AppHost.cs` containing `var redis = builder.AddRedis("<aspireName>");`.
- **Inputs:** `aspireName` (string, defaults to block name), `withDataVolume` (bool, defaults to false).

### 3.8 Plan 2.0 — Definition of done

- `dnx AspireForm@0.3.0 plan` resolves an unknown `type: redis` by auto-restoring `AspireForm.Plugin.Redis`, then renders a correct plan.
- `aspireform plugin install Redis` / `list` / `update` / `remove` all work.
- A `.cs`-script plugin dropped into `.aspireform/scripts/` is discovered and its providers registered.
- Plugin contract versioning rejects incompatible plugins cleanly.
- Tests: loader unit + integration tests; Redis plugin unit tests; smoke test running the real tool against a fixture that uses Redis via auto-restore.

---

## 4. The 10 plans (dependency-ordered)

| # | Plan | Plugin package(s) shipped | Direct deps |
|---|---|---|---|
| 2.0 | Plugin loader + first vertical | `AspireForm.Plugin.Redis` | core engine 0.2.0 |
| 2.1 | Mailpit | `AspireForm.Plugin.Mailpit` | 2.0 |
| 2.2 | Hangfire | `AspireForm.Plugin.Hangfire` | 2.0 (SQL or Redis as storage) |
| 2.3 | Data API Builder (DAB) | `AspireForm.Plugin.DAB` | 2.0 |
| 2.4 | Reporting (DAB-curated views) | `AspireForm.Plugin.Reporting` | 2.3 |
| 2.5 | Auth common substrate | `AspireForm.Plugin.Auth.Common` | 2.0 |
| 2.6 | API-key auth | `AspireForm.Plugin.Auth.ApiKey` | 2.5 |
| 2.7 | Magic-link auth | `AspireForm.Plugin.Auth.MagicLink` | 2.5 + 2.1 (SMTP) |
| 2.8 | Entra External ID auth | `AspireForm.Plugin.Auth.Entra` | 2.5 |
| 2.9 | ETL (CSV/Excel + watcher) | `AspireForm.Plugin.ETL` | 2.2 (Hangfire watcher) |

### 4.1 Vertical scope summaries

Each plan implements `IProvider` for its block type(s), authoring content templates + CLI actions in the same pattern `SqlServerResourceProvider` / `EfDataModuleProvider` already use today.

| # | Vertical | Block type(s) | Kind | Key inputs |
|---|---|---|---|---|
| 2.0 | Redis | `redis` | Resource | `aspireName`, `withDataVolume` |
| 2.1 | Mailpit | `mailpit` | Resource | `aspireName`, `withDataVolume` |
| 2.2 | Hangfire | `hangfire` | Module | `storage` (sql\|redis), `dependsOn`, `dashboardPath`, `dashboardProject` |
| 2.3 | DAB | `dab` | Resource | `aspireName`, `dependsOn` (database resources), `configFiles[]` |
| 2.4 | Reporting | `reporting` | Module | `dependsOn: [<dab>]`, `views[]` (each: name, source, permissions) |
| 2.5 | Auth.Common | *(no providers — shared library only)* | — | — |
| 2.6 | Auth.ApiKey | `auth-apikey` | Module | `targetProject`, `headerName`, `keysSource` (config\|db) |
| 2.7 | Auth.MagicLink | `auth-magiclink` | Module | `targetProject`, `dependsOn: [<mailpit>, <sql>]`, `tokenLifetime`, `fromAddress` |
| 2.8 | Auth.Entra | `auth-entra` | Module | `targetProject`, `tenantId`, `clientId`, `audience` |
| 2.9 | ETL | `etl` | Module | `targetProject`, `dependsOn: [<sql>, <hangfire>]`, `watchDirectory`, `parsers[]` |

---

## 5. Per-plan template (every plan follows this skeleton)

Every plan in this sub-project produces a single plugin package and follows the same TDD pattern:

1. **Scaffold the plugin csproj.** `src/Plugins/AspireForm.Plugin.<Name>/AspireForm.Plugin.<Name>.csproj` — `net10.0` class library; `<PackAsAspireFormPlugin>true</PackAsAspireFormPlugin>`; `PackageId=AspireForm.Plugin.<Name>`; `<Version>` initially `0.1.0`. Reference the core `AspireForm` package as a contract dependency.
2. **Author the manifest.** `aspireform-plugin.json` embedded as a content file (PackageType registers it).
3. **Implement `IProvider`** for the block type(s), with content renderers + CLI actions per the Core Engine conventions. XML doc comments throughout.
4. **Unit tests.** `tests/Plugins/AspireForm.Plugin.<Name>.Tests/` — xUnit v3 / MTP / AwesomeAssertions. Test the provider's `Plan(context)` outputs deterministically.
5. **Plugin-loaded integration test.** Add a test in the main `AspireForm.Tests` test project that loads the plugin via the real loader against a temp project directory and asserts the planner sees it.
6. **Docs.** Plugin-local `README.md` (NuGet `PackageReadmeFile`) and `CHANGELOG.md`. Main repo `CHANGELOG.md` gets a "new plugin available" entry.
7. **Release.** Tag `plugin/<Name>/v<version>` triggers a per-plugin release workflow job that packs + publishes only that plugin's nupkg + creates a GitHub release.

---

## 6. Architecture

```
┌────────────────────────────────────────────────────────────────┐
│ AspireForm (main package — slim)                               │
│   Configuration · Planning · Execution · Cli                   │
│   Providers (built-in: sqlserver, ef-data)                     │
│   + NEW: Plugins/                                              │
│       ├─ PluginLoader (NuGet restore + Roslyn for .cs)         │
│       ├─ PluginRegistry (extends ProviderRegistry)             │
│       ├─ PluginManifest (json schema + loader)                 │
│       └─ Cli/PluginCommand (list / install / update / remove)  │
└──────────────────────┬─────────────────────────────────────────┘
                       │ IProvider contract
        ┌──────────────┼───────────────┬──────────────┬────────┐
        ▼              ▼               ▼              ▼        ▼
┌──────────────┐  ┌──────────┐  ┌────────────┐  ┌─────────┐  ┌─────┐
│ Plugin.Redis │  │ .Mailpit │  │ .Hangfire  │  │ .DAB    │  │ ... │
│ (Resource)   │  │(Resource)│  │ (Module)   │  │(Resource│  │     │
└──────────────┘  └──────────┘  └────────────┘  └─────────┘  └─────┘
                                                       │
                                                       ▼ depends on
                                              ┌─────────────────┐
                                              │ Plugin.Reporting│
                                              │ (Module)        │
                                              └─────────────────┘
```

---

## 7. Cross-cutting concerns

### 7.1 Repo + release workflow

- **Mono-repo:** every plugin under `src/Plugins/`; tests under `tests/Plugins/`.
- **Release workflow** (`.github/workflows/release.yml`) extended:
  - Tag `v<version>` (existing) → release the main AspireForm package.
  - Tag `plugin/<Name>/v<version>` (new) → release only that plugin's nupkg.
  - The workflow extracts the plugin name from the tag, packs only that csproj, publishes to NuGet, creates a GitHub release scoped to the plugin.
- **Cross-plugin CI** — a separate workflow (`.github/workflows/ci.yml`, new) runs on every push/PR to main, builds the entire solution and runs all tests; catches breakage where a core-engine change breaks a plugin.

### 7.2 Plugin contract versioning

- Plugins declare `minAspireFormVersion` in the manifest.
- The loader compares against the running AspireForm assembly version (System.Reflection at startup).
- An incompatible plugin produces a clear error: *"Plugin 'Redis' v0.1.0 requires AspireForm ≥ 0.3.0; running 0.2.0. Update AspireForm or pin the plugin to an older version."*
- A separate concern from `PluginContractVersion` (a SemVer-on-the-IProvider-shape itself); for v1 the implicit assumption is `IProvider` is stable through all of sub-project #2.

### 7.3 Testing strategy per plugin

- **Provider unit tests** — pure, in-memory: assert `Plan(context)` returns the expected `ProviderPlan` shape (file actions, CLI actions, content).
- **Plugin-load integration test** — in the main `AspireForm.Tests` project, load the real plugin via the real loader, run `Planner` against a fixture, assert the plugin's blocks render.
- **No Aspire-Test-Framework boot per plugin** — the core engine's existing Docker-gated boot test continues to cover the broader "Aspire actually runs" case; individual plugins don't need their own boot test unless they have non-trivial runtime behaviour beyond the producer→provider→planner contract.

### 7.4 `.cs`-script plugin author UX

`.aspireform/scripts/my-vertical.cs`:

```csharp
#:package AspireForm@0.3.0
#:package SomeHelperLibrary@1.0.0

using AspireForm.Providers;

public sealed class MyVerticalProvider : IProvider
{
    public string Type => "my-vertical";
    public BlockKind Kind => BlockKind.Module;
    public ProviderPlan Plan(PlanContext context) =>
        new() { /* ... */ };
}
```

The loader's Roslyn compile reads `#:package` directives, restores them into the same `.aspireform/plugins/` cache, then compiles. Discovered types implementing `IProvider` are registered.

### 7.5 `.aspireform/plugins.lock.yaml` example

```yaml
schemaVersion: 1
plugins:
  - name: Redis
    package: AspireForm.Plugin.Redis
    version: 0.1.0
    source: https://api.nuget.org/v3/index.json
  - name: Mailpit
    package: AspireForm.Plugin.Mailpit
    version: 0.1.2
    source: https://api.nuget.org/v3/index.json
scripts:
  - path: .aspireform/scripts/my-vertical.cs
    compiledChecksum: "sha256:..."
```

Lockfile is committed to git (state file is too, per the Core Engine spec).

---

## 8. Scope boundaries — explicitly NOT in sub-project #2

- MCP server (sub-project #3).
- Builder UIs — API/EF designers (sub-project #4).
- Stretch — Figma, Blazing Story, theme editor, drag-and-drop designer (sub-project #5).
- Plugin sandboxing / capability restrictions — plugins run with full trust in v1.
- Plugin signing / publisher verification.
- Plugins beyond the ten listed in §4 — the catalog will grow over time; this sub-project covers the initial set.
- Migration tooling for the `add` command's lossy YAML round-trip — same v1 limitation from the Core Engine remains.

---

## 9. Risks & open questions

1. **NuGet restore in-process** — using the NuGet client API in-process inside a tool that may itself be `dnx`-launched introduces dependency-resolution edge cases. Mitigation: limit to direct package downloads (no transitive resolution) for plugin packages; let plugins declare their own transitive deps and require self-containment.
2. **AssemblyLoadContext leakage** — plugin assemblies remain loaded for the process lifetime. Plugin "uninstall" via `plugin remove` clears the disk cache but cannot unload an already-loaded assembly. Documented limitation; restart AspireForm after `remove`.
3. **`.cs`-script Roslyn compile cost** — first run pays compile time. Cache the compiled assembly + source-hash in `.aspireform/scripts/.cache/` to skip recompile on unchanged scripts.
4. **Plugin contract evolution** — if `IProvider` changes between AspireForm versions, the `minAspireFormVersion` check helps but doesn't prevent runtime ABI breaks. Mitigation: treat the `Providers` namespace as a stability boundary; document breaking changes prominently; consider a `PluginContractVersion` attribute later.
5. **Auth.MagicLink's cross-plugin dependency** — depends on both Mailpit (SMTP) and a database (SQL). Plugin dependency resolution must handle dependsOn across plugin boundaries.
6. **DAB-as-Resource vs DAB-as-Module** — DAB ships as a container (Resource semantics) but the user's config (`dab-config.json`) is generated code (Module semantics). The DAB plugin treats it as a hybrid: Resource that emits a `scaffold` dab-config.json. Documented in the plugin's README.

---

## 10. Definition of done (sub-project #2)

- AspireForm has a working plugin loader supporting both NuGet packages and `.cs` scripts.
- All ten plugins published to NuGet, each with its own release tag, README, and CHANGELOG.
- Auto-restore works end-to-end: a brand-new clone of an `aspireform.yaml` referencing any of the ten plugins runs `plan` and `apply` without manual plugin installation.
- Per-plugin tests + cross-plugin CI green.
- Documentation updated: main README's Commands table includes `plugin list/install/update/remove`; main README's "Available plugins" section lists the ten with their NuGet links.
- Each subsequent release of the main AspireForm package validates that the existing plugin set still loads under it (the cross-plugin CI catches breakage).
