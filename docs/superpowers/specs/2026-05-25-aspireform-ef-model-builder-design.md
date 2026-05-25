# AspireForm — EF Model Builder (Sub-project #4a) — Design Spec

- **Date:** 2026-05-25
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #4a of 5 — the code-first EF entity builder with UI + MCP surfaces. Pairs with #4b (API-definition builder) which is brainstormed and built separately.
- **Predecessors:**
  - Sub-project #1 (Core Engine) — **AspireForm 0.2.0**
  - Sub-project #2 (Vertical Catalog + 9 plugins) — **AspireForm 0.3.x**
  - Sub-project #3 (MCP server) — **AspireForm 0.4.0**

---

## 1. Context

Per the Core-Engine roadmap (§13): *"#4 Builder UIs — API-definition UI/MCP and EF context/model UI/MCP."* The two halves are independent and split into separate sub-projects. This spec covers **#4a — the EF model builder**.

Today, the built-in `ef-data` Module provider (`src/AspireForm/Providers/EfDataModuleProvider.cs`, part of the AspireForm 0.4.0 core engine package) is a minimal v1 that scaffolds a starter `DbContext.cs` plus a managed AppHost-region comment recording the database dependency. Users hand-write their entity classes from scratch and maintain `dab-config.json` separately when they also use the standalone DAB plugin — duplicating entity definitions across the two surfaces.

This sub-project adds a code-first authoring surface — both a visual UI (`aspireform ui`) and an MCP tool surface — that operates on the user's actual C# entity classes via Roslyn, and expands the built-in `ef-data` provider so it emits both EF Core code and `dab-config.json` from a single attribute-decorated source.

---

## 2. Locked design decisions

