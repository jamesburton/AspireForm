# AspireForm API-Definition Builder — Plan 4b.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AspireForm 0.6.0` + bump sibling `AspireForm.Annotations` to `0.2.0` adding a code-first Minimal API endpoint builder — Roslyn-backed `ApiCatalog` (scanner + mutator + emitter), 10 fine-grained MCP tools, `/endpoints` Blazor Server page, and a new built-in `api-endpoints` Module provider that emits `_Endpoints.g.cs` from `[ApiEndpoint]`-decorated methods.

**Architecture:** Roslyn `MSBuildWorkspace` reads + writes the user's endpoint handler `.cs` files via `IEndpointCatalogService`. Blazor pages, MCP tools, and the `api-endpoints` provider all call it. `MSBuildBootstrap.EnsureRegistered()` is already idempotent from #4a — no new MSBuild plumbing. `UiHost.cs` registers the new service alongside the existing `IEntityCatalogService`.

**Design spec:** `docs/superpowers/specs/2026-05-26-aspireform-api-definition-builder-design.md` — authoritative. Read it before implementing any task.

**Tech Stack:** .NET 10, Roslyn `MSBuildWorkspace` (already in csproj from #4a), Blazor Server, xUnit v3 / MTP, AwesomeAssertions, bUnit.

**Solo-dev workflow:** Work in worktree on branch `worktree-agent-a59e202037909d7c4`.

**Gotchas carried from #4a:**
1. `Microsoft.CodeAnalysis.Workspaces.MSBuild` requires `Microsoft.CodeAnalysis.CSharp.Workspaces` — already in csproj.
2. Blazor Server needs `app.UseAntiforgery()` — already in `UiHost.cs`.
3. `AspireForm.csproj` uses `Microsoft.NET.Sdk.Web`; `<IsPackable>true</IsPackable>` already set.
4. Subagents must verify commit with `git log -1 --oneline` before reporting DONE.
5. Path resolution: `Planner` absolutizes `AppHostDirectory`; use that pattern in new providers.
6. Razor pages need `@using` in `_Imports.razor` for new component namespaces.
7. Test runner: `dotnet run --project tests/AspireForm.Tests` (full suite). Never `dotnet test`.
8. **New for #4b:** `RoslynEndpointMutator` v1 supports only full-statement-body methods; expression-bodied methods are scanned but not mutated (return an error diagnostic).
9. **New for #4b:** Fixture projects for scanner/mutator tests stub the `[ApiEndpoint]` attribute inline (same pattern as entity fixtures stub `DbContext`/`DbSet`).

---

## File map

**New (production):**

- `src/AspireForm/ApiCatalog/EndpointModel.cs` — `EndpointCatalog`, `EndpointInfo`, `RouteParameter`, `EndpointMutationResult` records
- `src/AspireForm/ApiCatalog/EndpointChangeRequest.cs` — sealed-record mutation DSL
- `src/AspireForm/ApiCatalog/EndpointCatalogException.cs`
- `src/AspireForm/ApiCatalog/IEndpointCatalogService.cs` — DI seam
- `src/AspireForm/ApiCatalog/RoslynEndpointScanner.cs`
- `src/AspireForm/ApiCatalog/RoslynEndpointMutator.cs`
- `src/AspireForm/ApiCatalog/EndpointEmitter.cs`
- `src/AspireForm/ApiCatalog/RoslynEndpointCatalogService.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointListTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointShowTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointCreateTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointDeleteTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointParameterAddTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointParameterRemoveTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointAuthSetTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointAttributeSetTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointAttributeClearTool.cs`
- `src/AspireForm/Mcp/Tools/Endpoint/EndpointEmitTool.cs`
- `src/AspireForm/Providers/ApiEndpoints/ApiEndpointsModuleProvider.cs`
- `src/AspireForm/Ui/Components/Pages/Endpoints.razor`
- `src/AspireForm/Ui/Components/Endpoint/EndpointList.razor`
- `src/AspireForm/Ui/Components/Endpoint/EndpointHeader.razor`
- `src/AspireForm/Ui/Components/Endpoint/EndpointParametersTab.razor`
- `src/AspireForm/Ui/Components/Endpoint/EndpointAuthTab.razor`
- `src/AspireForm/Ui/Components/Endpoint/EndpointAttributesTab.razor`
- `src/AspireForm/Ui/Components/Dialogs/NewEndpointDialog.razor`
- `src/AspireForm.Annotations/ApiEndpointAttribute.cs`
- `src/AspireForm.Annotations/ApiAuthAttribute.cs`
- `src/AspireForm.Annotations/ApiTagAttribute.cs`
- `src/AspireForm.Annotations/ApiSummaryAttribute.cs`

**Modified:**

- `src/AspireForm/AspireForm.csproj` — version 0.5.0 → 0.6.0
- `src/AspireForm.Annotations/AspireForm.Annotations.csproj` — version 0.1.0 → 0.2.0
- `src/AspireForm/Cli/McpCommand.cs` — register 10 new endpoint tools (registry grows 29 → 39)
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor` — add Endpoints nav link
- `src/AspireForm/Ui/Components/_Imports.razor` — add `@using AspireForm.ApiCatalog` + `@using AspireForm.Ui.Components.Endpoint`
- `src/AspireForm/Ui/UiHost.cs` — register `IEndpointCatalogService`
- `src/AspireForm/Program.cs` — register `api-endpoints` provider
- `README.md` — add "Use the API builder" section
- `CHANGELOG.md` — `[0.6.0]` entry with Annotations 0.2.0 note

**New (tests):**

- `tests/AspireForm.Tests/ApiCatalog/EndpointModelTests.cs`
- `tests/AspireForm.Tests/ApiCatalog/RoslynEndpointScannerTests.cs`
- `tests/AspireForm.Tests/ApiCatalog/RoslynEndpointMutatorTests.cs`
- `tests/AspireForm.Tests/ApiCatalog/Fixtures/EndpointFixtureProjectBuilder.cs`
- `tests/AspireForm.Tests/ApiCatalog/EndpointEmitterTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Endpoint/EndpointToolsReadTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Endpoint/EndpointToolsMutationTests.cs`
- `tests/AspireForm.Tests/Providers/ApiEndpoints/ApiEndpointsModuleProviderTests.cs`
- `tests/AspireForm.Tests/Ui/EndpointsPageTests.cs`

---

## Task 1: Bump versions — AspireForm 0.5.0 → 0.6.0, Annotations 0.1.0 → 0.2.0

**Files:**
- Modify: `src/AspireForm/AspireForm.csproj`
- Modify: `src/AspireForm.Annotations/AspireForm.Annotations.csproj`

- [ ] **Step 1: Edit `src/AspireForm/AspireForm.csproj`** — change `<Version>0.5.0</Version>` to `<Version>0.6.0</Version>`.

- [ ] **Step 2: Edit `src/AspireForm.Annotations/AspireForm.Annotations.csproj`** — change `<Version>0.1.0</Version>` to `<Version>0.2.0</Version>`. Also update the `<Description>` to mention API endpoint attributes.

- [ ] **Step 3: Build to confirm no regressions**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build --nologo -v q
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/AspireForm.csproj src/AspireForm.Annotations/AspireForm.Annotations.csproj
git -c commit.gpgsign=false commit -m "chore: bump AspireForm to 0.6.0, AspireForm.Annotations to 0.2.0"
```

---

## Task 2: Add API endpoint attributes to AspireForm.Annotations 0.2.0

**Files:**
- Create: `src/AspireForm.Annotations/ApiEndpointAttribute.cs`
- Create: `src/AspireForm.Annotations/ApiAuthAttribute.cs`
- Create: `src/AspireForm.Annotations/ApiTagAttribute.cs`
- Create: `src/AspireForm.Annotations/ApiSummaryAttribute.cs`

Implement exactly as specified in spec §4. Each attribute is `sealed`, has XML doc comments, and appropriate `[AttributeUsage]`. `[ApiTag]` is `AllowMultiple = true`; all others `AllowMultiple = false`.

- [ ] **Step 1: Create all four attribute files** per spec §4 signatures.

- [ ] **Step 2: Build the Annotations project standalone**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build src/AspireForm.Annotations/AspireForm.Annotations.csproj --nologo -v q
```

- [ ] **Step 3: Add a trivial smoke test** in `tests/AspireForm.Tests/Annotations/ApiAnnotationsTests.cs` — instantiate each attribute, assert property values round-trip.

- [ ] **Step 4: Run test class**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Annotations.ApiAnnotationsTests"
```

- [ ] **Step 5: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm.Annotations/ tests/AspireForm.Tests/Annotations/
git -c commit.gpgsign=false commit -m "feat(annotations): add ApiEndpoint, ApiAuth, ApiTag, ApiSummary attributes (0.2.0)"
```

---

## Task 3: ApiCatalog domain model + change request DSL

**Files:**
- Create: `src/AspireForm/ApiCatalog/EndpointModel.cs`
- Create: `src/AspireForm/ApiCatalog/EndpointChangeRequest.cs`
- Create: `src/AspireForm/ApiCatalog/EndpointCatalogException.cs`
- Create: `tests/AspireForm.Tests/ApiCatalog/EndpointModelTests.cs`

**`EndpointModel.cs`** — immutable records per spec §5:
- `EndpointCatalog(IReadOnlyList<EndpointInfo> Endpoints, IReadOnlyList<CatalogDiagnostic> Diagnostics)`
- `EndpointInfo` (all fields per spec §5)
- `RouteParameter(string Name, string? Constraint, bool IsOptional)`
- `EndpointMutationResult(bool Success, IReadOnlyList<string> ChangedFiles, IReadOnlyList<CatalogDiagnostic> Diagnostics)` with `Ok(...)` and `Fail(...)` factory methods
- Reuse `AttributeInstance` and `CatalogDiagnostic` from `AspireForm.EntityCatalog` namespace (they live in the same `src/AspireForm` assembly — just `using AspireForm.EntityCatalog;`)

**`EndpointChangeRequest.cs`** — abstract record hierarchy:
- `CreateEndpoint(string MethodName, string TypeName, string Route, string HttpMethod, string FilePath, string Namespace)`
- `DeleteEndpoint(string MethodName, string? TypeName)`
- `AddParameter(string MethodName, string? TypeName, string ParamName, string ClrType)`
- `RemoveParameter(string MethodName, string? TypeName, string ParamName)`
- `SetAttribute(string MethodName, string? TypeName, AttributeInstance Attribute)` — uses `AspireForm.EntityCatalog.AttributeInstance`
- `ClearAttribute(string MethodName, string? TypeName, string AttributeFullTypeName)`
- `SetAuthPolicy(string MethodName, string? TypeName, string Policy)`

**`EndpointCatalogException.cs`** — mirrors `EntityCatalogException`; two ctors (message, message+inner).

**`EndpointModelTests.cs`** — ≥3 tests: `EndpointMutationResult.Ok` factory, `EndpointMutationResult.Fail` factory, `EndpointChangeRequest` pattern-match.

- [ ] **Step 1:** Create all three production files per descriptions above.
- [ ] **Step 2:** Create `EndpointModelTests.cs` with ≥3 tests using AwesomeAssertions.
- [ ] **Step 3:** Build + run test class.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.ApiCatalog.EndpointModelTests"
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/ApiCatalog/ tests/AspireForm.Tests/ApiCatalog/EndpointModelTests.cs
git -c commit.gpgsign=false commit -m "feat(api-catalog): add endpoint domain model, change-request DSL, and exception"
```

---

## Task 4: IEndpointCatalogService DI seam

**Files:**
- Create: `src/AspireForm/ApiCatalog/IEndpointCatalogService.cs`

Mirror `IEntityCatalogService` exactly — same two methods (`ScanAsync`, `MutateAsync`) but with endpoint types:

```csharp
public interface IEndpointCatalogService
{
    Task<EndpointCatalog> ScanAsync(string csprojPath, CancellationToken ct);
    Task<EndpointMutationResult> MutateAsync(string csprojPath, EndpointChangeRequest request, CancellationToken ct);
}
```

No tests needed for the interface itself. Build to confirm.

- [ ] **Step 1:** Create the file.
- [ ] **Step 2:** Build.
- [ ] **Step 3: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/ApiCatalog/IEndpointCatalogService.cs
git -c commit.gpgsign=false commit -m "feat(api-catalog): add IEndpointCatalogService DI seam"
```

---

## Task 5: EndpointFixtureProjectBuilder + RoslynEndpointScanner

**Files:**
- Create: `tests/AspireForm.Tests/ApiCatalog/Fixtures/EndpointFixtureProjectBuilder.cs`
- Create: `src/AspireForm/ApiCatalog/RoslynEndpointScanner.cs`
- Create: `tests/AspireForm.Tests/ApiCatalog/RoslynEndpointScannerTests.cs`

**`EndpointFixtureProjectBuilder.cs`** — mirrors `FixtureProjectBuilder` from EntityCatalog tests. Same pattern: write a temp-dir csproj (net10.0, no package references — attributes are stubbed inline). Key difference: add a helper `AddEndpointHandlerFile(string relativePath, string handlerSource)` that writes a `.cs` file containing a stub `ApiEndpointAttribute` inline plus the provided handler source.

Stub attribute to include in each fixture file (so fixture projects don't need to reference AspireForm.Annotations):
```csharp
// Stub — replaces AspireForm.Annotations reference in fixture projects
namespace AspireForm.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ApiEndpointAttribute : System.Attribute
    {
        public ApiEndpointAttribute(string route, string method = "GET") { Route = route; Method = method; }
        public string Route { get; }
        public string Method { get; }
    }
    // Add stubs for ApiAuth, ApiTag, ApiSummary similarly
}
```

**`RoslynEndpointScanner.cs`** — per spec §6:
- `MSBuildBootstrap.EnsureRegistered()` → `MSBuildWorkspace.Create()` → `OpenProjectAsync` → `GetCompilationAsync`
- Walk all `INamedTypeSymbol`s; for each walk `IMethodSymbol` members
- A method is an endpoint if any attribute has `FullName == "AspireForm.Annotations.ApiEndpointAttribute"`
- Extract route, method from ctor args; summary, auth, tags from sibling attributes
- Parse route parameters from `{name}`, `{name:constraint}`, `{name?}` tokens
- Map all method attributes to `AttributeInstance`s
- Surface `WorkspaceDiagnostics` + ambiguous-route warnings as `CatalogDiagnostic`s
- Cache workspace per instance (lazy-init, same project path)
- Implement `IAsyncDisposable`

**`RoslynEndpointScannerTests.cs`** — ≥4 tests:
1. Empty fixture project → `EndpointCatalog` with empty `Endpoints`
2. Single `[ApiEndpoint("/books", "GET")]` on a static method → 1 endpoint discovered, correct route + method
3. `[ApiEndpoint("/books/{id:int}")]` → `RouteParameter(Name:"id", Constraint:"int", IsOptional:false)` extracted
4. `[ApiAuth("policy1")]` sibling → `AuthPolicy == "policy1"` in `EndpointInfo`
5. Ambiguous routes (same route+method on two methods) → warning diagnostic surfaced

- [ ] **Step 1:** Create `EndpointFixtureProjectBuilder.cs`.
- [ ] **Step 2:** Create `RoslynEndpointScanner.cs` per spec §6.
- [ ] **Step 3:** Create `RoslynEndpointScannerTests.cs` with ≥4 tests.
- [ ] **Step 4:** Build + run scanner tests (these are slow — allow up to 60s).

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.ApiCatalog.RoslynEndpointScannerTests"
```

