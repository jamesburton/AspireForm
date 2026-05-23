# AspireForm Core Engine — Plan 2: Planner & `plan` verb

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the side-effect-free planner — provider contract, dependency graph, three-way reconcile (desired vs state vs disk), drift detection, and a unified-diff `plan` command that tells the user what `apply` *would* do.

**Architecture:** A `Planner` consumes the `ProjectModel` (from Plan 1), the loaded `AspireFormState` (Plan 1), and the project directory, dispatches each block to its `IProvider` for a `ProviderPlan` (file + CLI actions described, not executed), runs three-way reconciliation against state + disk checksums, topo-sorts by `dependsOn`, and produces a `Plan` that the `PlanRenderer` formats as unified diffs. `aspireform plan` is the verb. **Plan 2 ships no execution** — `apply` arrives in Plan 3.

**Tech Stack:** Inherits Plan 1's stack — .NET 10 (`net10.0`, SDK 10.0.300), `Spectre.Console.Cli` 0.55.0, `YamlDotNet` 18.0.0, xUnit v3 3.2.2 on Microsoft Testing Platform, `AwesomeAssertions` 9.4.0. **No new package dependencies** — the standard library covers SHA-256, topo sort, and text diffing.

**Spec:** `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` — this plan implements §5 (provider registry & contracts), §6 (reconciliation model: block-level actions, file-level ownership modes, drift, plan/apply split — plan side only), and the `plan` row of §9.

**Plan position:** Plan 2 of 3. Plan 1 (Foundations & `config`) shipped as `AspireForm 0.1.0` on NuGet. Plan 3 adds the executor and the `apply`/`destroy`/`new`/`add`/`import`/`state` verbs.

---

## Conventions for the executor

- **Assertions:** use `AwesomeAssertions` (`value.Should()....`) in every test. Never `Assert.*`.
- **XML docs:** every public type and public member gets at least a one-line `/// <summary>`.
- **Run tests:** `dotnet run --project tests/AspireForm.Tests --configuration Debug` is the authoritative invocation on this Windows setup (the xUnit v3 MTP in-process runner). `dotnet test` works but is slower and occasionally reports "Zero tests ran"; prefer `dotnet run` for tight loops.
- All paths are relative to the repo root `c:/Development/AspireForm`.
- This plan adds **no new NuGet packages** — `System.Security.Cryptography.SHA256` and `System.Collections.Generic` are sufficient.

---

## Important empirical truths (verified before writing this plan)

1. **`aspire add sqlserver` only edits the AppHost `.csproj`**, adding `<PackageReference Include="Aspire.Hosting.SqlServer" Version="<latest>" />`. It does NOT touch `AppHost.cs`. AspireForm therefore fully owns the `builder.AddSqlServer("sql").AddDatabase("appdb")` line, inside a marker region.
2. **Default Aspire AppHost.cs** (from `dotnet new aspire-apphost`):
   ```csharp
   var builder = DistributedApplication.CreateBuilder(args);

   builder.Build().Run();
   ```
   AspireForm-managed regions are inserted **before** `builder.Build().Run();` (the "anchor").
3. **`ef-data` Module v1 scope is narrow on purpose.** The minimal reference sample has no service project to wire a `DbContext` into. The `ef-data` Module in this plan therefore scaffolds the `DbContext` class file + emits a `managed` region in `AppHost.cs` containing a comment block that records the intent (database name, context name, dependency). Real DI/migration wiring is deferred to a richer reference scaffold in Plan 3 or beyond. The MODULE concept is still exercised: `dependsOn: [sql]`, `scaffold` mode, `managed` mode, destroy-protection.

---

## File structure (locked)

```
src/AspireForm/Providers/
  IProvider.cs                       — IProvider, BlockKind, OwnershipMode, PlannedFileAction,
                                       PlannedCliAction, ProviderPlan, PlanContext
  ProviderRegistry.cs                — built-in lookup by type
  SqlServerResourceProvider.cs       — sqlserver Resource provider
  EfDataModuleProvider.cs            — ef-data Module provider
src/AspireForm/Planning/
  MarkerRegion.cs                    — read/insert/replace `// <aspireform:block=X>...</...>`
                                       regions in a file's text. Independent unit.
  DependencyGraph.cs                 — topo sort + cycle detection
  Plan.cs                            — Plan, BlockAction, BlockActionKind, FileActionPlan,
                                       FileActionKind
  DriftDetector.cs                   — SHA-256 checksum + per-tracked-file drift status
  Reconciler.cs                      — three-way reconcile producing a Plan (block-level diff
                                       + per-file actions resolved by ownership + drift)
  Planner.cs                         — public orchestrator. ProjectModel + AspireFormState
                                       + projectDir → Plan
  PlanRenderer.cs                    — Plan → unified-diff string
src/AspireForm/Cli/
  PlanCommand.cs                     — `aspireform plan` verb
src/AspireForm/Program.cs            — modified to register the plan command

tests/AspireForm.Tests/
  Providers/SqlServerResourceProviderTests.cs
  Providers/EfDataModuleProviderTests.cs
  Providers/ProviderRegistryTests.cs
  Planning/MarkerRegionTests.cs
  Planning/DependencyGraphTests.cs
  Planning/DriftDetectorTests.cs
  Planning/ReconcilerTests.cs
  Planning/PlannerTests.cs
  Planning/PlanRendererTests.cs
  Cli/PlanCommandTests.cs            — joins ConsoleCaptureCollection (Console redirection)
  EndToEnd/PlanSmokeTests.cs         — adds a `plan` smoke test next to existing CliSmokeTests
```

---

## Task 1: Provider contract — types

**Files:**
- Create: `src/AspireForm/Providers/IProvider.cs`
- Test: `tests/AspireForm.Tests/Providers/ProviderContractTests.cs`

Establishes the data shapes every other task in this plan depends on. The contract is pure data + interface; no logic.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class ProviderContractTests
{
    [Fact]
    public void PlannedFileAction_carries_path_mode_and_content_renderer()
    {
        var action = new PlannedFileAction(
            Path: "MyApp.AppHost/AppHost.cs",
            OwnershipMode: OwnershipMode.Managed,
            BlockMarker: "sql",
            RenderContent: () => "rendered");

        action.OwnershipMode.Should().Be(OwnershipMode.Managed);
        action.RenderContent().Should().Be("rendered");
    }

    [Fact]
    public void PlannedCliAction_carries_tool_and_args()
    {
        var action = new PlannedCliAction("aspire", new[] { "add", "sqlserver" });
        action.Tool.Should().Be("aspire");
        action.Args.Should().Equal("add", "sqlserver");
    }

    [Fact]
    public void ProviderPlan_defaults_both_collections_to_empty()
    {
        var plan = new ProviderPlan();
        plan.FileActions.Should().BeEmpty();
        plan.CliActions.Should().BeEmpty();
    }

    [Fact]
    public void PlanContext_exposes_block_name_inputs_and_apphost_dir()
    {
        var ctx = new PlanContext(
            BlockName: "sql",
            Inputs: new JsonObject { ["aspireName"] = "sql" },
            AppHostDirectory: "./MyApp.AppHost",
            ProjectName: "MyApp");

        ctx.BlockName.Should().Be("sql");
        ctx.Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
        ctx.AppHostDirectory.Should().Be("./MyApp.AppHost");
        ctx.ProjectName.Should().Be("MyApp");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/AspireForm.Tests`
Expected: build error — types do not exist.

