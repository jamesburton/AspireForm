# AspireForm — Core Engine (Sub-project #1) — Design Spec

- **Date:** 2026-05-22
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #1 of 5 — the AspireForm Core Engine, proven end-to-end with a SQL Server Resource and a thin `ef-data` Module.

---

## 1. Context & vision

AspireForm is a tool for **constructing and configuring [.NET Aspire](https://aspire.dev) applications declaratively** — applying Infrastructure-as-Code ideas (from Terraform) and declarative-orchestration ideas (from Docker Compose) to the problem of scaffolding and evolving an Aspire solution. A human, an AI agent, or a UI edits a declarative config; AspireForm reconciles that desired state against what is actually on disk and applies the difference.

The full product is a **product line**, decomposed into five independently-buildable sub-projects:

| # | Sub-project | Covers |
|---|---|---|
| **1** | **Core engine** *(this spec)* | The `dnx`-runnable CLI, zero-install packaging + self-update, the config + state model, `plan`/`apply` reconciliation, the Resource/Module contract — proven with a SQL Server Resource + an `ef-data` Module. |
| 2 | Vertical catalog | Remaining verticals as plug-in slices: Redis, MailDev, Hangfire, Data API Builder, auth (ApiKey/MagicLink/Entra), reporting, ETL. Plus the external plugin loader (`.cs`-script + NuGet-package verticals). |
| 3 | Agent surface | An MCP server exposing AspireForm's functions so an agent can chat-construct a project. |
| 4 | Builder UIs | API-definition UI/MCP and EF context/model UI/MCP. |
| 5 | Stretch | Figma → UI generation, Blazing Story, theme editor, drag-and-drop designer. |

**This spec covers sub-project #1 only.** Sub-projects #2–#5 are recorded here as roadmap context (§13) but are not specified.

Research notes informing this design live in `docs/research/`:
`terraform-architecture.md`, `aspire-current-state.md`, `dnx-and-file-based-apps.md`, `verticals-and-integrations.md`, `docker-compose-inspiration.md`.

---

## 2. Locked design decisions

These were settled during brainstorming and are the fixed premises of this spec:

1. **Scope** — Core engine, proven with a SQL Server Resource **and** a thin `ef-data` Module.
2. **Relationship to the `aspire` CLI** — AspireForm **delegates fully** to the official `aspire` CLI for work they overlap on (creating projects, adding integrations). The `aspire` CLI is a **hard requirement**; it is runnable via `dnx` if not installed, and `aspireform doctor` offers to assist installation.
3. **Config format** — A **format-agnostic loader** supporting **YAML and JSONC** (JSON-with-comments) in v1, both deserialised into one canonical model. Pulumi-style C# config and an HCL-style DSL are explicitly deferred.
4. **Block model** — Two distinct kinds: **Resources** (infra — managed, destroyable) and **Modules** (feature slices — scaffold cross-layer code, destroy-protected by default).
5. **Re-apply / drift** — **Per-file ownership modes**: each emitted file is tagged `managed`, `scaffold`, or `merge`; the engine treats each accordingly on re-apply.
6. **Extensibility** — Resources/Modules are **built into the tool for v1**; the provider **contract** is designed as a clean, plugin-ready interface, but the external plugin loader is deferred to sub-project #2.
7. **State storage** — **Hybrid**: a `.aspireform/state.json` file is the source of truth, plus a lightweight `// aspireform:` marker in each generated file for human visibility and state recovery.
8. **Testing** — xUnit v3 on the Microsoft Testing Platform (MTP), plus the Aspire Test Framework for the generated solution.

---

## 3. Architecture

AspireForm is a **.NET 10 console app packaged as a .NET tool** (`<PackAsTool>true</PackAsTool>`, `<ToolCommandName>aspireform</ToolCommandName>`, `PackageType=DotnetTool`), published to NuGet. Users run it via `dnx AspireForm …`, which resolves the latest published version at each invocation — **self-update requires no code**; the engine only needs to *report* when a newer version exists.

### Pipeline

```
 aspireform.yaml (+ aspireform.<env>.yaml override files)   ← desired state
            │  format-agnostic loader → canonical ProjectModel
            ▼
   ┌──────────────┐   reads    ┌────────────────────────┐
   │   PLANNER    │ ◄───────── │ .aspireform/state.json │ ← last-known: emitted files,
   │ build graph, │            └────────────────────────┘    owning block, mode, checksum
   │ diff desired │ ◄───────── filesystem (actual on-disk reality)
   │ vs state vs  │
   │ disk         │ ──► human-readable PLAN (unified diffs; +/~/- per block & per file)
   └──────────────┘
            │  approval gate
            ▼
   ┌──────────────┐
   │  EXECUTOR    │  topological order: shell out to `aspire add` / `aspire new`,
   │  (apply)     │  write/merge scaffold files per ownership mode, update state + markers
   └──────────────┘
```

### Components

| Component | Responsibility |
|---|---|
| **CLI host** | Argument parsing, verb dispatch, console rendering of plans/diffs, exit codes. |
| **Config layer** | Format-agnostic YAML/JSONC loader; Compose-style override-file layering; `${VAR}` interpolation; validation against the canonical schema; produces the `ProjectModel`. |
| **Provider registry** | Holds the built-in Resource and Module providers behind the plugin-ready contract; resolves a config block's `type` to its provider. |
| **Planner** | Builds the dependency graph; performs the three-way reconcile (desired vs state vs disk); produces a `Plan` of block- and file-level actions; computes drift. Pure, side-effect-free. |
| **Executor** | Executes a `Plan` in topological order: invokes the `aspire` CLI adapter, renders/merges files per ownership mode, updates state and markers. |
| **State store** | Reads/writes `.aspireform/state.json`; reads/writes in-file markers; can rebuild state from markers (`import`/`refresh`). |
| **`aspire` CLI adapter** | Wraps shell-out to the `aspire` CLI behind an interface; version-checks; falls back to `dnx Aspire.Cli`; surfaces install guidance. |

Each component has one purpose and a defined interface, so each is unit-testable in isolation. The **Planner is pure** (no I/O) — it takes the `ProjectModel`, the loaded `State`, and a filesystem snapshot, and returns a `Plan` — which makes the engine's hardest logic fully testable without touching disk.

---

## 4. Config layer

### 4.1 Formats

`aspireform.yaml` or `aspireform.jsonc` — chosen by file extension. The loader deserialises either into the identical canonical `ProjectModel`. JSONC is parsed with comment-stripping (or a JSONC-aware reader). A round-trip parity test asserts the two formats produce identical models from equivalent input.

### 4.2 Override-file layering (Compose-inspired)

A base `aspireform.yaml` plus optional per-environment `aspireform.<env>.yaml`. `aspireform <verb> --env dev` deep-merges `aspireform.dev.yaml` over the base: **mappings deep-merge, sequences replace**. This keeps environment deltas small and diffable.

### 4.3 Interpolation

`${VAR}` and `${VAR:-default}` substitution, sourced from process environment and an optional `.env` file.

### 4.4 Canonical schema (sketch)

```yaml
aspireform:
  version: 1
  project: MyApp
  apphost: ./MyApp.AppHost          # path to the AppHost project

resources:
  sql:
    type: sqlserver
    aspireName: sql                 # builder.AddSqlServer("sql") in the AppHost
    databases: [appdb]

modules:
  data:
    type: ef-data
    dependsOn: [sql]
    database: appdb
    contextName: AppDbContext

profiles: {}                        # Compose-style optional groups — schema reserved, v1 no-op
```

`profiles` is **reserved in the schema** in v1 (parsed, validated, but with no behaviour) so the format is forward-compatible with sub-project #2 without a breaking change.

---

## 5. Provider registry & contracts

A **Resource** is infra: it is managed and safely destroyable (removing it deletes a `.csproj` line / AppHost statement). A **Module** is a feature slice: it scaffolds code across layers that a human will hand-edit, and is therefore **destroy-protected** by default.

Both are exposed through a clean, plugin-ready contract — designed now, even though v1 ships only built-in implementations:

```
IProvider (common)
  - Type            : string                 // e.g. "sqlserver", "ef-data"
  - Kind            : Resource | Module
  - DescribeInputs():  input schema for validation
  - DependsOn(block):  declared dependency block names
  - Plan(context)   :  returns the set of FileActions + aspire-CLI actions this block
                       would perform, given desired config + current state
```

Each `FileAction` carries its **ownership mode** (`managed` / `scaffold` / `merge`), its target path, and a content renderer. The provider declares intent; the Planner and Executor decide and perform. This separation is what makes external plugin providers (sub-project #2) a drop-in: a plugin only implements `IProvider`.

v1 ships exactly two built-in providers: `sqlserver` (Resource) and `ef-data` (Module).

---

## 6. Reconciliation model

The core loop reconciles three views: **Desired** (the merged `ProjectModel`), **State** (`.aspireform/state.json`), **Actual** (the filesystem).

### 6.1 Block-level actions

| Condition | Action |
|---|---|
| In config, not in state | **CREATE** |
| In config and state, config changed | **UPDATE** |
| In state, not in config | **DELETE** — Resource: executes. Module: **blocked** unless `prevent_destroy: false` is set on the block or `--allow-module-destroy` is passed. |

Blocks are ordered by the dependency graph (`dependsOn`); `apply` executes in topological order; `destroy` in reverse. Cycles are a validation error.

### 6.2 File-level actions, by ownership mode

On CREATE/UPDATE, each file the provider emits is acted on per its mode:

- **`managed`** — files the tool must keep owning (e.g. `.csproj`, `Program.cs` wiring, AppHost statements). Re-rendered every apply via **structured / AST-level edits** (Roslyn for `.cs`, XML DOM for `.csproj`) so human additions elsewhere in the file survive. If the on-disk checksum differs from the state checksum *inside the managed region*, the engine attempts an AST re-merge; if it cannot, it stops and prompts.
- **`scaffold`** — files generated **once** (e.g. the `DbContext`, entity classes, pages). If absent → create. If present → **skip, never overwrite**. Evolving them is the developer's job (or an explicit `--force` regenerate).
- **`merge`** — files where a 3-way merge is appropriate (e.g. config files). On re-apply: merge the **state baseline** (last-generated content, stored in state), the **current on-disk** content, and the **newly-rendered** content. Non-conflicting changes are kept; conflicts are surfaced, optionally handed to `meld` if available (falls back to inline conflict markers if not).

### 6.3 Drift detection

`plan` always **refreshes**: it compares every tracked file's on-disk checksum against its state checksum and reports drift, **even for blocks whose config has not changed**. Drift is surfaced in plan output; it does not by itself fail the plan, but it changes the per-file action (e.g. a drifted `managed` file shows a re-merge; a drifted `scaffold` file shows "skipped — local edits preserved"). A hand-**deleted** tracked file is detected via the state file (the marker is gone with it) and reported as "missing — will be recreated".

### 6.4 `plan` and `apply`

- `plan` — pure and side-effect-free. Renders the full reconciliation as **unified diffs** with `+` (create), `~` (modify), `-` (delete) at both block and file granularity. This is the human/agent review gate.
- `apply` — runs `plan`, presents it, waits for approval (`--yes` to skip), then the Executor performs it in topological order and writes the updated `state.json` + in-file markers atomically at the end.

---

## 7. State store

Hybrid, per decision #7:

- **`.aspireform/state.json`** — the source of truth. Records, per emitted file: owning block, provider type, ownership mode, baseline checksum, and (for `merge` files) the last-generated baseline content. Records each block's resolved inputs. **Committed to git** — it contains no secrets (only paths and checksums) and the team must share it.
- **In-file markers** — each generated file carries a header comment, e.g. `// aspireform: block=data type=ef-data mode=scaffold` (comment syntax adapted per file type; omitted for formats that cannot carry comments). Markers give humans visibility and let `aspireform import` / a future `refresh` **rebuild `state.json`** if it is lost or diverges.

---

## 8. The SQL Server + `ef-data` reference

v1 is proven end-to-end with two co-operating blocks, chosen so the engine's full machinery — not just the `aspire add` passthrough — is exercised and tested:

**`sqlserver` Resource** — delegates to `aspire add` for the hosting integration and emits a `managed` edit to the AppHost adding `builder.AddSqlServer("sql").AddDatabase("appdb")`.

**`ef-data` Module** (`dependsOn: [sql]`) — scaffolds:
- a `DbContext` class — **`scaffold`** mode (generated once, then owned by the developer);
- an initial EF Core migration — generated via `dotnet ef`;
- `Program.cs` / DI wiring registering the context, and a hosted migration runner with `WaitFor(sql)` — **`managed`** mode.

Between them these exercise: the dependency graph (Module → Resource), the `aspire` CLI adapter, code scaffolding, **all three ownership modes**, drift detection, and Module destroy-protection.

---

## 9. Command surface (v1)

| Command | Behaviour |
|---|---|
| `aspireform new <name> [-t <template>]` | Scaffold a new Aspire solution + a starter `aspireform.yaml`. Delegates to `aspire new`. v1 ships 1–2 built-in templates. |
| `aspireform add <type> [name] [--set k=v]` | Add a Resource/Module block to the config file. **Edits config only — does not apply.** Agent/UI-friendly. |
| `aspireform plan [--env <e>]` | Show the reconciliation diff. No side effects. |
| `aspireform apply [--env <e>] [--yes]` | Execute the plan after approval. |
| `aspireform destroy [<block>] [--allow-module-destroy]` | Remove blocks; reverse-topological order. |
| `aspireform config` *(alias `show`)* | Print the fully merged + interpolated desired state (Compose-style `config` verb). |
| `aspireform state list \| show` | Inspect tracked state. |
| `aspireform import <type> <name>` | Adopt an existing hand-built resource into state. |
| `aspireform doctor` | Check prerequisites (.NET 10 SDK, `aspire` CLI); offer to install `aspire`; report if a newer AspireForm version exists. |

Self-update needs no command — `dnx AspireForm` always resolves the latest NuGet version; `doctor` and `--version` simply *report* staleness.

---

## 10. `aspire` CLI adapter

All interaction with the official `aspire` CLI goes through one interface (`IAspireCli`). The real implementation shells out; it version-checks the installed `aspire` CLI, falls back to `dnx Aspire.Cli` when `aspire` is not on `PATH`, and surfaces install guidance via `doctor`. The interface allows a **fake** for unit tests, with a single real **smoke test** in CI exercising the genuine `aspire` CLI.

---

## 11. Testing strategy

- **Framework** — xUnit v3 on the Microsoft Testing Platform (MTP).
- **Unit** — config loader (YAML/JSONC parity, override-merge rules, interpolation); Planner diff logic (three-way reconcile, block actions, ownership-mode resolution, drift, dependency ordering, cycle detection); state store round-trip and marker-based rebuild.
- **Snapshot / golden** — `plan` output formatting; scaffolded file content for the `sqlserver` + `ef-data` reference.
- **Integration** — the `aspire` CLI adapter via the fake, plus one real smoke test.
- **End-to-end** — using the **Aspire Test Framework** (`DistributedApplicationTestingBuilder`): run `apply` against a temp directory, then boot the *generated* AppHost and assert the SQL resource reaches a healthy state and migrations ran.

---

## 12. Scope boundaries — explicitly NOT in v1

- No MCP server (sub-project #3).
- No external plugin loader / `.cs`-script or NuGet-package verticals — the **contract** is designed, the **loader** is not built (#2).
- No verticals beyond the `sqlserver` Resource + `ef-data` Module — Redis, MailDev, Hangfire, Data API Builder, auth, reporting, ETL all deferred (#2).
- No builder UIs, Figma integration, Blazing Story, theme editor, drag-and-drop designer (#4/#5).
- No C#-style or HCL-style config formats (deferred; YAML + JSONC only).
- No `aspireform publish` / `deploy` — use the `aspire` CLI directly.
- `profiles` is parsed and validated but has **no behaviour** in v1 (forward-compat reservation).

---

## 13. Roadmap context (not specified here)

Sub-projects #2–#5 each get their own spec → plan → build cycle. Recorded here only so the core engine's contracts are designed with them in mind:

- **#2 Vertical catalog** — the external plugin loader plus Redis, MailDev (prefer Mailpit — it has a Community Toolkit integration), Hangfire, Data API Builder, auth (ApiKey / MagicLink / Entra External ID), reporting, ETL.
- **#3 Agent surface** — an MCP server exposing AspireForm's verbs so an agent can chat-construct a project.
- **#4 Builder UIs** — API-definition UI/MCP and EF context/model UI/MCP.
- **#5 Stretch** — Figma → UI generation, Blazing Story demo pages, theme editor, drag-and-drop designer.

---

## 14. Risks & open questions

1. **AST-merge robustness for `managed` files** — Roslyn-based re-editing of `Program.cs` is the most technically demanding part. Mitigation: keep managed regions small and well-delimited; fall back to a prompt when an AST merge is ambiguous.
2. **`aspire` CLI surface drift** — AspireForm depends on `aspire add` / `aspire new` behaviour; the `aspire` CLI evolves on its own cadence. Mitigation: the adapter version-checks and isolates all coupling behind `IAspireCli`.
3. **`dotnet ef` availability** — the `ef-data` Module needs the EF Core tools. Mitigation: `doctor` checks for them; the Module can run `dotnet ef` via a local tool manifest.
4. **State/marker divergence** — if a developer edits `state.json` by hand or markers and state disagree. Mitigation: markers are the recovery path; define a clear precedence (state file wins; `import` rebuilds from markers).

---

## 15. Definition of done (v1)

`dnx AspireForm new MyApp` produces a runnable Aspire solution; `aspireform add sqlserver` and `aspireform add ef-data` edit the config; `aspireform plan` shows an accurate unified-diff; `aspireform apply` scaffolds the SQL resource and the EF data module; re-running `apply` after a hand-edit respects every ownership mode; `aspireform destroy` removes the Resource and refuses the Module without `--allow-module-destroy`; and the Aspire-Test-Framework end-to-end test boots the generated AppHost with a healthy SQL resource and applied migrations — all on xUnit v3 / MTP, green.