- [ ] **Step 5: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/ApiCatalog/RoslynEndpointScanner.cs tests/AspireForm.Tests/ApiCatalog/
git -c commit.gpgsign=false commit -m "feat(api-catalog): add RoslynEndpointScanner with fixture tests"
```

---

## Task 6: RoslynEndpointMutator

**Files:**
- Create: `src/AspireForm/ApiCatalog/RoslynEndpointMutator.cs`
- Create: `tests/AspireForm.Tests/ApiCatalog/RoslynEndpointMutatorTests.cs`

**`RoslynEndpointMutator.cs`** — per spec §7. Supports mutation requests:
- `CreateEndpoint` — new static class + method in new `.cs` file; skeleton: `[ApiEndpoint(route, method)] public static IResult Handle(HttpContext ctx) => Results.Ok();`
- `DeleteEndpoint` — remove method (and `[ApiEndpoint]` attribute); remove class if empty
- `AddParameter` / `RemoveParameter` — append/remove typed parameter from method signature
- `SetAttribute` / `ClearAttribute` — add/replace/remove attribute on method
- `SetAuthPolicy` — shorthand for `SetAttribute` on `ApiAuthAttribute`
- **V1 constraint:** expression-bodied methods (`=> expr`) are recognized (scanned) but NOT mutated — return `EndpointMutationResult.Fail("Expression-bodied methods are not supported for mutation in v1.")`.
- All-or-nothing transactional: stage all changes in memory; only write to disk when all succeed.

**`RoslynEndpointMutatorTests.cs`** — ≥4 tests:
1. `CreateEndpoint` → new file created, method has `[ApiEndpoint]` attribute
2. `AddParameter(string name, "string")` → method signature has new parameter
3. `SetAuthPolicy("admin")` → method gains `[ApiAuth("admin")]`
4. Expression-bodied method mutation → `EndpointMutationResult.Fail` with appropriate message
5. `DeleteEndpoint` on sole method in class → class file deleted

- [ ] **Step 1:** Create `RoslynEndpointMutator.cs`.
- [ ] **Step 2:** Create `RoslynEndpointMutatorTests.cs` with ≥4 tests.
- [ ] **Step 3:** Build + run mutator tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.ApiCatalog.RoslynEndpointMutatorTests"
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/ApiCatalog/RoslynEndpointMutator.cs tests/AspireForm.Tests/ApiCatalog/RoslynEndpointMutatorTests.cs
git -c commit.gpgsign=false commit -m "feat(api-catalog): add RoslynEndpointMutator with fixture tests"
```