- [ ] **Step 3: Create `src/AspireForm/Providers/IProvider.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Whether a block describes infrastructure (Resource) or a feature-slice (Module).</summary>
public enum BlockKind
{
    /// <summary>Infrastructure (e.g. SQL Server); managed, destroyable.</summary>
    Resource,

    /// <summary>Feature slice that scaffolds cross-layer code; destroy-protected by default.</summary>
    Module,
}

/// <summary>How AspireForm owns a generated file across re-applies.</summary>
public enum OwnershipMode
{
    /// <summary>Re-rendered every apply via structured/marker-region edits.</summary>
    Managed,

    /// <summary>Generated once; never re-touched (developer owns subsequent edits).</summary>
    Scaffold,

    /// <summary>3-way merge: state baseline vs on-disk vs newly-rendered.</summary>
    Merge,
}

/// <summary>One file that a provider intends to write or update.</summary>
/// <param name="Path">Repo-relative target path.</param>
/// <param name="OwnershipMode">How re-applies should treat the file.</param>
/// <param name="BlockMarker">The marker name used inside the file for Managed regions (e.g. <c>sql</c>); ignored for other modes.</param>
/// <param name="RenderContent">Produces the full rendered file content (or, for Managed regions, the content that belongs *inside* the marker region).</param>
public sealed record PlannedFileAction(
    string Path,
    OwnershipMode OwnershipMode,
    string BlockMarker,
    Func<string> RenderContent);

/// <summary>One CLI invocation a provider intends to make (e.g. <c>aspire add sqlserver</c>).</summary>
/// <param name="Tool">The executable name (e.g. <c>aspire</c>, <c>dotnet</c>).</param>
/// <param name="Args">The arguments to pass.</param>
public sealed record PlannedCliAction(string Tool, IReadOnlyList<string> Args);

/// <summary>A provider's description of what it would do for a single block. Pure data; no I/O.</summary>
public sealed class ProviderPlan
{
    /// <summary>File-level intents.</summary>
    public IReadOnlyList<PlannedFileAction> FileActions { get; init; } = [];

    /// <summary>CLI invocation intents.</summary>
    public IReadOnlyList<PlannedCliAction> CliActions { get; init; } = [];
}

/// <summary>Inputs passed to <see cref="IProvider.Plan(PlanContext)"/>.</summary>
/// <param name="BlockName">The block's name in the config (e.g. <c>sql</c>).</param>
/// <param name="Inputs">Provider-specific inputs from the config.</param>
/// <param name="AppHostDirectory">Repo-relative path to the AppHost project directory.</param>
/// <param name="ProjectName">The project name from the <c>aspireform</c> header.</param>
public sealed record PlanContext(
    string BlockName,
    JsonObject Inputs,
    string AppHostDirectory,
    string ProjectName);

/// <summary>A built-in or plug-in provider for one Resource or Module type.</summary>
public interface IProvider
{
    /// <summary>The block type this provider handles (e.g. <c>sqlserver</c>).</summary>
    string Type { get; }

    /// <summary>Whether this is a Resource or a Module.</summary>
    BlockKind Kind { get; }

    /// <summary>Describes what this provider would do for the given block. Pure; no I/O.</summary>
    ProviderPlan Plan(PlanContext context);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/AspireForm.Tests`
