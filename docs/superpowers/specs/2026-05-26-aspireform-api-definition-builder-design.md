# AspireForm — API-Definition Builder (Sub-project #4b) — Design Spec

- **Date:** 2026-05-26
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #4b of 5 — the code-first Minimal API endpoint builder with UI + MCP surfaces. Pairs with #4a (EF model builder) which shipped as AspireForm 0.5.0.
- **Predecessors:**
  - Sub-project #4a (EF Model Builder) — **AspireForm 0.5.0** (entity catalog, Roslyn scanner/mutator, `aspireform ui`, 12 MCP entity tools)

---

## 1. Context

Per the Core-Engine roadmap (§13): *"#4 Builder UIs — API-definition UI/MCP and EF context/model UI/MCP."* The two halves are independent and built separately. This spec covers **#4b — the API-definition builder**.

Today, when users build a .NET Aspire Web API project, they hand-write their Minimal API endpoint registrations (`MapGet`, `MapPost`, etc.) ad hoc — there is no structured source of truth AspireForm can read, manage, or surface in a UI. The #4a half added code-first entity authoring for EF + DAB; this sub-project adds code-first **endpoint authoring** for Minimal API.

### Scope decomposition

Three possible interpretations of "API-definition UI/MCP" exist in the ecosystem:

| Direction | What it covers | Decision |
|---|---|---|
| **A. Custom Minimal API endpoints** | `[ApiEndpoint]`-decorated methods → discover, edit, emit `_Endpoints.g.cs` | **Selected for #4b** |
| B. DAB stored-proc/view exposure | Already covered: standalone DAB plugin scaffolds `dab-config.json`; built-in `ef-data` handles entity-level DAB from attributes | **Deferred — already handled** |
| C. GraphQL schema authoring | Overlaps with DAB GraphQL exposure in #4a; advanced enough to warrant its own sub-project | **Deferred to #4c (future)** |

**Direction A is the genuine gap.** The DAB plugin produces an empty `dab-config.json` and delegates entity exposure to `ef-data`/`DabConfigEmitter`. No existing surface covers custom handler methods, request/response types, route parameters, or auth policies for Minimal API. #4b fills exactly this gap.

---

## 2. Locked design decisions

1. **Source of truth — C# methods decorated with `[ApiEndpoint(path, method)]`.** Code-first. No new YAML endpoint catalog file. Users write their endpoint-handler methods in a Web project; AspireForm reads and edits these methods via Roslyn.

2. **Annotations package — bump `AspireForm.Annotations` to 0.2.0.** Add `[ApiEndpoint]`, `[ApiAuth]`, `[ApiTag]`, and `[ApiSummary]` attributes. Existing `[DabExpose]`/`[DabPath]`/… attributes are unchanged. The single package carries both EF/DAB attributes and API-endpoint attributes.

3. **Output — managed `_Endpoints.g.cs` per Web project.** A single `MapAspireFormEndpoints(this WebApplication app)` extension method; one `Map*` call per discovered `[ApiEndpoint]` method. Regenerated on every `aspireform apply` (ownership mode: `Managed`). Users invoke it from `Program.cs` with `app.MapAspireFormEndpoints()`.