---

## Task 7: EndpointEmitter + RoslynEndpointCatalogService

**Files:**
- Create: `src/AspireForm/ApiCatalog/EndpointEmitter.cs`
- Create: `src/AspireForm/ApiCatalog/RoslynEndpointCatalogService.cs`
- Create: `tests/AspireForm.Tests/ApiCatalog/EndpointEmitterTests.cs`

**`EndpointEmitter.cs`** — per spec §8:
- `Render(EndpointCatalog catalog, string rootNamespace)` → file content as `string`
- Emits `// <auto-generated />` + `// aspireform: managed block="api-endpoints"` header
- One `app.Map{HttpMethod}(route, HandlerType.MethodName)` per endpoint
- Fluent chain: `.WithName(methodName)` always; `.WithSummary(...)` if `[ApiSummary]` present; `.RequireAuthorization(policy)` if `[ApiAuth]` present and policy != "anonymous"; `.AllowAnonymous()` if policy == "anonymous"; `.WithTags(...)` if `[ApiTag]` present
- Method selection: `MapGet/Post/Put/Patch/Delete` for known methods; `Map(route, handler)` for unknown
- Empty catalog → emits the file with empty `MapAspireFormEndpoints` body

**`RoslynEndpointCatalogService.cs`** — default impl of `IEndpointCatalogService`:
- Wraps a `RoslynEndpointScanner` and `RoslynEndpointMutator`
- Implements `IAsyncDisposable` (delegates to scanner)