Expected: PASS — 4 new tests (total = 62).

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/IProvider.cs tests/AspireForm.Tests/Providers/ProviderContractTests.cs
git commit -m "feat: add provider contract types (IProvider, ProviderPlan, PlannedFileAction, etc.)"
```

---

## Task 2: ProviderRegistry

**Files:**
- Create: `src/AspireForm/Providers/ProviderRegistry.cs`
- Test: `tests/AspireForm.Tests/Providers/ProviderRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class ProviderRegistryTests
{
    private sealed class FakeProvider(string type) : IProvider
    {
        public string Type => type;
        public BlockKind Kind => BlockKind.Resource;
        public ProviderPlan Plan(PlanContext context) => new();
    }

    [Fact]
    public void Get_returns_the_provider_for_a_known_type()
    {
        var registry = new ProviderRegistry([new FakeProvider("sqlserver")]);
        registry.Get("sqlserver").Type.Should().Be("sqlserver");
    }

    [Fact]
    public void Get_throws_a_clear_error_for_an_unknown_type()
    {
        var registry = new ProviderRegistry([new FakeProvider("sqlserver")]);
        var act = () => registry.Get("ghost");
        act.Should().Throw<ProviderNotFoundException>().WithMessage("*ghost*");
    }

    [Fact]
    public void Constructor_throws_when_two_providers_register_the_same_type()
    {
        var act = () => new ProviderRegistry(
            [new FakeProvider("dupe"), new FakeProvider("dupe")]);
        act.Should().Throw<ArgumentException>().WithMessage("*dupe*");
    }

    [Fact]
    public void Default_registry_contains_the_v1_built_in_providers()
    {
        var registry = ProviderRegistry.Default();
        registry.Get("sqlserver").Should().NotBeNull();
        registry.Get("ef-data").Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile error)**

- [ ] **Step 3: Create `src/AspireForm/Providers/ProviderRegistry.cs`**

```csharp
namespace AspireForm.Providers;

/// <summary>Raised when a config block references a provider type that is not registered.</summary>
public sealed class ProviderNotFoundException : Exception
{
    /// <summary>Initialises the exception with a message naming the missing type.</summary>
    public ProviderNotFoundException(string type)
        : base($"No provider is registered for block type '{type}'.") { }
}

/// <summary>Resolves a block's <c>type</c> to its <see cref="IProvider"/>.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProvider> _byType;

    /// <summary>Creates a registry from an explicit list of providers. Throws on duplicate types.</summary>
    public ProviderRegistry(IEnumerable<IProvider> providers)
    {
        _byType = new(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!_byType.TryAdd(provider.Type, provider))
            {
                throw new ArgumentException(
                    $"Duplicate provider registration for type '{provider.Type}'.",
                    nameof(providers));
            }
        }
    }

    /// <summary>Returns the registry containing the v1 built-in providers (<c>sqlserver</c> and <c>ef-data</c>).</summary>
    public static ProviderRegistry Default() =>
        new([new SqlServerResourceProvider(), new EfDataModuleProvider()]);

    /// <summary>Returns the provider for <paramref name="type"/>, or throws <see cref="ProviderNotFoundException"/>.</summary>
    public IProvider Get(string type) =>
        _byType.TryGetValue(type, out var provider)
            ? provider
            : throw new ProviderNotFoundException(type);
}
```

> The `Default()` factory references types created in Tasks 4 and 5. The project will not compile after Task 2 alone — that's expected. Add no-op stub classes (one-line `IProvider` impls that throw `NotImplementedException` from `Plan`) for `SqlServerResourceProvider` and `EfDataModuleProvider` if you want a green compile in isolation; Tasks 4 and 5 replace them.

- [ ] **Step 4: With stubs in place, run tests to verify they pass (4 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/ tests/AspireForm.Tests/Providers/ProviderRegistryTests.cs
git commit -m "feat: add ProviderRegistry with built-in lookup"
```

---

## Task 3: MarkerRegion editor

A small, independent utility. Reads, inserts, and replaces marker-bracketed regions in file text. Used by the SqlServer / EfData providers to render Managed `AppHost.cs` regions.

Marker syntax: `// <aspireform:block=NAME>` ... `// </aspireform:block=NAME>` (inclusive of both lines). When inserting into a file that has no region for the named block, insertion happens just before the **anchor line** the caller supplies (e.g. `builder.Build().Run();`).

**Files:**
- Create: `src/AspireForm/Planning/MarkerRegion.cs`
- Test: `tests/AspireForm.Tests/Planning/MarkerRegionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class MarkerRegionTests
{
    private const string Anchor = "builder.Build().Run();";

    private const string Empty = """
        var builder = DistributedApplication.CreateBuilder(args);

        builder.Build().Run();
        """;

    [Fact]
    public void Insert_adds_a_new_region_before_the_anchor()
    {
        var result = MarkerRegion.UpsertBeforeAnchor(Empty, blockName: "sql",
            innerContent: "var sql = builder.AddSqlServer(\"sql\");", anchor: Anchor);

        result.Should().Contain("// <aspireform:block=sql>")
              .And.Contain("var sql = builder.AddSqlServer(\"sql\");")
              .And.Contain("// </aspireform:block=sql>");

        var sqlIdx = result.IndexOf("// <aspireform:block=sql>", StringComparison.Ordinal);
        var anchorIdx = result.IndexOf(Anchor, StringComparison.Ordinal);
        sqlIdx.Should().BeLessThan(anchorIdx);
    }

    [Fact]
    public void Insert_then_upsert_replaces_inner_content_without_duplicating_the_region()
    {
        var afterInsert = MarkerRegion.UpsertBeforeAnchor(Empty, "sql",
            "var sql = builder.AddSqlServer(\"sql\");", Anchor);

        var afterUpdate = MarkerRegion.UpsertBeforeAnchor(afterInsert, "sql",
            "var sql = builder.AddSqlServer(\"sql\").AddDatabase(\"appdb\");", Anchor);

        // The new content is present; the old line is gone; only one region for 'sql'.
        afterUpdate.Should().Contain("AddDatabase(\"appdb\")");
        afterUpdate.Should().NotContain("var sql = builder.AddSqlServer(\"sql\");\n");
        var matches = System.Text.RegularExpressions.Regex.Matches(
            afterUpdate, @"// <aspireform:block=sql>");
        matches.Count.Should().Be(1);
    }

    [Fact]
    public void Two_different_blocks_can_coexist_in_the_same_file()
    {
        var step1 = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "S", Anchor);
        var step2 = MarkerRegion.UpsertBeforeAnchor(step1, "data", "D", Anchor);

        step2.Should().Contain("// <aspireform:block=sql>")
             .And.Contain("// <aspireform:block=data>");
    }

    [Fact]
    public void Remove_deletes_a_region_when_present_and_is_a_noop_otherwise()
    {
        var withRegion = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "X", Anchor);
        var removed = MarkerRegion.Remove(withRegion, "sql");
        removed.Should().NotContain("aspireform:block=sql");

        var stillEmpty = MarkerRegion.Remove(Empty, "sql");
        stillEmpty.Should().Be(Empty);
    }

    [Fact]
    public void TryReadInner_returns_the_inner_content_of_an_existing_region()
    {
        var withRegion = MarkerRegion.UpsertBeforeAnchor(Empty, "sql", "abc", Anchor);
        MarkerRegion.TryReadInner(withRegion, "sql", out var inner).Should().BeTrue();
        inner.Should().Be("abc");
    }

    [Fact]
    public void Upsert_throws_when_the_anchor_is_absent_and_no_existing_region_for_the_block()
    {
        var noAnchor = "// nothing to anchor to\n";
        var act = () => MarkerRegion.UpsertBeforeAnchor(noAnchor, "sql", "X", Anchor);
        act.Should().Throw<InvalidOperationException>().WithMessage("*anchor*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile error)**

- [ ] **Step 3: Create `src/AspireForm/Planning/MarkerRegion.cs`**

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace AspireForm.Planning;

/// <summary>
/// Reads, inserts, and replaces AspireForm-owned regions in a file's text, demarcated by
/// <c>// &lt;aspireform:block=NAME&gt;</c> ... <c>// &lt;/aspireform:block=NAME&gt;</c> lines.
/// </summary>
public static class MarkerRegion
{
    /// <summary>Builds the opening marker line for a block.</summary>
    public static string OpenMarker(string blockName) => $"// <aspireform:block={blockName}>";

    /// <summary>Builds the closing marker line for a block.</summary>
    public static string CloseMarker(string blockName) => $"// </aspireform:block={blockName}>";

    private static Regex RegionRegex(string blockName) => new(
        $@"^[ \t]*{Regex.Escape(OpenMarker(blockName))}\r?\n(?<inner>.*?)(?:\r?\n)?[ \t]*{Regex.Escape(CloseMarker(blockName))}[ \t]*\r?\n?",
        RegexOptions.Multiline | RegexOptions.Singleline);

    /// <summary>
    /// Inserts or replaces the named region inside <paramref name="text"/>. If the region exists,
    /// its inner content is replaced with <paramref name="innerContent"/>. Otherwise a new region
    /// is inserted immediately before the first line containing <paramref name="anchor"/>.
    /// Throws <see cref="InvalidOperationException"/> when no existing region is present and the
    /// anchor cannot be located.
    /// </summary>
    public static string UpsertBeforeAnchor(string text, string blockName, string innerContent, string anchor)
    {
        var match = RegionRegex(blockName).Match(text);
        if (match.Success)
        {
            var newRegion = $"{OpenMarker(blockName)}\n{innerContent}\n{CloseMarker(blockName)}\n";
            return string.Concat(text.AsSpan(0, match.Index), newRegion, text.AsSpan(match.Index + match.Length));
        }

        var anchorIndex = text.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            throw new InvalidOperationException(
                $"Cannot insert region '{blockName}': anchor '{anchor}' not found in file content.");
        }

        // Insert at the start of the anchor's line.
        var lineStart = text.LastIndexOf('\n', Math.Max(0, anchorIndex - 1)) + 1;
        var newRegionWithGap = $"{OpenMarker(blockName)}\n{innerContent}\n{CloseMarker(blockName)}\n\n";
        return string.Concat(text.AsSpan(0, lineStart), newRegionWithGap, text.AsSpan(lineStart));
    }

    /// <summary>Removes the named region if present; otherwise returns <paramref name="text"/> unchanged.</summary>
    public static string Remove(string text, string blockName) =>
        RegionRegex(blockName).Replace(text, string.Empty);

    /// <summary>Extracts the inner content of an existing region; returns false when the region is absent.</summary>
    public static bool TryReadInner(string text, string blockName, out string innerContent)
    {
        var match = RegionRegex(blockName).Match(text);
        if (!match.Success)
        {
            innerContent = string.Empty;
            return false;
        }

        innerContent = match.Groups["inner"].Value;
        return true;
    }
}
```

- [ ] **Step 4: Run tests (6 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/MarkerRegion.cs tests/AspireForm.Tests/Planning/MarkerRegionTests.cs
git commit -m "feat: add marker-region editor for managed-file regions"
```

---

## Task 4: SqlServerResourceProvider

**Files:**
- Replace: `src/AspireForm/Providers/SqlServerResourceProvider.cs` (the stub from Task 2)
- Test: `tests/AspireForm.Tests/Providers/SqlServerResourceProviderTests.cs`

The provider's plan:
- **CLI action:** `aspire add sqlserver` — this is what edits the AppHost `.csproj`. We don't reinvent it.
- **File action:** managed region in `<apphost-dir>/AppHost.cs` declaring the SqlServer resource + its databases. Inner content rendered from the block's inputs.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class SqlServerResourceProviderTests
{
    private readonly SqlServerResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("sql", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("sqlserver");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_an_aspire_add_sqlserver_cli_action()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "sql" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "sqlserver");
    }

    [Fact]
    public void Plan_emits_a_managed_apphost_region_with_the_resource_declaration()
    {
        var inputs = new JsonObject
        {
            ["aspireName"] = "sql",
            ["databases"] = new JsonArray("appdb", "reportdb"),
        };

        var plan = _provider.Plan(Ctx(inputs));

        plan.FileActions.Should().ContainSingle();
        var file = plan.FileActions[0];
        file.OwnershipMode.Should().Be(OwnershipMode.Managed);
        file.BlockMarker.Should().Be("sql");
        file.Path.Replace('\\', '/').Should().Be("./MyApp.AppHost/AppHost.cs");

        var content = file.RenderContent();
        content.Should().Contain("builder.AddSqlServer(\"sql\")");
        content.Should().Contain("AddDatabase(\"appdb\")");
        content.Should().Contain("AddDatabase(\"reportdb\")");
    }

    [Fact]
    public void Plan_uses_block_name_when_aspireName_is_absent()
    {
        var plan = _provider.Plan(Ctx(new JsonObject()));
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddSqlServer(\"sql\")");
    }

    [Fact]
    public void Plan_emits_no_database_calls_when_databases_array_is_absent_or_empty()
    {
        var planEmpty = _provider.Plan(Ctx(new JsonObject { ["databases"] = new JsonArray() }));
        planEmpty.FileActions[0].RenderContent().Should().NotContain(".AddDatabase(");

        var planMissing = _provider.Plan(Ctx(new JsonObject()));
        planMissing.FileActions[0].RenderContent().Should().NotContain(".AddDatabase(");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (the Task 2 stub returns an empty plan / NotImplementedException)**

- [ ] **Step 3: Replace `src/AspireForm/Providers/SqlServerResourceProvider.cs`**

```csharp
using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Built-in Resource provider for SQL Server. Delegates package add to <c>aspire add sqlserver</c>; owns the AppHost resource declaration in a managed region.</summary>
public sealed class SqlServerResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "sqlserver";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var aspireName = context.Inputs["aspireName"]?.GetValue<string>() ?? context.BlockName;
        var databases = (context.Inputs["databases"] as JsonArray)?
            .Select(n => n?.GetValue<string>() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToArray() ?? [];

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            CliActions =
            [
                new PlannedCliAction("aspire", ["add", "sqlserver"]),
            ],
            FileActions =
            [
                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderInner(aspireName, databases, context.BlockName)),
            ],
        };
    }

    private static string RenderInner(string aspireName, IReadOnlyList<string> databases, string blockName)
    {
        var sb = new StringBuilder();
        sb.Append("var ").Append(blockName).Append(" = builder.AddSqlServer(\"").Append(aspireName).Append("\");");

        foreach (var db in databases)
        {
            sb.AppendLine();
            sb.Append("var ").Append(blockName).Append('_').Append(db)
              .Append(" = ").Append(blockName).Append(".AddDatabase(\"").Append(db).Append("\");");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests (5 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/SqlServerResourceProvider.cs tests/AspireForm.Tests/Providers/SqlServerResourceProviderTests.cs
git commit -m "feat: add SqlServerResourceProvider"
```

---

## Task 5: EfDataModuleProvider

A minimal `ef-data` Module v1: scaffolds a `DbContext` class and emits a managed region in `AppHost.cs` that records the dependency on the named database. Full DI / migration-runner wiring waits for a richer reference (Plan 3+).

**Files:**
- Replace: `src/AspireForm/Providers/EfDataModuleProvider.cs` (the stub from Task 2)
- Test: `tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class EfDataModuleProviderTests
{
    private readonly EfDataModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("data", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("ef-data");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_scaffolds_the_dbcontext_class_at_the_configured_path()
    {
        var inputs = new JsonObject
        {
            ["database"] = "appdb",
            ["contextName"] = "AppDbContext",
        };

        var plan = _provider.Plan(Ctx(inputs));

        var scaffoldFile = plan.FileActions
            .SingleOrDefault(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffoldFile.Should().NotBeNull();
        scaffoldFile!.Path.Replace('\\', '/').Should().Be("./MyApp.AppHost/Data/AppDbContext.cs");
        scaffoldFile.RenderContent().Should().Contain("class AppDbContext : DbContext");
        scaffoldFile.RenderContent().Should().Contain("Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Plan_emits_a_managed_apphost_region_recording_the_database_dependency()
    {
        var inputs = new JsonObject
        {
            ["database"] = "appdb",
            ["contextName"] = "AppDbContext",
        };

        var plan = _provider.Plan(Ctx(inputs));

        var managedFile = plan.FileActions
            .SingleOrDefault(f => f.OwnershipMode == OwnershipMode.Managed);
        managedFile.Should().NotBeNull();
        managedFile!.BlockMarker.Should().Be("data");

        var content = managedFile.RenderContent();
        content.Should().Contain("ef-data module").And.Contain("AppDbContext").And.Contain("appdb");
    }

    [Fact]
    public void Plan_emits_no_cli_actions_in_v1()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["database"] = "appdb", ["contextName"] = "X" }));
        plan.CliActions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_defaults_contextName_when_absent()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["database"] = "appdb" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.Path.Should().EndWith("AppDbContext.cs");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Replace `src/AspireForm/Providers/EfDataModuleProvider.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Built-in Module provider for EF Core data access. v1 scaffolds a DbContext and records the dependency in a managed AppHost region.</summary>
public sealed class EfDataModuleProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "ef-data";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var database = context.Inputs["database"]?.GetValue<string>() ?? "appdb";
        var contextName = context.Inputs["contextName"]?.GetValue<string>() ?? "AppDbContext";

        var contextFile = Path.Combine(context.AppHostDirectory, "Data", $"{contextName}.cs");
        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: contextFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderDbContext(contextName, context.ProjectName)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(database, contextName)),
            ],
        };
    }

    private static string RenderDbContext(string contextName, string projectName) => $$"""
        using Microsoft.EntityFrameworkCore;

        namespace {{projectName}}.AppHost.Data;

        /// <summary>EF Core DbContext scaffolded by AspireForm (ef-data module). Add DbSet&lt;T&gt; properties as your model grows.</summary>
        public class {{contextName}} : DbContext
        {
            /// <summary>Initialises the context with the runtime-injected options.</summary>
            public {{contextName}}(DbContextOptions<{{contextName}}> options) : base(options) { }
        }
        """;

    private static string RenderAppHostRegion(string database, string contextName) => $"""
        // ef-data module: {contextName} bound to database '{database}'.
        // Wire your service project here (e.g. .WithReference({database})).
        """;
}
```

- [ ] **Step 4: Run tests (5 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/EfDataModuleProvider.cs tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs
git commit -m "feat: add EfDataModuleProvider (minimal v1 — DbContext scaffold + managed region)"
```

---

## Task 6: DependencyGraph (topo sort + cycle detection)

**Files:**
- Create: `src/AspireForm/Planning/DependencyGraph.cs`
- Test: `tests/AspireForm.Tests/Planning/DependencyGraphTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class DependencyGraphTests
{
    [Fact]
    public void TopologicallySort_returns_dependencies_before_dependents()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["data"] = ["sql"],
            ["sql"] = [],
        };

        var order = DependencyGraph.TopologicallySort(edges);

        order.Should().HaveCount(2);
        order.IndexOf("sql").Should().BeLessThan(order.IndexOf("data"));
    }

    [Fact]
    public void TopologicallySort_orders_independent_nodes_alphabetically_for_determinism()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["z"] = [], ["a"] = [], ["m"] = [],
        };

        var order = DependencyGraph.TopologicallySort(edges);
        order.Should().Equal("a", "m", "z");
    }

    [Fact]
    public void TopologicallySort_throws_on_a_cycle_and_names_the_blocks_involved()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["a"] = ["b"],
            ["b"] = ["c"],
            ["c"] = ["a"],
        };

        var act = () => DependencyGraph.TopologicallySort(edges);

        var ex = act.Should().Throw<DependencyCycleException>().Which;
        ex.Cycle.Should().Contain("a").And.Contain("b").And.Contain("c");
    }

    [Fact]
    public void TopologicallySort_throws_on_self_loop()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["x"] = ["x"],
        };

        var act = () => DependencyGraph.TopologicallySort(edges);
        act.Should().Throw<DependencyCycleException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/DependencyGraph.cs`**

```csharp
namespace AspireForm.Planning;

/// <summary>Raised when a dependency graph contains a cycle.</summary>
public sealed class DependencyCycleException : Exception
{
    /// <summary>The block names participating in the cycle, in traversal order.</summary>
    public IReadOnlyList<string> Cycle { get; }

    /// <summary>Initialises the exception with the offending cycle.</summary>
    public DependencyCycleException(IReadOnlyList<string> cycle)
        : base($"Dependency cycle detected: {string.Join(" → ", cycle)}.")
    {
        Cycle = cycle;
    }
}

/// <summary>Pure utility for topologically sorting block dependency graphs.</summary>
public static class DependencyGraph
{
    /// <summary>
    /// Returns a deterministic topological sort of the nodes in <paramref name="edges"/>.
    /// Dependencies precede dependents; ties are broken by ordinal string comparison of the
    /// node name. Throws <see cref="DependencyCycleException"/> on any cycle (including self-loops).
    /// </summary>
    public static IReadOnlyList<string> TopologicallySort(
        IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        var result = new List<string>(edges.Count);

        foreach (var node in edges.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            Visit(node);
        }

        return result;

        void Visit(string node)
        {
            if (visited.Contains(node))
            {
                return;
            }

            if (!stack.Add(node))
            {
                var cycleStart = path.IndexOf(node);
                var cycle = path.Skip(cycleStart).Append(node).ToList();
                throw new DependencyCycleException(cycle);
            }

            path.Add(node);

            if (edges.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps.OrderBy(d => d, StringComparer.Ordinal))
                {
                    Visit(dep);
                }
            }

            stack.Remove(node);
            path.RemoveAt(path.Count - 1);
            visited.Add(node);
            result.Add(node);
        }
    }
}
```

- [ ] **Step 4: Run tests (4 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/DependencyGraph.cs tests/AspireForm.Tests/Planning/DependencyGraphTests.cs
git commit -m "feat: add DependencyGraph with topo sort and cycle detection"
```