4. **Provider — new built-in `api-endpoints` Module provider.** Independent from `ef-data`; the two have different lifecycles (entity model vs. endpoint registration). The provider emits `_Endpoints.g.cs` only; it does not touch `dab-config.json` (that remains `ef-data`'s responsibility).

5. **Discovery — Roslyn `MSBuildWorkspace` analysis (same infrastructure as EntityCatalog).** Discovers methods in the target Web project that carry `[ApiEndpoint]`. `MSBuildBootstrap.EnsureRegistered()` is already idempotent; no new MSBuild plumbing needed.

6. **Delivery — extend existing `aspireform ui` Blazor Server app.** New `/endpoints` page in the existing host, reusing `MainLayout.razor`, `UiHost.cs`, and `UiOptions`. No new CLI verb — `aspireform ui` already starts the host.

7. **MCP shape — 10 new fine-grained tools.** `aspireform_endpoint_{list,show,create,delete}`, `aspireform_endpoint_parameter_{add,remove}`, `aspireform_endpoint_auth_set`, `aspireform_endpoint_attribute_{set,clear}`, `aspireform_endpoint_emit`. Registry grows 29 → 39 tools.

8. **Packaging — `AspireForm 0.6.0` (main package).** Bump `AspireForm.Annotations` to 0.2.0 simultaneously.

---

## 3. Architecture

```
src/AspireForm/                                    AspireForm 0.6.0
├── ApiCatalog/                                    NEW — code-first endpoint domain model
│   ├── EndpointModel.cs                           EndpointCatalog, EndpointInfo, RouteParameter,
│   │                                              EndpointChangeRequest records + enums
│   ├── EndpointChangeRequest.cs                   Sealed-record request DSL for mutations
│   ├── RoslynEndpointScanner.cs                   MSBuildWorkspace + semantic-model endpoint discovery
│   ├── RoslynEndpointMutator.cs                   Roslyn rewriters for the change-request DSL
│   ├── EndpointEmitter.cs                         Renders _Endpoints.g.cs from the catalog snapshot
│   ├── IEndpointCatalogService.cs                 DI seam (scanner + mutator)
│   ├── RoslynEndpointCatalogService.cs            Default impl
│   └── EndpointCatalogException.cs               Catalog-specific errors
│
├── Mcp/Tools/Endpoint/                            NEW — 10 fine-grained MCP tools
│   ├── EndpointListTool.cs
│   ├── EndpointShowTool.cs
│   ├── EndpointCreateTool.cs
│   ├── EndpointDeleteTool.cs
│   ├── EndpointParameterAddTool.cs
│   ├── EndpointParameterRemoveTool.cs
│   ├── EndpointAuthSetTool.cs
│   ├── EndpointAttributeSetTool.cs
│   ├── EndpointAttributeClearTool.cs
│   └── EndpointEmitTool.cs
│
├── Ui/Components/Pages/Endpoints.razor            NEW — 2-pane endpoint browser
├── Ui/Components/Endpoint/                        NEW — tab components
│   ├── EndpointList.razor
│   ├── EndpointHeader.razor
│   ├── EndpointParametersTab.razor
│   ├── EndpointAuthTab.razor
│   └── EndpointAttributesTab.razor
├── Ui/Components/Dialogs/NewEndpointDialog.razor  NEW
├── Ui/Components/Layout/MainLayout.razor          MODIFY — add Endpoints nav link
│
├── Providers/ApiEndpoints/                        NEW — built-in provider
│   └── ApiEndpointsModuleProvider.cs
│
└── AspireForm.csproj                              MODIFY — version 0.5.0 → 0.6.0

src/AspireForm.Annotations/                        AspireForm.Annotations 0.2.0
├── AspireForm.Annotations.csproj                  MODIFY — version 0.1.0 → 0.2.0
├── ApiEndpointAttribute.cs                        NEW
├── ApiAuthAttribute.cs                            NEW
├── ApiTagAttribute.cs                             NEW
└── ApiSummaryAttribute.cs                         NEW
```

### 3.1 Data flow (`aspireform ui` → `/endpoints`)

```
Browser ⇄ Blazor Server pages (in-process via SignalR)
              │
              ▼
       IEndpointCatalogService
              │
              ├── RoslynEndpointScanner ──→ user's Web csproj
              │     (MSBuildWorkspace + Compilation + IMethodSymbol walks)
              │
              └── RoslynEndpointMutator ──→ handler .cs files
                    (SyntaxRewriter passes, atomic file writes)
```

### 3.2 Data flow (`aspireform apply` with the api-endpoints provider)

```
ConfigLoader → api-endpoints block.inputs.projectPath
                              │
                              ▼
              RoslynEndpointScanner → EndpointCatalog snapshot
                              │
                              ▼
                     EndpointEmitter
                              │
                              ▼
               _Endpoints.g.cs (managed file)
```

### 3.3 Same services power UI + MCP

`IEndpointCatalogService` is the single seam over `RoslynEndpointScanner` + `RoslynEndpointMutator`. Both Blazor pages and MCP tool handlers depend on it. No duplication.

---

## 4. Annotations additions (AspireForm.Annotations 0.2.0)

```csharp
namespace AspireForm.Annotations;

/// <summary>Marks a static method as a Minimal API endpoint. The method body is the handler;
/// AspireForm discovers it and emits a <c>MapAspireFormEndpoints()</c> call for it.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiEndpointAttribute : Attribute
{
    /// <summary>Initialises the endpoint with the route pattern (e.g. <c>/books/{id}</c>) and HTTP method.</summary>
    public ApiEndpointAttribute(string route, string method = "GET") { Route = route; Method = method; }

    /// <summary>Route pattern (e.g. <c>/books/{id}</c>).</summary>
    public string Route { get; }

    /// <summary>HTTP method: GET, POST, PUT, PATCH, DELETE. Default is GET.</summary>
    public string Method { get; }
}

/// <summary>Declares the authorization policy for an <see cref="ApiEndpointAttribute"/>-decorated method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiAuthAttribute : Attribute
{
    /// <summary>Initialises with a policy name. Use <c>"anonymous"</c> to allow unauthenticated access.</summary>
    public ApiAuthAttribute(string policy) { Policy = policy; }
    public string Policy { get; }
}

/// <summary>Assigns one or more OpenAPI tags to an endpoint for grouping in generated documentation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ApiTagAttribute : Attribute
{
    public ApiTagAttribute(string tag) { Tag = tag; }
    public string Tag { get; }
}

/// <summary>Provides a human-readable summary for the endpoint, emitted as an OpenAPI operation summary.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiSummaryAttribute : Attribute
{
    public ApiSummaryAttribute(string summary) { Summary = summary; }
    public string Summary { get; }
}
```

---

## 5. Endpoint catalog domain model

Immutable records, in `AspireForm.ApiCatalog`:

```csharp
/// <summary>Immutable snapshot of the API endpoint graph in a user's project.</summary>
public sealed record EndpointCatalog(
    IReadOnlyList<EndpointInfo> Endpoints,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);

/// <summary>One Minimal API endpoint discovered in the user's Web project.</summary>
public sealed record EndpointInfo(
    string HandlerTypeName,     // declaring class (or top-level file class)
    string MethodName,          // C# method name
    string Route,               // from [ApiEndpoint]
    string HttpMethod,          // GET / POST / …
    string? Summary,            // from [ApiSummary]
    string? AuthPolicy,         // from [ApiAuth]
    IReadOnlyList<string> Tags, // from [ApiTag]
    IReadOnlyList<RouteParameter> Parameters,
    IReadOnlyList<AttributeInstance> Attributes,
    string FilePath);

/// <summary>A route parameter extracted from the route pattern (e.g. <c>{id:int}</c>).</summary>
public sealed record RouteParameter(
    string Name,
    string? Constraint,  // e.g. "int", "guid", null for unconstrained
    bool IsOptional);

/// <summary>Result of an endpoint-mutation operation.</summary>
public sealed record EndpointMutationResult(
    bool Success,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);
```

`AttributeInstance` and `CatalogDiagnostic` are reused from `AspireForm.EntityCatalog` (same assembly; both namespaces live in the same project).

---

## 6. `RoslynEndpointScanner`

Pipeline:

1. `MSBuildBootstrap.EnsureRegistered()` (idempotent)
2. `MSBuildWorkspace.Create()` → `OpenProjectAsync(csprojPath)` — captures `WorkspaceDiagnostics`
3. `project.GetCompilationAsync()` → `Compilation`
4. Walk all `INamedTypeSymbol`s in the assembly; for each type, walk `IMethodSymbol` members
5. A method is an endpoint if it carries any attribute with `FullName == "AspireForm.Annotations.ApiEndpointAttribute"`
6. Extract `Route` + `Method` from the attribute constructor args
7. Extract `Summary`, `AuthPolicy`, `Tags` from sibling attributes
8. Extract `RouteParameter`s by parsing `{name}`, `{name:constraint}`, `{name?}` tokens from the route pattern
9. Map all method-level attributes to `AttributeInstance`s (preserving ctor + named args)
10. Surface `WorkspaceDiagnostics` + ambiguous-route warnings as `CatalogDiagnostic`s — non-blocking
11. Return immutable `EndpointCatalog` snapshot

Workspace is cached per service instance (same lazy-init pattern as `RoslynEntityScanner`).

---

## 7. `RoslynEndpointMutator`

| Request | Implementation |
|---|---|
| `CreateEndpoint` | Create new static class + method in a new `.cs` file (or append to a specified existing class); skeleton: `[ApiEndpoint(route, method)] public static IResult Handle(HttpContext ctx) => Results.Ok();` |
| `DeleteEndpoint` | Remove the method (and `[ApiEndpoint]` attribute); remove the class if it becomes empty |
| `AddParameter` | No-op on the C# source for route parameters (they are part of the route pattern string); for query/body parameters, append a typed parameter to the method signature |
| `RemoveParameter` | Remove parameter from the method signature |
| `SetAttribute` | Find existing attribute of the same `FullTypeName` (replace) or insert new attribute list |
| `ClearAttribute` | Remove the attribute with matching `FullTypeName` |
| `SetAuthPolicy` | Shorthand for `SetAttribute` on `ApiAuthAttribute` |

**Transactional commit:** all-or-nothing per request (same pattern as `RoslynEntityMutator`). Failure mid-stream leaves disk untouched.

**Result type:** `EndpointMutationResult` (parallel to `MutationResult` in EntityCatalog).

---

## 8. `EndpointEmitter`

Renders `_Endpoints.g.cs` from an `EndpointCatalog` snapshot.

```csharp
// <auto-generated />
// aspireform: managed block="api-endpoints"
using Microsoft.AspNetCore.Builder;

namespace {inferredNamespace};

/// <summary>Extension methods generated by AspireForm from [ApiEndpoint]-decorated methods.</summary>
internal static class AspireFormEndpointExtensions
{
    /// <summary>Registers all [ApiEndpoint]-decorated handlers discovered by AspireForm.</summary>
    public static WebApplication MapAspireFormEndpoints(this WebApplication app)
    {
        // {HandlerType}.{MethodName} — {HttpMethod} {Route}
        app.Map{HttpMethod}("{Route}", {HandlerType}.{MethodName});
            .WithName("{MethodName}")
            .WithSummary("{Summary}")
            .RequireAuthorization("{Policy}")
            .WithTags({Tags});
        // …
        return app;
    }
}
```

Details:
- **Namespace inference:** `{WebProjectRootNamespace}` from the csproj (read the `<RootNamespace>` or `<AssemblyName>` property via MSBuild; fall back to the project file name without extension)
- **Method selection:** `app.MapGet` / `app.MapPost` / `app.MapPut` / `app.MapPatch` / `app.MapDelete` / `app.Map` for unknown methods
- **Fluent chain:** `.WithSummary()` only if `[ApiSummary]` present; `.RequireAuthorization()` only if `[ApiAuth]` present and policy != `"anonymous"`; `.AllowAnonymous()` if policy == `"anonymous"`; `.WithTags()` only if `[ApiTag]` present; `.WithName()` always (MethodName)
- **File ownership:** `Managed` — regenerated on every `apply`; never merge-conflicts (user edits the handler method, not the emitted registration file)

---

## 9. Built-in `api-endpoints` provider

```yaml
modules:
  api:
    type: api-endpoints
    inputs:
      projectPath: ./Demo.Api/Demo.Api.csproj    # required
      outputPath: ./Demo.Api/Generated/_Endpoints.g.cs  # optional; default next to csproj
```

### 9.1 `ApiEndpointsModuleProvider.Plan`

1. Resolve `inputs.projectPath` → absolute path; fail if missing
2. Load `EndpointCatalog` from the Web project (fresh `RoslynEndpointScanner` per plan invocation — no caching across plan calls)
3. `EndpointEmitter.Render(catalog)` → file content
4. Return `PlannedFileAction(outputPath, Managed, block-marker, RenderContent)`
5. No CLI actions (no `aspire add` required for Minimal API)

### 9.2 Plan diagnostics

- Zero endpoints found → info-level diagnostic; still emits the file (with empty `MapAspireFormEndpoints` body) so the extension method exists to reference
- Ambiguous route (two `[ApiEndpoint]` with identical route + method) → warning; first-wins, second skipped

---

## 10. MCP tools

10 new tools, registered in `McpCommand.BuildRegistry` after the existing 29.

| Tool | Required inputs | Optional inputs | Returns |
|---|---|---|---|
| `aspireform_endpoint_list` | `projectPath` | — | Table text |
| `aspireform_endpoint_show` | `methodName`, `projectPath` | `typeName` | Indented JSON |
| `aspireform_endpoint_create` | `methodName`, `typeName`, `route`, `projectPath` | `httpMethod` (default "GET"), `filePath`, `namespace` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_delete` | `methodName`, `projectPath` | `typeName` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_parameter_add` | `methodName`, `paramName`, `clrType`, `projectPath` | `typeName` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_parameter_remove` | `methodName`, `paramName`, `projectPath` | `typeName` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_auth_set` | `methodName`, `policy`, `projectPath` | `typeName` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_attribute_set` | `methodName`, `attributeFullName`, `projectPath` | `typeName`, `ctorArgs`, `namedArgs` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_attribute_clear` | `methodName`, `attributeFullName`, `projectPath` | `typeName` | `EndpointMutationResult` JSON |
| `aspireform_endpoint_emit` | `projectPath` | `outputPath` | Emitted file content as text |

**Total registry:** 29 existing + 10 endpoint tools = **39 tools**.

All tools follow existing MCP conventions: catch `EndpointCatalogException` + existing exception set as tool-level errors (`isError: true`); never throw across the JSON-RPC boundary.

---

## 11. `aspireform ui` — Endpoints page

### 11.1 Navigation

Extend `MainLayout.razor` to add an Endpoints nav link:

```html
<a href="/entities">Entities</a>
<a href="/endpoints">Endpoints</a>
<a href="/diagnostics">Diagnostics</a>
<a href="/about">About</a>
```

### 11.2 Pages and components

- `/endpoints` → `Endpoints.razor` — 2-pane master/detail (sidebar = endpoint list + search + "+ New", detail = selected endpoint with tabs)
  - **Tabs:** Parameters, Auth, Attributes
  - **Header:** shows `{Method} {Route}` prominently, handler class/method below

### 11.3 Component model

Each tab is a stateless component taking the current `EndpointInfo` + `IEndpointCatalogService`. Mutations dispatch through the service; after success the page re-scans and re-renders. Mirrors the `EntityPropertiesTab`, `EntityAttributesTab` pattern exactly.

### 11.4 DI additions to UiHost

```csharp
builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
```

No change to the existing `IEntityCatalogService` registration.

---

## 12. Error model

Mirrors the #4a error model:

- **Scanner errors** — non-blocking. `EndpointCatalog.Diagnostics` carries workspace warnings + ambiguous-route warnings. UI shows a banner + Diagnostics page; MCP tools include diagnostics in their response.
- **Mutator errors** — transactional. All-or-nothing per request. `EndpointMutationResult.Success = false` + diagnostics on failure; no partial writes.
- **Provider-time errors** — caught via the existing `PluginContractException` path: missing `projectPath`, empty catalog. Info-level diagnostic when no endpoints found (not an error).
- **MCP boundary** — `isError: true` in `ToolResult`; transport-level errors only for truly unhandled exceptions.

---

## 13. Testing strategy

| Layer | Style | Tooling |
|---|---|---|
| Domain model | Value semantics, factory helpers, pattern-matching | xUnit v3 / MTP / AwesomeAssertions |
| `RoslynEndpointScanner` | Fixture-based: small fixture `.cs` files in temp dirs; scan + assert | xUnit v3 / MTP |
| `RoslynEndpointMutator` | Fixture-based: input file + change request + expected output diff | xUnit v3 / MTP |
| `EndpointEmitter` | Unit: in-memory catalog → assert emitted file content | xUnit v3 / MTP |
| MCP endpoint tools | Per-tool unit tests against a fixture project copied to a temp dir | xUnit v3 / MTP |
| `ApiEndpointsModuleProvider` | Provider plan tests with fixture endpoint files | xUnit v3 / MTP |
| `AspireForm.Annotations` new attributes | Trivial type/property assertions | xUnit v3 / MTP |
| Blazor Endpoints page | bUnit component tests with a fake `IEndpointCatalogService` | bUnit + xUnit v3 / MTP |

**Target:** ~45 new tests across catalog, provider, MCP, bUnit pages.

---

## 14. Scope boundaries — explicitly NOT in #4b

- DAB stored-procedure/view endpoint authoring → the standalone DAB plugin owns this
- GraphQL schema authoring → deferred (#4c, future)
- OpenAPI/Swagger generation — AspireForm emits the registration code; the ASP.NET Core `AddOpenApi()` pipeline generates docs from it
- Request/response body schema editing (DTOs) → user writes their DTO classes manually; #4b only emits the `Map*` call
- Auth middleware configuration (`AddAuthentication`, `AddAuthorization`) → outside scope; `[ApiAuth]` sets the policy name only
- Multi-project scanning in one session — scanner operates on one project per UI session; user selects via the page
- Real-time multi-user editing — single-user assumption
- Undo/redo — git is the v1 undo

---

## 15. Risks & open questions

1. **Method-level Roslyn mutation is harder than class-level.** Method signatures, attributes, expression-bodied methods, and partial methods add corner cases. Mitigation: v1 supports only full-statement-body methods (`{ return ...; }`); expression-bodied and partial methods are recognized (scanned) but not mutated (the mutator returns an error diagnostic). Expanding support is #4b.1.

2. **Route pattern parsing.** `{name:constraint?}` parsing covers 95% of real-world cases; exotic constraints (custom type converters, etc.) may not parse. Mitigation: treat anything unrecognized as an unconstrained string parameter; the user edits the route string directly in the UI.

3. **Namespace inference for `_Endpoints.g.cs`.** Reading `<RootNamespace>` from MSBuild properties requires the workspace to be loaded. Mitigation: if the property isn't available, fall back to the assembly name (always available from the csproj filename).

4. **Fixture project doesn't reference `AspireForm.Annotations`.** The test scanner fixture projects don't add a package reference to `AspireForm.Annotations`, so attributes are stub-defined inline. This is the same pattern `RoslynEntityScannerTests` uses for `DbContext`/`DbSet` — continue the pattern.

5. **`aspireform ui` dual catalog startup cost.** Registering both `IEntityCatalogService` and `IEndpointCatalogService` doubles the lazy-workspace cost. Mitigation: both are `Singleton` and lazy-init on first request — the cost is only paid on the first scan; subsequent scans reuse the cached workspace.

---

## 16. Definition of done (sub-project #4b)

- `aspireform ui` shows an Endpoints nav link; `/endpoints` page opens and lists endpoints from an `[ApiEndpoint]`-decorated fixture project
- All 10 new MCP tools registered and behaving end-to-end via stdio
- The built-in `api-endpoints` provider emits a correct `_Endpoints.g.cs` from a fixture Web project
- `AspireForm.Annotations 0.2.0` (with `[ApiEndpoint]`, `[ApiAuth]`, `[ApiTag]`, `[ApiSummary]`) packs cleanly and is referenceable from a netstandard2.0/net6+ project
- Tests: scanner + mutator fixture tests + emitter tests + 10 MCP tool tests + provider tests + ≥3 bUnit component tests; xUnit v3 / MTP suite green
- `AspireForm 0.6.0` ready to ship
- README has a "Use the API builder" section with a code-first authoring snippet and the Claude Code MCP config update