**`EndpointEmitterTests.cs`** — ≥4 unit tests (in-memory catalog, assert emitted string content):
1. Empty catalog → emits valid C# with empty method body
2. Single GET endpoint → emits `app.MapGet(...)` with `.WithName`
3. Endpoint with `[ApiSummary]` → `.WithSummary(...)` in chain
4. Endpoint with `[ApiAuth("anonymous")]` → `.AllowAnonymous()` (not `RequireAuthorization`)
5. Unknown HTTP method → `app.Map(...)` (not `app.MapFoo`)

- [ ] **Step 1:** Create `EndpointEmitter.cs`.
- [ ] **Step 2:** Create `RoslynEndpointCatalogService.cs`.
- [ ] **Step 3:** Create `EndpointEmitterTests.cs` with ≥4 tests.
- [ ] **Step 4:** Build + run emitter tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.ApiCatalog.EndpointEmitterTests"
```

- [ ] **Step 5: Commit**

```bash
cd "C:/Development/AspireForm/.claire/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/ApiCatalog/ tests/AspireForm.Tests/ApiCatalog/EndpointEmitterTests.cs
git -c commit.gpgsign=false commit -m "feat(api-catalog): add EndpointEmitter + RoslynEndpointCatalogService"
```

---

## Task 8: api-endpoints Module provider

**Files:**
- Create: `src/AspireForm/Providers/ApiEndpoints/ApiEndpointsModuleProvider.cs`
- Create: `tests/AspireForm.Tests/Providers/ApiEndpoints/ApiEndpointsModuleProviderTests.cs`
- Modify: `src/AspireForm/Program.cs` — register the provider

**`ApiEndpointsModuleProvider.cs`** — per spec §9:
- `Plan(ModuleContext ctx, CancellationToken ct)`:
  1. Resolve `inputs.projectPath` → absolute path; throw `PluginContractException` if missing
  2. Create `RoslynEndpointScanner`, scan → `EndpointCatalog`
  3. Resolve `outputPath` (from inputs; default = `{projectDir}/Generated/_Endpoints.g.cs`)
  4. Infer `rootNamespace` from MSBuild property or csproj file name
  5. `EndpointEmitter.Render(catalog, rootNamespace)` → file content
  6. Return `PlannedFileAction(outputPath, FileOwnership.Managed, "api-endpoints", content)`
  7. Zero endpoints → info-level diagnostic; still emits the file
  8. Ambiguous route → warning; first-wins
- Register in the provider registry alongside `ef-data`, `sql-server`, etc.

**`ApiEndpointsModuleProviderTests.cs`** — ≥3 tests:
1. Missing `projectPath` → `PluginContractException`
2. Fixture project with one `[ApiEndpoint]` → `PlannedFileAction` for `_Endpoints.g.cs`
3. Empty project → emits file with empty body + info diagnostic

**`Program.cs` edit** — locate where other built-in providers are registered; add `api-endpoints`.

- [ ] **Step 1:** Create `ApiEndpointsModuleProvider.cs`.
- [ ] **Step 2:** Create `ApiEndpointsModuleProviderTests.cs`.
- [ ] **Step 3:** Register the provider in `Program.cs`.
- [ ] **Step 4:** Build + run tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Providers.ApiEndpoints.ApiEndpointsModuleProviderTests"
```