---

## Task 7: Plan model types

The data shape the planner produces and the renderer consumes.

**Files:**
- Create: `src/AspireForm/Planning/Plan.cs`
- Test: `tests/AspireForm.Tests/Planning/PlanModelTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Planning;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlanModelTests
{
    [Fact]
    public void Empty_plan_is_a_noop()
    {
        var plan = new Plan();
        plan.Blocks.Should().BeEmpty();
        plan.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void HasChanges_is_true_when_any_block_action_is_not_noop()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Create, []),
            ],
        };

        plan.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void FileActionPlan_carries_path_mode_marker_and_resolved_kind()
    {
        var fa = new FileActionPlan(
            Path: "MyApp.AppHost/AppHost.cs",
            OwnershipMode: OwnershipMode.Managed,
            BlockMarker: "sql",
            Kind: FileActionKind.Create,
            DriftDetected: false,
            BeforeContent: null,
            AfterContent: "rendered");

        fa.Kind.Should().Be(FileActionKind.Create);
        fa.DriftDetected.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/Plan.cs`**

```csharp
using AspireForm.Providers;

namespace AspireForm.Planning;

/// <summary>The action the planner intends to take against one block.</summary>
public enum BlockActionKind
{
    /// <summary>Block is in config but not in state; create it.</summary>
    Create,

    /// <summary>Block is in both config and state; inputs (or files) changed.</summary>
    Update,

    /// <summary>Block is in state but not in config; remove it.</summary>
    Delete,

    /// <summary>Block matches state and disk exactly; nothing to do.</summary>
    Noop,
}

/// <summary>The action the planner intends to take against one file inside a block.</summary>
public enum FileActionKind
{
    /// <summary>File does not exist on disk; will be written.</summary>
    Create,

    /// <summary>File exists; will be updated (Managed region replaced, full re-render, or merge).</summary>
    Modify,

    /// <summary>File exists; tool will not touch it (Scaffold mode + file already present).</summary>
    Skip,

    /// <summary>File previously tracked; will be removed (block delete).</summary>
    Remove,

    /// <summary>Drift requires human attention before apply can proceed.</summary>
    DriftBlocked,
}

/// <summary>One file's planned action.</summary>
/// <param name="Path">Repo-relative file path.</param>
/// <param name="OwnershipMode">The file's ownership mode.</param>
/// <param name="BlockMarker">Marker name (for Managed regions).</param>
/// <param name="Kind">The action that will be taken.</param>
/// <param name="DriftDetected">True when the file's on-disk checksum has diverged from the state baseline.</param>
/// <param name="BeforeContent">Current on-disk content (or null when the file is absent).</param>
/// <param name="AfterContent">Content that would be written (or null when the action is Skip / Remove).</param>
public sealed record FileActionPlan(
    string Path,
    OwnershipMode OwnershipMode,
    string BlockMarker,
    FileActionKind Kind,
    bool DriftDetected,
    string? BeforeContent,
    string? AfterContent);

/// <summary>One block's planned action.</summary>
public sealed record BlockAction(
    string BlockName,
    BlockKind BlockKind,
    BlockActionKind Kind,
    IReadOnlyList<FileActionPlan> FileActions)
{
    /// <summary>CLI invocations planned for this block (from the provider).</summary>
    public IReadOnlyList<PlannedCliAction> CliActions { get; init; } = [];
}

/// <summary>An ordered list of block actions — the full reconciliation plan.</summary>
public sealed class Plan
{
    /// <summary>Block actions in topological order.</summary>
    public IReadOnlyList<BlockAction> Blocks { get; init; } = [];

    /// <summary>True when any block action would actually change something.</summary>
    public bool HasChanges => Blocks.Any(b => b.Kind != BlockActionKind.Noop);
}
```