1. **Source of truth — C# DbContext + entity classes with attributes.** Code-first. No new YAML/JSON entity catalog file. Users write `DbContext`-derived classes and entity classes (or have them already); AspireForm reads and edits these files via Roslyn.
2. **Provider architecture — built-in `ef-data` drives DAB; standalone `AspireForm.Plugin.DAB` stays for DAB-without-EF.** The expanded built-in `ef-data` provider emits `dab-config.json` from `[DabExpose]` / `[DabPath]` / `[DabPermission]` etc. attributes on entity classes. The standalone DAB plugin remains for users pointing DAB at views or other non-EF sources.
3. **Delivery — `aspireform ui` verb, Kestrel + Blazor Server on localhost.** Same pattern as `aspireform mcp --http`: a dnx-launched local web server, no auth in v1 (dev-tool only).
4. **Builder scope — forms + full CRUD on entities, properties, attributes via Roslyn mutation.** Single-page master/detail; no visual canvas (that's #5).
5. **MCP shape — fine-grained verbs mirroring UI actions.** ~12 new tools: `aspireform_entity_*`, `aspireform_property_*`, `aspireform_relationship_*`, `aspireform_attribute_*`.
6. **Discovery — Roslyn `MSBuildWorkspace` analysis (semantic + syntactic).** Tolerates partial builds; surfaces diagnostics; doesn't require a successful build.
7. **Packaging — `AspireForm 0.5.0` (single core release).** Adds `<FrameworkReference Microsoft.AspNetCore.App />` (zero-cost on .NET 10 shared framework). Plus one new sibling package `AspireForm.Annotations 0.1.0` (attribute-only library that user projects reference). No plugin churn — the `ef-data` provider stays built-in.

---

## 3. Architecture

```
src/AspireForm/                                    AspireForm 0.5.0
├── EntityCatalog/                                 NEW — code-first entity domain model
│   ├── EntityModel.cs                             Entity, Property, Relationship, AttributeInstance records
│   ├── EntityChangeRequest.cs                     Sealed-record request DSL for mutations
│   ├── RoslynEntityScanner.cs                     MSBuildWorkspace + semantic-model entity discovery
│   ├── RoslynEntityMutator.cs                     Roslyn rewriters for the change-request DSL
│   └── EntityCatalogException.cs                  Catalog-specific errors
│
├── Mcp/Tools/Entity/                              NEW — 12 fine-grained MCP tools
│   ├── EntityListTool.cs
│   ├── EntityShowTool.cs
│   ├── EntityCreateTool.cs
│   ├── EntityDeleteTool.cs
│   ├── PropertyAddTool.cs
│   ├── PropertyRemoveTool.cs
│   ├── PropertyRenameTool.cs
│   ├── AttributeSetTool.cs
│   ├── AttributeClearTool.cs
│   ├── RelationshipAddTool.cs
│   ├── RelationshipRemoveTool.cs
│   └── DbContextListTool.cs
│
├── Cli/UiCommand.cs                               NEW — `aspireform ui` Spectre verb
│
├── Ui/                                            NEW — Blazor Server pages + Kestrel hosting
│   ├── UiHost.cs                                  Kestrel + Blazor Server bootstrap
│   ├── UiOptions.cs                               Verb settings DTO
│   ├── BrowserLauncher.cs                         Cross-platform process-start to open the browser
│   ├── Services/IEntityCatalogService.cs          DI seam over EntityCatalog (scanner + mutator)
│   ├── Services/RoslynEntityCatalogService.cs     Default impl
│   ├── App.razor / Routes.razor / _Layout.razor   Blazor app shell
│   ├── Pages/Index.razor                          Project picker (auto-redirects when single ef-data block)
│   ├── Pages/Entities.razor                       2-pane master/detail
│   ├── Pages/Diagnostics.razor                    Scanner diagnostics view
│   ├── Pages/About.razor                          Version + project info
│   ├── Components/EntityList.razor
│   ├── Components/EntityHeader.razor
│   ├── Components/EntityPropertiesTab.razor
│   ├── Components/EntityRelationshipsTab.razor
│   ├── Components/EntityAttributesTab.razor
│   ├── Components/EntityDabTab.razor
│   ├── Components/NewEntityDialog.razor
│   ├── Components/AddPropertyDialog.razor
│   └── wwwroot/site.css
│
├── Providers/EfDataModuleProvider.cs              MODIFY — rewrite to use EntityCatalog
├── Providers/EfData/DbContextEmitter.cs           NEW — emits/updates the DbContext .cs file
├── Providers/EfData/DabConfigEmitter.cs           NEW — emits dab-config.json from DAB-attributed entities
└── AspireForm.csproj                              MODIFY — <FrameworkReference Microsoft.AspNetCore.App />, version 0.4.0 → 0.5.0

src/AspireForm.Annotations/                        NEW package: AspireForm.Annotations 0.1.0
├── AspireForm.Annotations.csproj                  netstandard2.0 (referenceable from any project)
├── DabExposeAttribute.cs
├── DabPathAttribute.cs
├── DabPermissionAttribute.cs                      AllowMultiple = true
├── DabRestOnlyAttribute.cs
├── DabGraphqlOnlyAttribute.cs
├── DabHiddenAttribute.cs
└── OnDeleteAttribute.cs                           Optional EF helper (cascade behavior)
```

### 3.1 Data flow (`aspireform ui` session)

```
Browser ⇄ Blazor Server pages (in-process via SignalR)
              │
              ▼ (in-process C# calls)
       IEntityCatalogService
              │
              ├── RoslynEntityScanner ──→ user's csproj
              │     (MSBuildWorkspace + Compilation + INamedTypeSymbol walks)
              │
              └── RoslynEntityMutator ──→ entity .cs files
                    (SyntaxRewriter passes, atomic file writes)
```

### 3.2 Data flow (`aspireform apply` with the expanded ef-data provider)

```
ConfigLoader → ef-data block.inputs.projectPath
                              │
                              ▼
                  RoslynEntityScanner → EntityCatalog snapshot
                              │
              ┌───────────────┼────────────────┐
              ▼               ▼                ▼
      DbContextEmitter   (if [DabExpose])   nothing else
              │            DabConfigEmitter
              │               │
              ▼               ▼
       DbContext.cs      dab-config.json
       (managed file)    (managed file)
```

### 3.3 Same services power UI + MCP

`IEntityCatalogService` is the single seam over `RoslynEntityScanner` + `RoslynEntityMutator`. Both Blazor pages and MCP tool handlers depend on it. No duplication of business logic.

---

## 4. Entity catalog domain model

Immutable records, in `AspireForm.EntityCatalog`:

```csharp
public sealed record EntityCatalog(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<DbContextInfo> DbContexts,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);

public sealed record DbContextInfo(string Name, string Namespace, string FilePath,
    IReadOnlyList<string> DbSetEntityNames);

public sealed record Entity(
    string Name, string Namespace, string FilePath,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<AttributeInstance> Attributes);

public sealed record Property(
    string Name, string ClrType, bool IsNullable,
    bool IsPrimaryKey,
    IReadOnlyList<AttributeInstance> Attributes);

public sealed record Relationship(
    string Name, string TargetEntity, RelationshipCardinality Cardinality,
    string? ForeignKeyProperty);

public enum RelationshipCardinality { OneToOne, OneToMany, ManyToOne, ManyToMany }

public sealed record AttributeInstance(
    string FullTypeName,
    IReadOnlyList<object?> ConstructorArgs,
    IReadOnlyDictionary<string, object?> NamedArgs);

public sealed record CatalogDiagnostic(
    string Severity,    // "info", "warning", "error"
    string Message,
    string? FilePath,
    int? Line);
```

### 4.1 `RoslynEntityScanner`

Pipeline:

1. `MSBuildWorkspace.Create()` (with `MSBuildLocator.RegisterDefaults()` once per process)
2. `OpenProjectAsync(csprojPath)` — captures `WorkspaceDiagnostics`
3. `project.GetCompilationAsync()` → `Compilation`
4. Walk `Compilation.Assembly.GlobalNamespace` recursively, collecting `INamedTypeSymbol`s
5. Classify entity if: inherits from a type named `DbContext` (signals a DbContext, follow `DbSet<T>` to find entity types), OR appears as a `DbSet<T>` type argument, OR carries `[Table]` / any `AspireForm.Annotations.*` attribute
6. For each entity: enumerate properties; classify as scalar (primitive/enum/string/DateOnly/etc.) or navigation (ref to another entity OR collection of another entity)
7. Infer relationship cardinality:
   - Scalar nav with reverse-side collection → `ManyToOne` (from this side)
   - Collection nav with reverse-side scalar → `OneToMany`
   - Both sides collection → `ManyToMany`
   - Both sides scalar → `OneToOne`
8. Read attributes from `INamedTypeSymbol.GetAttributes()` and `IPropertySymbol.GetAttributes()`; map to `AttributeInstance` (preserving constructor + named args)
9. Surface `WorkspaceDiagnostics`, missing-PK warnings, dangling navigations as `CatalogDiagnostic`s — non-blocking
10. Return immutable `EntityCatalog` snapshot

Workspace is cached per UI session; the same workspace is reused across mutations (incremental reparse).

### 4.2 `RoslynEntityMutator`

Each `EntityChangeRequest` subtype maps to one Roslyn pass:

| Request | Implementation |
|---|---|
| `CreateEntity` | Create new `.cs` file with `public sealed class {Name} { public int Id { get; set; } }` skeleton in the requested namespace; add `DbSet<T>` to the DbContext (if exactly one detected) |
| `DeleteEntity` | Delete the `.cs` file (or remove only the class if multiple classes share the file); remove `DbSet<T>` from DbContext; remove all navigation properties referencing the deleted entity |
| `AddProperty` | Append property declaration to the class body; apply attributes |
| `RemoveProperty` | Remove the declaration |
| `RenameProperty` | `Renamer.RenameSymbolAsync` — semantic-safe across the whole workspace |
| `SetAttribute` | Find existing attribute of the same `FullTypeName` (replace) or insert new attribute list |
| `ClearAttribute` | Remove the attribute with matching `FullTypeName` |
| `AddRelationship` | Multi-file: emit navigation property on `FromEntity`, emit reverse navigation + FK on `ToEntity`; trivia-preserving rewriters on both files |
| `RemoveRelationship` | Symmetric to add: remove navigation + reverse nav + FK |

**Transactional commit:** rewriters produce in-memory `SyntaxTree`s; only after all rewrites succeed does the mutator write the changed files. Failure mid-stream leaves the disk untouched.

**Result:**
```csharp
public sealed record MutationResult(
    bool Success,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);
```

---

## 5. AspireForm.Annotations 0.1.0

Tiny attribute-only package, `netstandard2.0` to maximize referenceability:

```csharp
namespace AspireForm.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DabExposeAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class DabPathAttribute(string path) : Attribute { public string Path { get; } = path; }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DabPermissionAttribute(string role, string actions) : Attribute
{
    public string Role { get; } = role;
    public string Actions { get; } = actions;
}

[AttributeUsage(AttributeTargets.Class)] public sealed class DabRestOnlyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Class)] public sealed class DabGraphqlOnlyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Class)] public sealed class DabHiddenAttribute : Attribute { }

// Optional EF helper not covered by System.ComponentModel.DataAnnotations
[AttributeUsage(AttributeTargets.Property)]
public sealed class OnDeleteAttribute(string behavior) : Attribute
{
    /// <summary>One of: "Cascade", "Restrict", "SetNull", "NoAction", "ClientCascade", "ClientSetNull".</summary>
    public string Behavior { get; } = behavior;
}
```

Versioned independently from the main AspireForm package; published as `AspireForm.Annotations 0.1.0` alongside `AspireForm 0.5.0`.

---

## 6. Built-in ef-data provider (expanded)

The existing `EfDataModuleProvider` in `src/AspireForm/Providers/` is rewritten in 0.5.0. It remains built-in (not extracted to a plugin) so the Core-Engine §15 Definition of Done (`aspireform apply` scaffolds the EF data module without any plugin install step) keeps holding.

### 6.1 Block-input shape (aspireform.yaml)

```yaml
modules:
  data:
    type: ef-data
    dependsOn: [sql]
    inputs:
      projectPath: ./Demo.Data/Demo.Data.csproj    # required (new in 0.5.0)
      dbContext: Demo.Data.AppDbContext             # optional — auto-detected if one
      emitDabConfig: true                            # default: auto (true iff any [DabExpose] found)
      dabConfigPath: ./Demo.AppHost/dab-config.json # default when emitDabConfig is true
```

This input shape replaces the 0.4.0 shape (`database` + `contextName`). The 0.4.0 inputs were used for a minimal scaffold-only flow that no longer fits the EntityCatalog-driven generation.

### 6.2 Plan (`EfDataModuleProvider.Plan`)

1. Load `EntityCatalog` from `inputs.projectPath` (no workspace caching across plan invocations — fresh scan per plan)
2. Resolve target DbContext: explicit `inputs.dbContext` or auto-detect; fail if zero or multiple-without-explicit
3. `DbContextEmitter` produces the DbContext file content:
   - If file exists: locate the managed region (or insert one) inside the class; emit `DbSet<T>` properties for all entities + `OnModelCreating` fluent calls for relationships requiring config beyond conventions
   - If file does not exist: emit a full class skeleton
   - Return `PlannedFileAction(Path, Managed, block-marker, RenderContent)`
4. If `emitDabConfig == true` (explicit or auto): `DabConfigEmitter` produces `dab-config.json`:
   - Top-level: `$schema`, `data-source` (mssql with `@env('ConnectionStrings__<dependsOn[0]>')`), `runtime.rest`, `runtime.graphql`
   - For each entity with `[DabExpose]`: emit `entities[name]` block with `source` (from `[Table]` or `dbo.<Name>`), `permissions` (from `[DabPermission]` instances or default `[{role:"anonymous", actions:["read"]}]`), `rest` (path from `[DabPath]` or default `/<name>`, suppressed if `[DabGraphqlOnly]`), `graphql` (suppressed if `[DabRestOnly]`), `relationships` (from `Relationship` graph)
   - Conflict resolution: multiple `[DabPermission]` with the same role → last-wins + warning diagnostic
   - Return `PlannedFileAction(Path, Managed, block-marker, RenderContent)`
5. CLI actions: none in v1 (the `aspire add data-api-builder` integration is the standalone DAB plugin's job; this plugin emits the config only)

### 6.3 Migration (AspireForm 0.4.0 → 0.5.0 ef-data input shape)

The 0.4.0 built-in provider used `inputs.database` (default "appdb") + `inputs.contextName` (default "AppDbContext") and emitted a starter `Data/{ContextName}.cs` plus a managed AppHost comment region. The 0.5.0 provider uses `inputs.projectPath` and emits a real DbContext driven by the entity classes in that project. This is a breaking change to the `ef-data` input shape.

Migration path:
- Users who declared `ef-data` in 0.4.0 get a `ConfigValidationException` from `EfDataModuleProvider.Plan` when their old inputs are unrecognised — the message points to the AspireForm `CHANGELOG.md` [0.5.0] entry which shows the before/after diff.
- For the 0.4.0 → 0.5.0 transition the documented migration is: create an entity project (or designate an existing one), point `projectPath` at it, drop `database`/`contextName`, re-run `aspireform plan`.

---

## 7. MCP tools

12 new fine-grained tools, registered in `McpCommand.BuildRegistry` after the existing 17.

| Tool | Required inputs | Optional inputs | Returns |
|---|---|---|---|
| `aspireform_entity_list` | `projectPath` | — | Table text |
| `aspireform_entity_show` | `entity`, `projectPath` | — | Indented JSON |
| `aspireform_dbcontext_list` | `projectPath` | — | Tabular text |
| `aspireform_entity_create` | `name`, `namespace`, `filePath`, `projectPath` | — | `MutationResult` JSON |
| `aspireform_entity_delete` | `entity`, `projectPath` | — | `MutationResult` JSON |
| `aspireform_property_add` | `entity`, `name`, `clrType`, `projectPath` | `isNullable`, `isPrimaryKey` | `MutationResult` JSON |
| `aspireform_property_remove` | `entity`, `property`, `projectPath` | — | `MutationResult` JSON |
| `aspireform_property_rename` | `entity`, `oldName`, `newName`, `projectPath` | — | `MutationResult` JSON |
| `aspireform_attribute_set` | `entity`, `attributeFullName`, `projectPath` | `property`, `ctorArgs` (array), `namedArgs` (object) | `MutationResult` JSON |
| `aspireform_attribute_clear` | `entity`, `attributeFullName`, `projectPath` | `property` | `MutationResult` JSON |
| `aspireform_relationship_add` | `fromEntity`, `toEntity`, `cardinality`, `projectPath` | `foreignKeyProperty` | `MutationResult` JSON |
| `aspireform_relationship_remove` | `fromEntity`, `relationshipName`, `projectPath` | — | `MutationResult` JSON |

**Total registry:** 14 verbs + 3 macros + 12 entity tools = **29 tools**.

All tools follow the existing MCP conventions: catch `EntityCatalogException` + existing AspireForm exception set as tool-level errors (`isError: true`); never throw across the JSON-RPC boundary.

---

## 8. `aspireform ui` verb

### 8.1 CLI

```bash
aspireform ui                           # auto port (5050 if free, next-free otherwise)
aspireform ui --port 5051               # explicit port
aspireform ui --no-launch               # don't auto-open the browser
aspireform ui --project-dir ./myapp     # default AspireForm project dir
```

### 8.2 Pages

- `/` → `Index.razor` — project picker. Reads `aspireform.yaml` from `--project-dir`; finds `ef-data` blocks; if exactly one, redirects to `/entities`
- `/entities` → `Entities.razor` — 2-pane master/detail (sidebar = entity list + search + "+ New", detail = selected entity with tabs)
- `/diagnostics` → `Diagnostics.razor` — scanner diagnostics list
- `/about` → `About.razor` — version + project info

### 8.3 Component model

Each tab is a stateless component that takes the current `Entity` + an `IEntityCatalogService`. Mutations dispatch through the service; after success the page re-scans and re-renders.

### 8.4 Hosting

```csharp
internal static class UiHost
{
    public static async Task RunAsync(UiOptions opts, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton<IEntityCatalogService, RoslynEntityCatalogService>();
        builder.Services.AddSingleton(opts);
        var app = builder.Build();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        if (opts.LaunchBrowser) BrowserLauncher.Open($"http://localhost:{opts.Port}");
        await app.RunAsync(ct);
    }
}
```

Vanilla Blazor (no third-party component library in v1). Styling via `wwwroot/site.css` — simple, dev-tool appropriate. Component library polish is a #4a.1 candidate.

**Process lifetime:** Ctrl-C stops the host. No shutdown endpoint in v1 (would need CSRF mitigation we're not building).

---

## 9. Error model

- **Scanner errors** — non-blocking. `EntityCatalog.Diagnostics` carries `MSBuildWorkspace` warnings + missing-PK / dangling-nav / cycle warnings. UI shows a banner + Diagnostics page; MCP tools include diagnostics in their response.
- **Mutator errors** — transactional. All-or-nothing per `EntityChangeRequest`. `MutationResult.Success = false` + diagnostics on failure; no partial writes.
- **Plugin-time errors** — caught via the existing `PluginContractException` path: missing `projectPath`, ambiguous DbContext, nav to non-entity type.
- **Attribute conflicts** — multiple `[DabPermission]` with same role: last-wins + warning diagnostic; plan still proceeds.
- **MCP boundary** — tool-level errors via `isError: true` in `ToolResult`; transport-level errors only for truly unhandled exceptions (matches existing MCP error model).

---

## 10. Testing strategy

| Layer | Style | Tooling |
|---|---|---|
| EntityCatalog (scanner) | Fixture-based: small fixture .cs files in `tests/fixtures/EntityCatalog/Scanner/`; scan + assert | xUnit v3 / MTP / AwesomeAssertions |
| EntityCatalog (mutator) | Fixture-based: input file + change request + expected output file diff | Same |
| MCP entity tools | Per-tool unit tests against a fixture project copied to a temp dir | xUnit v3 / MTP (matches `tests/AspireForm.Tests/Mcp/Tools/`) |
| Expanded `EfDataModuleProvider` | Provider plan tests with fixture entity files; assert DbContext emit + dab-config.json emit | xUnit v3 / MTP, in `tests/AspireForm.Tests/Providers/EfData/` |
| `AspireForm.Annotations` | Trivial type/property assertions | xUnit v3 / MTP |
| Blazor pages | bUnit component tests with a fake `IEntityCatalogService` | bUnit + xUnit v3 / MTP |
| UI verb integration | Start host on ephemeral port; HttpClient asserts response status + basic content | xUnit v3 / MTP |
| Full e2e | Manual `dnx AspireForm@0.5.0 ui` walkthrough during release verification | Manual |

**Target:** ~50 new tests across catalog, plugin, MCP, bUnit pages, integration.

---

## 11. Scope boundaries — explicitly NOT in #4a

- Visual canvas / drag-drop entity layout → **#5** (Stretch)
- API-definition UI/MCP → **#4b** (separate sub-project)
- Multi-DbContext editing in one UI session — v1 picks one at a time
- Cross-project entity references — entities must live in the project specified by `projectPath`
- Migration generation (`dotnet ef migrations add`) — user runs after edits; UI shows a hint when model changes
- Auth on UI/HTTP endpoint — dev-tool, localhost-only, no auth in v1
- Third-party Blazor component library — vanilla Blazor in v1; polish pass is #4a.1
- Real-time multi-user editing — single-user assumption
- Undo/redo stack — git is the v1 undo
- Custom user-defined attributes in the UI — only `AspireForm.Annotations.*` + standard `System.ComponentModel.DataAnnotations.*` are recognized; user attributes pass through but aren't editable

---

## 12. Risks & open questions

1. **`MSBuildWorkspace` startup cost.** First open of a project can take several seconds. Mitigation: cache the workspace per UI session; show a "Loading..." banner during the first scan; lazy-initialize on the first request rather than on UI startup.
2. **Roslyn rewriter complexity for `AddRelationship`.** Multi-file with FK + reverse-nav generation has corner cases (self-referencing entities, M:N requiring join tables). Mitigation: v1 supports 1:1, 1:N, N:1 — defers M:N join-table generation to #4a.1 if it turns out to be too complex.
3. **DbContext detection ambiguity.** Projects with multiple DbContexts (or DbContexts in a referenced project) need explicit `inputs.dbContext`. Mitigation: explicit error message points users at the input; doctor command surfaces the ambiguity.
4. **`PackAsTool` + ASP.NET shared framework compatibility.** Adding `<FrameworkReference Microsoft.AspNetCore.App />` to a tool package is supported on .NET 10 but needs verification on the dnx install path. Mitigation: smoke test in CI; fallback to splitting `AspireForm.Ui` into a separate package (Approach B from brainstorming) if it doesn't work.
5. **Blazor Server WebSocket connectivity on Windows.** Kestrel + SignalR + browser auto-launch may hit Windows firewall prompts on first run. Mitigation: localhost-only binding doesn't trigger Windows Firewall for new ports under most policies; document the once-per-port prompt if it appears.

---

## 13. Definition of done (sub-project #4a)

- `aspireform ui` starts a Blazor Server host and opens the browser to the entity list
- All 12 new MCP tools registered and behaving end-to-end via stdio
- The expanded built-in `ef-data` provider emits a correct DbContext and (when applicable) `dab-config.json` from a fixture entity project
- `AspireForm.Annotations 0.1.0` packs cleanly and is referenceable from a netstandard2.0/net6+ project
- Tests: scanner + mutator fixture tests + 12 MCP tool tests + ef-data emitter tests + ≥3 bUnit component tests + UI host smoke test; xUnit v3 / MTP suite green
- `AspireForm 0.5.0` ready to ship via `v0.5.0` tag through the existing release workflow
- `AspireForm.Annotations 0.1.0` ready to ship via `annotations/v0.1.0` tag (new release-workflow job) — alternatively bundled alongside `AspireForm` so a single `v0.5.0` tag pushes both packages
- README has a "Use the entity builder" section with a Claude Code config snippet for the new MCP tools and a screenshot of the UI