- [ ] **Step 5:** Run full test suite to check no regressions.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet run --project tests/AspireForm.Tests
```

- [ ] **Step 6: Commit + push**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Providers/ApiEndpoints/ src/AspireForm/Program.cs tests/AspireForm.Tests/Providers/ApiEndpoints/
git -c commit.gpgsign=false commit -m "feat(provider): add api-endpoints Module provider (emits _Endpoints.g.cs)"
git push
```

---

## Task 9: MCP endpoint tools — read tools (list, show, emit)

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointListTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointShowTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointEmitTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/Endpoint/EndpointToolsReadTests.cs`

All tools follow existing MCP conventions (see `EntityListTool.cs` as the template):
- Catch `EndpointCatalogException` + `FileNotFoundException` as `isError: true` tool results
- Never throw across JSON-RPC boundary
- Create a fresh `RoslynEndpointCatalogService` per tool call (no shared state at the MCP layer)

**`EndpointListTool`** — `aspireform_endpoint_list`; required input `projectPath`; returns table text (one row per endpoint: Method, Route, HandlerType.MethodName).

**`EndpointShowTool`** — `aspireform_endpoint_show`; required `methodName`, `projectPath`; optional `typeName`; returns indented JSON of the matching `EndpointInfo`.

**`EndpointEmitTool`** — `aspireform_endpoint_emit`; required `projectPath`; optional `outputPath`; scans + emits, writes file to `outputPath`, returns emitted file content as text.

**`EndpointToolsReadTests.cs`** — ≥3 tests using a fixture project:
1. `EndpointListTool` with empty project → non-error result, empty table
2. `EndpointListTool` with one endpoint → table row present
3. `EndpointShowTool` unknown method → `isError: true`

- [ ] **Step 1:** Create the three tool files.
- [ ] **Step 2:** Create `EndpointToolsReadTests.cs`.
- [ ] **Step 3:** Build + run tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Endpoint.EndpointToolsReadTests"
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Mcp/Tools/Endpoint/ tests/AspireForm.Tests/Mcp/Tools/Endpoint/EndpointToolsReadTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add endpoint read tools (list, show, emit)"
```