- [ ] **Step 4: Run tests (3 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/Plan.cs tests/AspireForm.Tests/Planning/PlanModelTests.cs
git commit -m "feat: add Plan, BlockAction, and FileActionPlan model types"
```

---

## Task 8: DriftDetector

**Files:**
- Create: `src/AspireForm/Planning/DriftDetector.cs`
- Test: `tests/AspireForm.Tests/Planning/DriftDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class DriftDetectorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-drift").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ComputeChecksum_is_stable_and_matches_known_sha256()
    {
        var path = Write("a.txt", "hello");
        DriftDetector.ComputeChecksum(path)
            .Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public void IsAbsent_returns_true_for_a_missing_file_and_false_for_a_present_one()
    {
        DriftDetector.IsAbsent(Path.Combine(_dir, "ghost.txt")).Should().BeTrue();
        var present = Write("there.txt", "x");
        DriftDetector.IsAbsent(present).Should().BeFalse();
    }

    [Fact]
    public void HasDrifted_returns_true_when_on_disk_checksum_differs_from_baseline()
    {
        var path = Write("a.txt", "current");
        const string baseline = "0000000000000000000000000000000000000000000000000000000000000000";
        DriftDetector.HasDrifted(path, baseline).Should().BeTrue();
    }

    [Fact]
    public void HasDrifted_returns_false_when_checksums_match()
    {
        var path = Write("a.txt", "hello");
        var hash = DriftDetector.ComputeChecksum(path);
        DriftDetector.HasDrifted(path, hash).Should().BeFalse();
    }

    [Fact]
    public void HasDrifted_returns_true_when_the_file_has_been_deleted()
    {
        DriftDetector.HasDrifted(Path.Combine(_dir, "deleted.txt"), "anyhash").Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/DriftDetector.cs`**

```csharp
using System.Security.Cryptography;

namespace AspireForm.Planning;

/// <summary>Filesystem-checksum drift detection for tracked files.</summary>
public static class DriftDetector
{
    /// <summary>SHA-256 hex digest of the file at <paramref name="path"/>.</summary>
    public static string ComputeChecksum(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>SHA-256 hex digest of an in-memory string (used to checksum freshly-rendered content).</summary>
    public static string ComputeChecksum(ReadOnlySpan<char> text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text.ToArray());
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>True when no file exists at <paramref name="path"/>.</summary>
    public static bool IsAbsent(string path) => !File.Exists(path);

    /// <summary>True when the file is missing or its on-disk checksum differs from <paramref name="baselineChecksum"/>.</summary>
    public static bool HasDrifted(string path, string baselineChecksum) =>
        IsAbsent(path) || !string.Equals(ComputeChecksum(path), baselineChecksum, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Run tests (5 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/DriftDetector.cs tests/AspireForm.Tests/Planning/DriftDetectorTests.cs
git commit -m "feat: add DriftDetector (SHA-256 checksum comparison)"
```

---

## Task 9: Reconciler

The three-way reconcile that produces `FileActionPlan`s, given a provider's `ProviderPlan`, the prior `BlockState` (if any), and the project directory. Two parts: **block-level** diff (desired vs state — produces `BlockActionKind`), and **file-level** resolution (given the block action, the ownership mode of each file, and on-disk reality, produce `FileActionKind` + drift flag + before/after content).

**Files:**
- Create: `src/AspireForm/Planning/Reconciler.cs`
- Test: `tests/AspireForm.Tests/Planning/ReconcilerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class ReconcilerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-reconcile").FullName;
    private readonly Reconciler _reconciler = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static PlannedFileAction Action(string path, OwnershipMode mode, string content) =>
        new(Path: path, OwnershipMode: mode, BlockMarker: "sql", RenderContent: () => content);

    [Fact]
    public void CREATE_with_a_scaffold_file_yields_a_file_create()
    {
        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(Path.Combine(_dir, "scaffolded.cs"), OwnershipMode.Scaffold, "// new")],
        };

        var actions = _reconciler.Reconcile(
            blockName: "sql",
            blockKind: BlockKind.Resource,
            blockKindAction: BlockActionKind.Create,
            providerPlan: providerPlan,
            previousState: null,
            projectDir: _dir);

        actions.FileActions.Should().ContainSingle();
        actions.FileActions[0].Kind.Should().Be(FileActionKind.Create);
        actions.FileActions[0].AfterContent.Should().Be("// new");
        actions.FileActions[0].BeforeContent.Should().BeNull();
    }

    [Fact]
    public void Scaffold_file_already_on_disk_resolves_to_skip()
    {
        var path = Path.Combine(_dir, "scaffolded.cs");
        File.WriteAllText(path, "// pre-existing developer code");

        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(path, OwnershipMode.Scaffold, "// new template")],
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Update,
            providerPlan, previousState: null, projectDir: _dir);

        actions.FileActions[0].Kind.Should().Be(FileActionKind.Skip);
    }

    [Fact]
    public void Managed_file_with_matching_checksum_resolves_to_modify()
    {
        var path = Path.Combine(_dir, "AppHost.cs");
        const string initial = "var builder = DistributedApplication.CreateBuilder(args);\n\nbuilder.Build().Run();\n";
        File.WriteAllText(path, initial);

        var prev = new BlockState
        {
            Type = "sqlserver",
            Kind = "resource",
            Files = { [path] = new FileState { OwnershipMode = "managed", Checksum = DriftDetector.ComputeChecksum(path) } },
        };

        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(path, OwnershipMode.Managed, "var sql = builder.AddSqlServer(\"sql\");")],
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Update,
            providerPlan, previousState: prev, projectDir: _dir);

        actions.FileActions[0].Kind.Should().Be(FileActionKind.Modify);
        actions.FileActions[0].DriftDetected.Should().BeFalse();
        actions.FileActions[0].AfterContent.Should().Contain("// <aspireform:block=sql>");
        actions.FileActions[0].AfterContent.Should().Contain("AddSqlServer");
    }

    [Fact]
    public void Managed_file_with_drift_flags_drift_but_still_proposes_modify()
    {
        var path = Path.Combine(_dir, "AppHost.cs");
        File.WriteAllText(path, "var builder = DistributedApplication.CreateBuilder(args);\n\nbuilder.Build().Run();\n");

        var prev = new BlockState
        {
            Type = "sqlserver",
            Kind = "resource",
            Files = { [path] = new FileState { OwnershipMode = "managed", Checksum = "stale_baseline" } },
        };

        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(path, OwnershipMode.Managed, "var sql = builder.AddSqlServer(\"sql\");")],
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Update,
            providerPlan, previousState: prev, projectDir: _dir);

        actions.FileActions[0].DriftDetected.Should().BeTrue();
        actions.FileActions[0].Kind.Should().Be(FileActionKind.Modify);
    }

    [Fact]
    public void DELETE_block_proposes_remove_for_every_tracked_file()
    {
        var path = Path.Combine(_dir, "tracked.cs");
        File.WriteAllText(path, "content");

        var prev = new BlockState
        {
            Type = "sqlserver",
            Kind = "resource",
            Files = { [path] = new FileState { OwnershipMode = "managed", Checksum = DriftDetector.ComputeChecksum(path) } },
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Delete,
            providerPlan: new ProviderPlan(), previousState: prev, projectDir: _dir);

        actions.FileActions.Should().ContainSingle();
        actions.FileActions[0].Kind.Should().Be(FileActionKind.Remove);
    }

    [Fact]
    public void NOOP_block_yields_no_file_actions()
    {
        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Noop,
            new ProviderPlan(), previousState: null, projectDir: _dir);
        actions.FileActions.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/Reconciler.cs`**

```csharp
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Planning;

/// <summary>The per-block result of reconciliation: ordered file actions + CLI invocations.</summary>
/// <param name="FileActions">Resolved per-file actions.</param>
/// <param name="CliActions">CLI invocations the provider wanted to perform.</param>
public sealed record BlockReconcileResult(
    IReadOnlyList<FileActionPlan> FileActions,
    IReadOnlyList<PlannedCliAction> CliActions);

/// <summary>
/// Three-way reconciler: combines a provider's <see cref="ProviderPlan"/> with the prior
/// <see cref="BlockState"/> and on-disk filesystem state, producing the resolved
/// <see cref="FileActionPlan"/> list for one block.
/// </summary>
public sealed class Reconciler
{
    /// <summary>Reconciles one block. Pure with respect to its inputs except that it reads files from <paramref name="projectDir"/>.</summary>
    public BlockReconcileResult Reconcile(
        string blockName,
        BlockKind blockKind,
        BlockActionKind blockKindAction,
        ProviderPlan providerPlan,
        BlockState? previousState,
        string projectDir)
    {
        if (blockKindAction == BlockActionKind.Noop)
        {
            return new BlockReconcileResult([], []);
        }

        if (blockKindAction == BlockActionKind.Delete)
        {
            return new BlockReconcileResult(BuildRemoveActions(previousState), []);
        }

        // CREATE or UPDATE: walk provider's file actions and resolve each.
        var resolved = new List<FileActionPlan>(providerPlan.FileActions.Count);
        foreach (var planned in providerPlan.FileActions)
        {
            resolved.Add(ResolveFileAction(planned, blockName, previousState));
        }

        return new BlockReconcileResult(resolved, providerPlan.CliActions);
    }

    private static FileActionPlan ResolveFileAction(
        PlannedFileAction planned, string blockName, BlockState? previousState)
    {
        var path = planned.Path;
        var exists = File.Exists(path);
        var beforeContent = exists ? File.ReadAllText(path) : null;
        var previousFile = previousState?.Files.GetValueOrDefault(path);
        var driftDetected = previousFile is not null && exists
            && !string.Equals(DriftDetector.ComputeChecksum(path), previousFile.Checksum, StringComparison.Ordinal);

        switch (planned.OwnershipMode)
        {
            case OwnershipMode.Scaffold:
                return exists
                    ? new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Skip, DriftDetected: driftDetected,
                        BeforeContent: beforeContent, AfterContent: null)
                    : new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Create, DriftDetected: false,
                        BeforeContent: null, AfterContent: planned.RenderContent());

            case OwnershipMode.Managed:
            {
                var inner = planned.RenderContent();
                string after;
                if (exists)
                {
                    after = MarkerRegion.UpsertBeforeAnchor(beforeContent!, blockName, inner,
                        anchor: "builder.Build().Run();");
                }
                else
                {
                    // No file yet — Managed mode still needs the host scaffold; synthesise a minimal AppHost.cs.
                    var host = "var builder = DistributedApplication.CreateBuilder(args);\n\nbuilder.Build().Run();\n";
                    after = MarkerRegion.UpsertBeforeAnchor(host, blockName, inner, "builder.Build().Run();");
                }

                return new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                    Kind: exists ? FileActionKind.Modify : FileActionKind.Create,
                    DriftDetected: driftDetected,
                    BeforeContent: beforeContent, AfterContent: after);
            }

            case OwnershipMode.Merge:
                // Plan 2 does not implement Merge mode; treat as Managed but flag for the renderer.
                goto case OwnershipMode.Managed;

            default:
                throw new InvalidOperationException($"Unknown ownership mode: {planned.OwnershipMode}.");
        }
    }

    private static IReadOnlyList<FileActionPlan> BuildRemoveActions(BlockState? previousState)
    {
        if (previousState is null)
        {
            return [];
        }

        var removals = new List<FileActionPlan>(previousState.Files.Count);
        foreach (var (path, fileState) in previousState.Files)
        {
            var mode = Enum.TryParse<OwnershipMode>(fileState.OwnershipMode, ignoreCase: true, out var parsed)
                ? parsed : OwnershipMode.Managed;

            removals.Add(new FileActionPlan(
                Path: path, OwnershipMode: mode, BlockMarker: string.Empty,
                Kind: FileActionKind.Remove, DriftDetected: false,
                BeforeContent: File.Exists(path) ? File.ReadAllText(path) : null,
                AfterContent: null));
        }

        return removals;
    }
}
```

- [ ] **Step 4: Run tests (6 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/Reconciler.cs tests/AspireForm.Tests/Planning/ReconcilerTests.cs
git commit -m "feat: add Reconciler — three-way diff producing FileActionPlans"
```

---

## Task 10: Planner

The public orchestrator. Takes the bound `ProjectModel`, the loaded `AspireFormState`, and `projectDir`. Performs block-level diff (CREATE/UPDATE/DELETE), topo-sorts, dispatches each block to its provider, runs the reconciler, and returns a `Plan`.

**Files:**
- Create: `src/AspireForm/Planning/Planner.cs`
- Test: `tests/AspireForm.Tests/Planning/PlannerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlannerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-planner").FullName;
    private readonly Planner _planner = new(ProviderRegistry.Default());

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ProjectModel ModelWith(params (string Name, ResourceBlock Block)[] resources)
    {
        var dict = resources.ToDictionary(r => r.Name, r => r.Block);
        return new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = dict,
        };
    }

    [Fact]
    public void Plan_for_a_new_sql_resource_emits_a_create_block_action()
    {
        var model = ModelWith(("sql", new ResourceBlock
        {
            Name = "sql",
            Type = "sqlserver",
            Inputs = new JsonObject { ["aspireName"] = "sql" },
        }));

        var plan = _planner.Plan(model, new AspireFormState(), _dir);

        plan.Blocks.Should().ContainSingle();
        plan.Blocks[0].BlockName.Should().Be("sql");
        plan.Blocks[0].Kind.Should().Be(BlockActionKind.Create);
        plan.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Plan_orders_modules_after_their_resource_dependencies()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
            Modules = new Dictionary<string, ModuleBlock>
            {
                ["data"] = new()
                {
                    Name = "data", Type = "ef-data", DependsOn = ["sql"],
                    Inputs = new JsonObject { ["database"] = "appdb", ["contextName"] = "AppDbContext" },
                },
            },
        };

        var plan = _planner.Plan(model, new AspireFormState(), _dir);

        plan.Blocks.Select(b => b.BlockName).Should().Equal("sql", "data");
    }

    [Fact]
    public void Plan_proposes_delete_for_a_block_in_state_but_absent_from_config()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { [Path.Combine(_dir, "AppHost.cs")] =
                new FileState { OwnershipMode = "managed", Checksum = "x" } },
        };

        var plan = _planner.Plan(ModelWith(), state, _dir);

        plan.Blocks.Should().ContainSingle(b => b.BlockName == "sql" && b.Kind == BlockActionKind.Delete);
    }

    [Fact]
    public void Plan_throws_when_a_provider_type_is_unknown()
    {
        var model = ModelWith(("x", new ResourceBlock { Name = "x", Type = "no-such-provider", Inputs = new JsonObject() }));
        var act = () => _planner.Plan(model, new AspireFormState(), _dir);
        act.Should().Throw<ProviderNotFoundException>();
    }

    [Fact]
    public void Plan_throws_on_a_dependency_cycle()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Modules = new Dictionary<string, ModuleBlock>
            {
                ["a"] = new() { Name = "a", Type = "ef-data", DependsOn = ["b"], Inputs = new JsonObject() },
                ["b"] = new() { Name = "b", Type = "ef-data", DependsOn = ["a"], Inputs = new JsonObject() },
            },
        };

        var act = () => _planner.Plan(model, new AspireFormState(), _dir);
        act.Should().Throw<DependencyCycleException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/Planner.cs`**