---

## Task 10: MCP endpoint tools — mutation tools (create, delete)

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointCreateTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointDeleteTool.cs`

**`EndpointCreateTool`** — `aspireform_endpoint_create`; required `methodName`, `typeName`, `route`, `projectPath`; optional `httpMethod` (default "GET"), `filePath`, `namespace`; dispatches `CreateEndpoint` change request; returns `EndpointMutationResult` as JSON.

**`EndpointDeleteTool`** — `aspireform_endpoint_delete`; required `methodName`, `projectPath`; optional `typeName`; dispatches `DeleteEndpoint`; returns `EndpointMutationResult` JSON.

Add tests for both in `EndpointToolsMutationTests.cs`:
1. `EndpointCreateTool` → new file created, scan confirms endpoint exists
2. `EndpointDeleteTool` → scan confirms endpoint no longer exists

- [ ] **Step 1:** Create the two tool files.
- [ ] **Step 2:** Create `tests/AspireForm.Tests/Mcp/Tools/Endpoint/EndpointToolsMutationTests.cs` with ≥2 tests.
- [ ] **Step 3:** Build + run tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Endpoint.EndpointToolsMutationTests"
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Mcp/Tools/Endpoint/EndpointCreateTool.cs src/AspireForm/Mcp/Tools/Endpoint/EndpointDeleteTool.cs tests/AspireForm.Tests/Mcp/Tools/Endpoint/
git -c commit.gpgsign=false commit -m "feat(mcp): add endpoint mutation tools (create, delete)"
```

---

## Task 11: MCP endpoint tools — parameter + auth + attribute tools

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointParameterAddTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointParameterRemoveTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointAuthSetTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointAttributeSetTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Endpoint/EndpointAttributeClearTool.cs`

Follow the spec §10 table for required/optional inputs and return shapes. All dispatch the corresponding `EndpointChangeRequest` subtype. No additional test file needed for this batch — the existing `EndpointToolsMutationTests.cs` can be extended with 2-3 more tests covering `EndpointAuthSetTool` and `EndpointParameterAddTool`.

- [ ] **Step 1:** Create all five tool files.
- [ ] **Step 2:** Add ≥2 tests to `EndpointToolsMutationTests.cs` (auth set + parameter add).
- [ ] **Step 3:** Build + run mutation tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Endpoint.EndpointToolsMutationTests"
```

- [ ] **Step 4: Commit + push**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Mcp/Tools/Endpoint/
git -c commit.gpgsign=false commit -m "feat(mcp): add endpoint parameter, auth, and attribute tools"
git push
```

---

## Task 12: Register all 10 MCP endpoint tools in McpCommand

**Files:**
- Modify: `src/AspireForm/Cli/McpCommand.cs`

Locate `BuildRegistry` (or equivalent registration method). After the last existing entity tool registration (registry at 29), add all 10 endpoint tools in order matching the spec §10 table. Registry should grow to 39.

- [ ] **Step 1:** Read `McpCommand.cs` to identify the registration pattern.
- [ ] **Step 2:** Add registrations for all 10 endpoint tools.
- [ ] **Step 3:** Build.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build --nologo -v q
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Cli/McpCommand.cs
git -c commit.gpgsign=false commit -m "feat(mcp): register 10 endpoint tools (registry: 29 → 39)"
```

---

## Task 13: UiHost DI registration + _Imports.razor update

**Files:**
- Modify: `src/AspireForm/Ui/UiHost.cs`
- Modify: `src/AspireForm/Ui/Components/_Imports.razor`

**`UiHost.cs`** — add after the existing `IEntityCatalogService` registration:
```csharp
builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
```

**`_Imports.razor`** — add two `@using` directives:
```razor
@using AspireForm.ApiCatalog
@using AspireForm.Ui.Components.Endpoint
```