```csharp
using AspireForm.Configuration;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Planning;

/// <summary>The public planner. Builds a <see cref="Plan"/> from a <see cref="ProjectModel"/>, a <see cref="AspireFormState"/>, and the project directory.</summary>
public sealed class Planner
{
    private readonly ProviderRegistry _providers;
    private readonly Reconciler _reconciler = new();

    /// <summary>Initialises the planner with an explicit provider registry. Use <see cref="ProviderRegistry.Default"/> for the v1 built-ins.</summary>
    public Planner(ProviderRegistry providers) => _providers = providers;

    /// <summary>Builds a <see cref="Plan"/>. Reads files under <paramref name="projectDir"/> but writes nothing.</summary>
    public Plan Plan(ProjectModel model, AspireFormState state, string projectDir)
    {
        // Block-level diff
        var desired = model.Resources.Keys.Concat(model.Modules.Keys).ToHashSet(StringComparer.Ordinal);
        var stateBlocks = state.Blocks.Keys.ToHashSet(StringComparer.Ordinal);

        var creates = desired.Except(stateBlocks).ToHashSet(StringComparer.Ordinal);
        var updates = desired.Intersect(stateBlocks).ToHashSet(StringComparer.Ordinal);
        var deletes = stateBlocks.Except(desired).ToHashSet(StringComparer.Ordinal);

        // Build the dependency graph for desired blocks (deletes are appended unordered to the end).
        var edges = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var r in model.Resources.Values)
        {
            edges[r.Name] = [];
        }

        foreach (var m in model.Modules.Values)
        {
            edges[m.Name] = m.DependsOn;
        }

        var order = DependencyGraph.TopologicallySort(edges);

        var blocks = new List<BlockAction>();
        foreach (var name in order)
        {
            var (kind, providerType, inputs) = LookupDesired(model, name);
            var provider = _providers.Get(providerType);
            var ctx = new PlanContext(
                BlockName: name,
                Inputs: inputs,
                AppHostDirectory: model.AspireForm.AppHost,
                ProjectName: model.AspireForm.Project);

            var providerPlan = provider.Plan(ctx);
            var blockActionKind = creates.Contains(name) ? BlockActionKind.Create
                : updates.Contains(name) ? BlockActionKind.Update
                : BlockActionKind.Noop;

            var previousState = state.Blocks.GetValueOrDefault(name);
            var result = _reconciler.Reconcile(name, kind, blockActionKind, providerPlan, previousState, projectDir);

            blocks.Add(new BlockAction(name, kind, blockActionKind, result.FileActions)
            {
                CliActions = result.CliActions,
            });
        }

        // Deletes — pull from state, no provider needed.
        foreach (var name in deletes.OrderBy(n => n, StringComparer.Ordinal))
        {
            var previous = state.Blocks[name];
            var blockKind = string.Equals(previous.Kind, "module", StringComparison.OrdinalIgnoreCase)
                ? BlockKind.Module : BlockKind.Resource;
            var result = _reconciler.Reconcile(name, blockKind, BlockActionKind.Delete,
                providerPlan: new ProviderPlan(), previousState: previous, projectDir: projectDir);
            blocks.Add(new BlockAction(name, blockKind, BlockActionKind.Delete, result.FileActions));
        }

        return new Plan { Blocks = blocks };
    }

    private static (BlockKind Kind, string ProviderType, System.Text.Json.Nodes.JsonObject Inputs) LookupDesired(
        ProjectModel model, string name)
    {
        if (model.Resources.TryGetValue(name, out var r))
        {
            return (BlockKind.Resource, r.Type, r.Inputs);
        }

        var m = model.Modules[name];
        return (BlockKind.Module, m.Type, m.Inputs);
    }
}
```

- [ ] **Step 4: Run tests (5 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/Planner.cs tests/AspireForm.Tests/Planning/PlannerTests.cs
git commit -m "feat: add Planner — orchestrates provider dispatch and three-way reconcile"
```

---

## Task 11: PlanRenderer

Pretty-prints a `Plan` as the human-readable output of `aspireform plan`. Block headers (`+ sql (resource, sqlserver) — CREATE`, etc.), per-file actions, and unified-diff snippets where the file content changed.

**Files:**
- Create: `src/AspireForm/Planning/PlanRenderer.cs`
- Test: `tests/AspireForm.Tests/Planning/PlanRendererTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Planning;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlanRendererTests
{
    [Fact]
    public void Render_of_empty_plan_says_nothing_to_do()
    {
        var output = PlanRenderer.Render(new Plan());
        output.Should().Contain("No changes");
    }

    [Fact]
    public void Render_of_create_block_includes_block_header_and_file_actions()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Create,
                [
                    new FileActionPlan(
                        Path: "MyApp.AppHost/AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Create,
                        DriftDetected: false,
                        BeforeContent: null,
                        AfterContent: "var sql = builder.AddSqlServer(\"sql\");"),
                ])
                {
                    CliActions = [new PlannedCliAction("aspire", ["add", "sqlserver"])],
                },
            ],
        };

        var output = PlanRenderer.Render(plan);

        output.Should().Contain("+ sql").And.Contain("CREATE").And.Contain("sqlserver");
        output.Should().Contain("MyApp.AppHost/AppHost.cs");
        output.Should().Contain("aspire add sqlserver");
        output.Should().Contain("+ var sql = builder.AddSqlServer");
    }

    [Fact]
    public void Render_marks_drift_for_each_drifted_file()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Update,
                [
                    new FileActionPlan(
                        Path: "AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Modify,
                        DriftDetected: true,
                        BeforeContent: "old\n", AfterContent: "new\n"),
                ]),
            ],
        };

        var output = PlanRenderer.Render(plan);
        output.Should().Contain("DRIFT");
    }

    [Fact]
    public void Render_of_delete_block_shows_minus_marker()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Delete,
                [
                    new FileActionPlan(
                        Path: "AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Remove,
                        DriftDetected: false,
                        BeforeContent: "x\n", AfterContent: null),
                ]),
            ],
        };

        var output = PlanRenderer.Render(plan);
        output.Should().Contain("- sql").And.Contain("DELETE");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Planning/PlanRenderer.cs`**

```csharp
using System.Text;

namespace AspireForm.Planning;

/// <summary>Pretty-prints a <see cref="Plan"/> for the <c>aspireform plan</c> output.</summary>
public static class PlanRenderer
{
    /// <summary>Renders <paramref name="plan"/> as a human-readable, line-oriented string.</summary>
    public static string Render(Plan plan)
    {
        if (!plan.HasChanges && plan.Blocks.Count == 0)
        {
            return "No changes — desired state matches actual state.\n";
        }

        var sb = new StringBuilder();
        var changed = 0;

        foreach (var block in plan.Blocks)
        {
            sb.AppendLine(RenderBlockHeader(block));

            foreach (var cli in block.CliActions)
            {
                sb.Append("    will run: ").Append(cli.Tool).Append(' ').AppendLine(string.Join(' ', cli.Args));
            }

            foreach (var file in block.FileActions)
            {
                sb.AppendLine(RenderFileLine(file));
                if (file.Kind is FileActionKind.Create or FileActionKind.Modify or FileActionKind.Remove
                    && (file.AfterContent is not null || file.BeforeContent is not null))
                {
                    AppendUnifiedDiff(sb, file.BeforeContent ?? string.Empty, file.AfterContent ?? string.Empty);
                }
            }

            if (block.Kind != BlockActionKind.Noop)
            {
                changed++;
            }

            sb.AppendLine();
        }

        sb.Append("Summary: ").Append(changed).AppendLine(" block(s) would change.");
        return sb.ToString();
    }

    private static string RenderBlockHeader(BlockAction block) => block.Kind switch
    {
        BlockActionKind.Create => $"+ {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — CREATE",
        BlockActionKind.Update => $"~ {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — UPDATE",
        BlockActionKind.Delete => $"- {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — DELETE",
        BlockActionKind.Noop => $"  {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — no change",
        _ => block.BlockName,
    };

    private static string RenderFileLine(FileActionPlan file)
    {
        var prefix = file.Kind switch
        {
            FileActionKind.Create => "+",
            FileActionKind.Modify => "~",
            FileActionKind.Remove => "-",
            FileActionKind.Skip => " ",
            FileActionKind.DriftBlocked => "!",
            _ => "?",
        };

        var drift = file.DriftDetected ? "  [DRIFT]" : string.Empty;
        return $"    {prefix} {file.Path}  [{file.OwnershipMode.ToString().ToLowerInvariant()}, {file.Kind.ToString().ToLowerInvariant()}]{drift}";
    }

    private static void AppendUnifiedDiff(StringBuilder sb, string before, string after)
    {
        // Minimal line-by-line diff: print removed lines as '-' and added lines as '+'.
        // For Plan 2 a precise unified-diff is overkill; this conveys intent and keeps the
        // renderer self-contained (no external diff library).
        var beforeLines = before.Split('\n');
        var afterLines = after.Split('\n');

        var common = LongestCommonPrefixCount(beforeLines, afterLines);
        var suffix = LongestCommonSuffixCount(beforeLines, afterLines, common);

        for (var i = common; i < beforeLines.Length - suffix; i++)
        {
            sb.Append("        - ").AppendLine(beforeLines[i]);
        }

        for (var i = common; i < afterLines.Length - suffix; i++)
        {
            sb.Append("        + ").AppendLine(afterLines[i]);
        }
    }

    private static int LongestCommonPrefixCount(string[] a, string[] b)
    {
        var max = Math.Min(a.Length, b.Length);
        for (var i = 0; i < max; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return i;
            }
        }

        return max;
    }

    private static int LongestCommonSuffixCount(string[] a, string[] b, int alreadyMatchedPrefix)
    {
        var max = Math.Min(a.Length, b.Length) - alreadyMatchedPrefix;
        var count = 0;
        while (count < max
               && string.Equals(a[^(count + 1)], b[^(count + 1)], StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
```

- [ ] **Step 4: Run tests (4 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Planning/PlanRenderer.cs tests/AspireForm.Tests/Planning/PlanRendererTests.cs
git commit -m "feat: add PlanRenderer producing human-readable plan output"
```

---

## Task 12: PlanCommand

Wires the planner into the Spectre CLI as `aspireform plan`. Loads config + state, calls the planner, renders to stdout.

**Files:**
- Create: `src/AspireForm/Cli/PlanCommand.cs`
- Modify: `src/AspireForm/Program.cs` (register the command)
- Test: `tests/AspireForm.Tests/Cli/PlanCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PlanCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plan-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunPlan(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(c => c.AddCommand<PlanCommand>("plan"));
            return (app.Run(["plan", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plan_against_sample_config_renders_create_actions()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: SampleApp
              apphost: ./SampleApp.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
                databases: [appdb]
            modules:
              data:
                type: ef-data
                dependsOn: [sql]
                database: appdb
                contextName: AppDbContext
            """);

        var (exitCode, stdout, _) = RunPlan("--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("+ sql").And.Contain("+ data");
        stdout.Should().Contain("CREATE");
        stdout.Should().Contain("aspire add sqlserver");
    }

    [Fact]
    public void Plan_exits_nonzero_with_an_error_when_no_config_exists()
    {
        var (exitCode, _, stderr) = RunPlan("--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("No AspireForm configuration");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/PlanCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plan</c> command: shows the reconciliation diff. Pure; no side effects.</summary>
public sealed class PlanCommand : Command<PlanCommand.Settings>
{
    /// <summary>Options for the <c>plan</c> command.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory containing the AspireForm configuration.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory containing the AspireForm configuration.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Environment whose override file is layered over the base config.</summary>
        [CommandOption("-e|--env <ENV>")]
        [Description("Environment whose override file (aspireform.<env>.*) is layered over the base.")]
        public string? Env { get; init; }
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, settings.Env);
            var state = new StateStore().Load(projectDir);
            var plan = new Planner(ProviderRegistry.Default()).Plan(loaded.Model, state, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (DependencyCycleException ex)
        {
            Console.Error.WriteLine($"Plan error: {ex.Message}");
            return 1;
        }
        catch (ProviderNotFoundException ex)
        {
            Console.Error.WriteLine($"Plan error: {ex.Message}");
            return 1;
        }
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs` — register `plan`**

After the `doctor` registration line, add:

```csharp
    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show the reconciliation diff between desired and current state.");
```

Full updated `Program.cs`:

```csharp
using AspireForm.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("aspireform");

    config.AddCommand<ConfigCommand>("config")
        .WithAlias("show")
        .WithDescription("Print the fully merged and interpolated desired-state configuration.");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that AspireForm's prerequisites are installed.");

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show the reconciliation diff between desired and current state.");
});

return await app.RunAsync(args);
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/PlanCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/PlanCommandTests.cs
git commit -m "feat: add plan command wiring planner + renderer"
```

---

## Task 13: End-to-end `plan` smoke test

Mirrors the existing `CliSmokeTests` pattern: shells out to the real `dotnet run --project src/AspireForm -- plan` against `examples/sample` and asserts the rendered plan contains the expected blocks.

**Files:**
- Test: `tests/AspireForm.Tests/EndToEnd/PlanSmokeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>Runs the real AspireForm tool's plan verb against the sample fixture.</summary>
public sealed class PlanSmokeTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    private static (int ExitCode, string Output) RunTool(params string[] args)
    {
        var root = RepoRoot();
        var allArgs = new List<string>
        {
            "run",
            "--configuration", BuildConfiguration(),
            "--no-build",
            "--project", Path.Combine(root, "src", "AspireForm"),
            "--",
        };
        allArgs.AddRange(args);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        foreach (var arg in allArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void Plan_against_sample_fixture_shows_sql_and_data_blocks()
    {
        var sampleDir = Path.Combine(RepoRoot(), "examples", "sample");
        var (exitCode, output) = RunTool("plan", "--project-dir", sampleDir);

        exitCode.Should().Be(0);
        output.Should().Contain("+ sql").And.Contain("+ data");
        output.Should().Contain("aspire add sqlserver");
    }
}
```

- [ ] **Step 2: Run tests (1 new test)**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests
```

Expected: PASS — total tests around 90+ (Plan 1 had 58; the 12 Plan 2 tasks add ~40+).

- [ ] **Step 3: Commit**

```bash
git add tests/AspireForm.Tests/EndToEnd/PlanSmokeTests.cs
git commit -m "test: add end-to-end smoke test for the plan command"
```

---

## Plan 2 — Definition of done

- `aspireform plan [--project-dir DIR] [--env ENV]` renders a human-readable diff: block headers (`+`/`~`/`-`), per-file actions with ownership-mode tags, per-block CLI invocations, and unified-diff snippets for changed content.
- Pure planner — no files written, no CLI invocations executed.
- Provider contract (`IProvider`, `ProviderPlan`, `PlannedFileAction`, `PlannedCliAction`, `PlanContext`, `OwnershipMode`, `BlockKind`) and the v1 built-in providers (`sqlserver`, `ef-data`) all in place.
- Marker-region editor handles insert / upsert / remove / read of `// <aspireform:block=NAME>` regions.
- Dependency graph with topological sort and cycle detection.
- Drift detection via SHA-256 against state baselines.
- Three-way reconciliation: ProjectModel vs AspireFormState vs disk.
- xUnit v3 / MTP test suite green; CLI smoke tests pass (existing + new `PlanSmokeTests`).

---

## Self-review notes

- **Spec coverage:** §5 (Provider registry & contracts) — Tasks 1, 2, 4, 5. §6.1 (Block-level actions) — Task 10. §6.2 (Ownership modes) — Tasks 3, 4, 5, 9 (Managed via MarkerRegion; Scaffold via skip-when-present in Reconciler; Merge documented and goto-Managed for v1, full impl deferred to Plan 3). §6.3 (Drift detection) — Tasks 8, 9. §6.4 (`plan` is pure) — Tasks 10, 11, 12. §9 `plan` row — Task 12. §10 (`IAspireCli` adapter) — not expanded in Plan 2 (already minimal from Plan 1; Plan 3 expands).
- **Scope deviations called out in the plan body:**
  - The `ef-data` Module v1 is intentionally minimal (DbContext scaffold + managed-region comment) because the reference sample has no service project. The MODULE concept is exercised; full DI/migration wiring waits.
  - Merge ownership mode is documented but not implemented in Plan 2; the Reconciler falls through to the Managed path (which still uses MarkerRegion). Plan 3 (or the verticals catalog) supplies the actual 3-way merge.
  - The PlanRenderer uses a minimal line-by-line diff (longest common prefix + suffix) rather than a full Myers diff to avoid an external dependency. Adequate for v1; can be upgraded later.
- **Placeholder scan:** none. Every step has concrete code or commands. Each task can be executed without further design decisions.
- **Type / name consistency:** `IProvider`, `BlockKind`, `OwnershipMode`, `PlannedFileAction(Path, OwnershipMode, BlockMarker, RenderContent)`, `PlannedCliAction(Tool, Args)`, `ProviderPlan { FileActions, CliActions }`, `PlanContext(BlockName, Inputs, AppHostDirectory, ProjectName)`, `BlockAction(BlockName, BlockKind, Kind, FileActions) { CliActions }`, `BlockActionKind` (Create/Update/Delete/Noop), `FileActionKind` (Create/Modify/Skip/Remove/DriftBlocked), `Plan { Blocks, HasChanges }`, `BlockReconcileResult(FileActions, CliActions)`, `Planner(ProviderRegistry).Plan(model, state, projectDir)`, `Reconciler.Reconcile(blockName, blockKind, blockKindAction, providerPlan, previousState, projectDir)`, `PlanRenderer.Render(plan)`, `MarkerRegion.UpsertBeforeAnchor / Remove / TryReadInner`, `DependencyGraph.TopologicallySort(edges)`, `DriftDetector.ComputeChecksum / IsAbsent / HasDrifted` — names match across every task that references them.

---

## Release note (not a plan task — for after Plan 3)

Plan 2 does not by itself warrant a NuGet release: the user can see a plan but cannot apply it. Bundle the next release (0.2.0) with Plan 3's completion, when `apply`/`destroy` join `plan` and the tool can actually evolve a project.