- [ ] **Step 1:** Edit both files.
- [ ] **Step 2:** Build.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build --nologo -v q
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Ui/UiHost.cs src/AspireForm/Ui/Components/_Imports.razor
git -c commit.gpgsign=false commit -m "feat(ui): register IEndpointCatalogService in UiHost, add _Imports usings"
```

---

## Task 14: Blazor Endpoints page + nav link

**Files:**
- Create: `src/AspireForm/Ui/Components/Pages/Endpoints.razor`
- Modify: `src/AspireForm/Ui/Components/Layout/MainLayout.razor`

**`Endpoints.razor`** — 2-pane master/detail (spec §11.2). Mirror `Entities.razor` structure:
- Left pane: `<EndpointList>` component + search input + "+ New Endpoint" button (opens `<NewEndpointDialog>`)
- Right pane: `<EndpointHeader>` (shows `{Method} {Route}` + handler class/method) + three tabs: Parameters, Auth, Attributes
- Inject `IEndpointCatalogService`; `OnInitializedAsync` triggers scan using `UiOptions.ProjectPath`
- Handle null selection (no endpoint selected) with a placeholder message
- On mutation success: re-scan and re-render (same pattern as `Entities.razor`)

**`MainLayout.razor`** — add `<a href="/endpoints">Endpoints</a>` nav link between the Entities and Diagnostics links.

- [ ] **Step 1:** Read `Entities.razor` for the structural pattern to mirror.
- [ ] **Step 2:** Create `Endpoints.razor`.
- [ ] **Step 3:** Edit `MainLayout.razor`.
- [ ] **Step 4:** Build.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build --nologo -v q
```

- [ ] **Step 5: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Ui/Components/Pages/Endpoints.razor src/AspireForm/Ui/Components/Layout/MainLayout.razor
git -c commit.gpgsign=false commit -m "feat(ui): add Endpoints page and nav link"
```

---

## Task 15: Blazor Endpoint sub-components

**Files:**
- Create: `src/AspireForm/Ui/Components/Endpoint/EndpointList.razor`
- Create: `src/AspireForm/Ui/Components/Endpoint/EndpointHeader.razor`
- Create: `src/AspireForm/Ui/Components/Endpoint/EndpointParametersTab.razor`
- Create: `src/AspireForm/Ui/Components/Endpoint/EndpointAuthTab.razor`
- Create: `src/AspireForm/Ui/Components/Endpoint/EndpointAttributesTab.razor`
- Create: `src/AspireForm/Ui/Components/Dialogs/NewEndpointDialog.razor`

Mirror the Entity equivalents (`EntityList.razor`, `EntityHeader.razor`, etc.):

**`EndpointList.razor`** — `[Parameter] IReadOnlyList<EndpointInfo> Endpoints`, `[Parameter] EventCallback<EndpointInfo> OnSelect`, `[Parameter] string? SearchFilter`; renders a list of endpoint buttons filtered by search.

**`EndpointHeader.razor`** — `[Parameter] EndpointInfo? Endpoint`; shows `{HttpMethod} {Route}` in a prominent heading, handler class + method name below.

**`EndpointParametersTab.razor`** — `[Parameter] EndpointInfo? Endpoint`, `[Parameter] IEndpointCatalogService Service`, `[Parameter] string ProjectPath`; lists `RouteParameter`s and method parameters; "+ Add Parameter" button dispatches `AddParameter` change request.

**`EndpointAuthTab.razor`** — `[Parameter] EndpointInfo? Endpoint`, `[Parameter] IEndpointCatalogService Service`, `[Parameter] string ProjectPath`; shows current auth policy; text input + "Set Policy" button dispatches `SetAuthPolicy`.

**`EndpointAttributesTab.razor`** — `[Parameter] EndpointInfo? Endpoint`, `[Parameter] IEndpointCatalogService Service`, `[Parameter] string ProjectPath`; lists all `AttributeInstance`s; "Clear" button per attribute dispatches `ClearAttribute`.

**`NewEndpointDialog.razor`** — modal dialog with inputs for Route, Method, TypeName, MethodName; "Create" button dispatches `CreateEndpoint`; raises `EventCallback<EndpointInfo> OnCreated`.

- [ ] **Step 1:** Create all six component files.
- [ ] **Step 2:** Build.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet build --nologo -v q
```

- [ ] **Step 3: Commit + push**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add src/AspireForm/Ui/Components/Endpoint/ src/AspireForm/Ui/Components/Dialogs/NewEndpointDialog.razor
git -c commit.gpgsign=false commit -m "feat(ui): add Endpoint sub-components (List, Header, ParametersTab, AuthTab, AttributesTab, NewEndpointDialog)"
git push
```

---

## Task 16: bUnit tests for Endpoints page

**Files:**
- Create: `tests/AspireForm.Tests/Ui/EndpointsPageTests.cs`

Use a fake `IEndpointCatalogService` (in-memory). Mirror `EntitiesPageTests.cs` for test structure. ≥3 tests:

1. Page renders with empty catalog → shows "No endpoints found" (or equivalent empty state) and nav link is present
2. Page renders with 2 endpoints → `EndpointList` shows 2 items
3. Selecting an endpoint → `EndpointHeader` shows the correct route + method

Register the fake service with bUnit's `Services.AddSingleton<IEndpointCatalogService>(fake)`.

- [ ] **Step 1:** Read `EntitiesPageTests.cs` for the bUnit test pattern.
- [ ] **Step 2:** Create `EndpointsPageTests.cs` with ≥3 tests.
- [ ] **Step 3:** Build + run tests.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Ui.EndpointsPageTests"
```

- [ ] **Step 4: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add tests/AspireForm.Tests/Ui/EndpointsPageTests.cs
git -c commit.gpgsign=false commit -m "test(ui): add bUnit tests for Endpoints page"
```

---

## Task 17: Full test suite + regression check

Run the full test suite. All prior tests from #4a must still pass; all new #4b tests must pass.

- [ ] **Step 1:** Run full suite.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && dotnet run --project tests/AspireForm.Tests
```

- [ ] **Step 2:** If any failures exist — fix before proceeding. Do NOT continue to Task 18 with a red suite.

- [ ] **Step 3: Commit any fixes**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add -A
git -c commit.gpgsign=false commit -m "fix: resolve full-suite test failures after #4b integration"
```

---

## Task 18: README + CHANGELOG

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`

**README** — add a "## Use the API builder" section after the existing EF entity builder section. Content:
1. Brief description (1-2 sentences)
2. Code snippet: decorate a handler method with `[ApiEndpoint("/books", "GET")]`
3. Snippet: run `aspireform apply` → `_Endpoints.g.cs` emitted
4. Snippet: call `app.MapAspireFormEndpoints()` from `Program.cs`
5. Claude Code MCP config update note: registry grows to 39 tools (`aspireform_endpoint_*`)

**CHANGELOG** — prepend a `[0.6.0]` entry with:
- Added: `ApiCatalog` (scanner, mutator, emitter), `api-endpoints` provider, 10 MCP endpoint tools, `/endpoints` UI page
- Changed: `AspireForm.Annotations` bumped to `0.2.0` (adds `[ApiEndpoint]`, `[ApiAuth]`, `[ApiTag]`, `[ApiSummary]`)
- Migration: if using `AspireForm.Annotations`, update your project reference from `0.1.0` to `0.2.0`

- [ ] **Step 1:** Edit README.md.
- [ ] **Step 2:** Edit CHANGELOG.md.
- [ ] **Step 3: Commit**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add README.md CHANGELOG.md
git -c commit.gpgsign=false commit -m "docs: add README API builder section and CHANGELOG [0.6.0] entry"
```

---

## Task 19: Pack both packages + verify

- [ ] **Step 1:** Pack both packages.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts -p:EnableSourceControlManagerQueries=false
dotnet pack src/AspireForm.Annotations/AspireForm.Annotations.csproj -o ./artifacts -p:EnableSourceControlManagerQueries=false
```

- [ ] **Step 2:** Verify `.nupkg` files exist and contain the expected version.

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4" && ls artifacts/
```

Expected: `AspireForm.0.6.0.nupkg` and `AspireForm.Annotations.0.2.0.nupkg`.

- [ ] **Step 3: Commit + final push**

```bash
cd "C:/Development/AspireForm/.claude/worktrees/agent-a59e202037909d7c4"
git add artifacts/
git -c commit.gpgsign=false commit -m "chore: pack AspireForm 0.6.0 + AspireForm.Annotations 0.2.0 artifacts"
git push
```

---

## Appendix: Batching guidance for agentic workers

| Tasks | Who | Notes |
|---|---|---|
| 1 | Orchestrator (inline) | Trivial version bump |
| 2 | Single subagent | Attribute files are trivial; batch together |
| 3–4 | Single subagent | Domain model + seam; no Roslyn I/O |
| 5 | Single subagent | Scanner is complex; give it a 10-min timeout |
| 6 | Single subagent | Mutator; give it a 10-min timeout |
| 7 | Single subagent | Emitter + service |
| 8 | Single subagent | Provider; run full suite at end |
| 9–11 | Batched: 3 subagents in parallel (9, 10, 11) | Each tool batch is independent |
| 12 | Orchestrator (inline) | McpCommand registration — read file first |
| 13 | Single subagent | UiHost + _Imports |
| 14–15 | Batched: 2 subagents in parallel | UI page + sub-components are independent |
| 16 | Single subagent | bUnit tests; needs all UI files in place |
| 17 | Orchestrator (inline) | Full suite gate |
| 18 | Orchestrator (inline) | Docs only |
| 19 | Orchestrator (inline) | Pack + verify |

Push after tasks 8, 11, 15, 17, and 19.
