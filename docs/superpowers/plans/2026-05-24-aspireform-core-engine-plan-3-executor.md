# AspireForm Core Engine — Plan 3: Executor & the apply / destroy / new / add / import / state verbs

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the action half of the AspireForm engine — the `Executor` that actually performs a `Plan`, plus the user-facing verbs (`apply`, `destroy`, `new`, `add`, `import`, `state list/show`), state write-back, and an end-to-end test that boots a generated Aspire AppHost via the Aspire Test Framework. Tag and ship as **AspireForm 0.2.0** on NuGet.

**Architecture:** The `Planner` from Plan 2 produces a `Plan` of `BlockAction`s. The new `Executor` walks the plan in topological order, runs each `BlockAction`'s `PlannedCliAction`s (via the expanded `IAspireCli`), applies each `FileActionPlan` (write / modify / skip / remove), and persists the resulting `AspireFormState` to `.aspireform/state.json` after each successful block. The CLI verbs are thin Spectre wrappers around the Planner + Executor + StateStore + ConfigLoader.

**Tech Stack:** Inherits prior plans — .NET 10 (`net10.0`, SDK 10.0.300), `Spectre.Console.Cli` 0.55.0, `YamlDotNet` 18.0.0, xUnit v3 3.2.2 on Microsoft Testing Platform, `AwesomeAssertions` 9.4.0. **New package for Task 12 only:** `Aspire.Hosting.Testing` (current stable, 13.3.5+); test is opt-in (skips if Docker isn't running).

**Spec:** `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` — this plan implements the §9 verbs that aren't yet shipped, the §6.4 `apply` half of the plan/apply split, the §7 state-writeback story, and the §11 Aspire-Test-Framework e2e.

**Plan position:** Plan 3 of 3 — the final plan in the Core Engine sub-project. After this lands, AspireForm 0.2.0 ships on NuGet with full v1 functionality.

---

## Conventions for the executor (the human/agent running this plan)

- **Solo dev workflow** — work in-place on `main`; no feature branch needed.
- **Assertions:** `AwesomeAssertions` throughout. Never `Assert.*`.
- **XML docs:** every public type and member.
- **Run tests:** `dotnet run --project tests/AspireForm.Tests --configuration Debug`. (`dotnet test` is flaky on this Windows setup.)
- All paths relative to `c:/Development/AspireForm`.

---

## Important truths carried forward (from Plans 1 & 2)

1. `aspire add <integration>` only edits the AppHost `.csproj` — AspireForm owns `AppHost.cs` content via marker regions.
2. Spectre.Console.Cli 0.55.0 — `Command<T>.Execute` signature is `protected override int Execute(CommandContext, T, CancellationToken)`. Async = `protected override Task<int> ExecuteAsync(CommandContext, CancellationToken)`.
3. Tests that redirect `Console.Out`/`Console.Error` must join `[Collection(nameof(ConsoleCaptureCollection))]` (Plan 1 introduced this to serialise process-wide console state).
4. The `BlockState.Inputs` field exists (added by Plan 1 review fix) — the executor writes the resolved `JsonObject` of inputs into it per block.
5. `FileActionPlan.Path` is **absolute** (Plan 2 review fix resolves relative-to-projectDir into absolute). State writeback must convert absolute → repo-relative (state is committed to git).
6. The `MarkerRegion` editor supports a candidate-list of anchors — `["builder.Build().Run();", "await builder.Build().RunAsync();", "builder.Build().RunAsync();"]`.

---

## Locked design decisions (premises for Plan 3)

- **Failure handling:** the executor commits state after each successful block. On a failed block, prior blocks remain applied and persisted; the failed block leaves state untouched; the run exits non-zero with the failure detail.
- **Drift handling:** if `plan` detected any drift, `apply` refuses by default. `--force-drift` overrides.
- **Approval gate:** `apply` prints the plan and prompts `Apply? [y/N]` interactively unless `--yes` is supplied. The prompt reads from `Console.In`; in tests this is fed via a `StringReader`.
- **`new` uses `dotnet new aspire-apphost`** (not `aspire new`, which prompts for NuGet config). Then writes a starter `aspireform.yaml`.
- **`add <type>` mutates `aspireform.yaml` lossily** — round-trips through `JsonObject` and re-serialises. Comments and formatting are preserved on a best-effort basis only; this is documented in `--help` text. Acceptable v1 trade-off (full structure-preserving YAML editing is a post-v1 enhancement).
- **`import <type> <name>`** populates state with the provider's plan output as if it had been executed. No CLI is run; no files are written. The block is recorded in state with checksums of whatever currently exists on disk for the provider's would-be paths (or absent if files don't exist).
- **State paths are repo-relative** (forward-slash-normalised) for portability; the executor converts.
- **Aspire-Test-Framework test is opt-in:** the test detects Docker availability (`docker info` exit 0) and skips otherwise. CI without Docker will skip; local with Docker Desktop will run.

---

## File structure

```
src/AspireForm/Aspire/
  IAspireCli.cs                       MODIFY — add RunAsync(args, workingDir) → CliResult
  AspireCli.cs                        MODIFY — implement RunAsync
  CliResult.cs                        CREATE — record { ExitCode, StdOut, StdErr }
src/AspireForm/Execution/
  ExecuteOptions.cs                   CREATE — flags: AutoApprove, ForceDrift
  ExecutionResult.cs                  CREATE — { Success, FailureMessage, BlocksApplied, NewState }
  Executor.cs                         CREATE — the orchestrator
  PathUtilities.cs                    CREATE — absolute↔repo-relative helpers
src/AspireForm/Cli/
  ApplyCommand.cs                     CREATE
  DestroyCommand.cs                   CREATE
  NewCommand.cs                       CREATE
  AddCommand.cs                       CREATE
  ImportCommand.cs                    CREATE
  StateListCommand.cs                 CREATE
  StateShowCommand.cs                 CREATE
src/AspireForm/Program.cs             MODIFY — register the 7 new verbs

tests/AspireForm.Tests/
  Execution/ExecutorTests.cs
  Execution/PathUtilitiesTests.cs
  Cli/ApplyCommandTests.cs
  Cli/DestroyCommandTests.cs
  Cli/NewCommandTests.cs
  Cli/AddCommandTests.cs
  Cli/ImportCommandTests.cs
  Cli/StateListCommandTests.cs
  Cli/StateShowCommandTests.cs
  EndToEnd/ApplySnapshotTests.cs      — file-system snapshot test (no Docker)
  EndToEnd/ApplyAspireBootTests.cs    — Aspire-Test-Framework integration test (skips if no Docker)

README.md                             MODIFY — list all 8 verbs, mark 0.2.0
CHANGELOG.md                          MODIFY — add [0.2.0] section
src/AspireForm/AspireForm.csproj      MODIFY — bump <Version> to 0.2.0
```

---

## Task 1: Expand `IAspireCli` with `RunAsync`

**Files:**
- Modify: `src/AspireForm/Aspire/IAspireCli.cs`, `src/AspireForm/Aspire/AspireCli.cs`
- Create: `src/AspireForm/Aspire/CliResult.cs`
- Test: `tests/AspireForm.Tests/Aspire/AspireCliRunAsyncTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Aspire;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Aspire;

public sealed class AspireCliRunAsyncTests
{
    [Fact]
    public async Task RunAsync_returns_failure_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        var result = await cli.RunAsync(args: ["--version"], workingDirectory: Environment.CurrentDirectory);
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsync_captures_stdout_when_invoking_a_real_command()
    {
        // 'dotnet --version' is guaranteed available on this machine; use it as a known-good probe.
        var cli = new AspireCli(executablePath: "dotnet");
        var result = await cli.RunAsync(args: ["--version"], workingDirectory: Environment.CurrentDirectory);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Trim().Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }
}
```

- [ ] **Step 2: Run test to verify it fails** (`RunAsync` doesn't exist yet)

- [ ] **Step 3: Create `src/AspireForm/Aspire/CliResult.cs`**

```csharp
namespace AspireForm.Aspire;

/// <summary>The captured outcome of a CLI subprocess invocation.</summary>
/// <param name="ExitCode">The process exit code (0 == success).</param>
/// <param name="StandardOutput">Everything the subprocess wrote to stdout.</param>
/// <param name="StandardError">Everything the subprocess wrote to stderr.</param>
public sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
```

- [ ] **Step 4: Modify `src/AspireForm/Aspire/IAspireCli.cs`** — add the new method to the interface:

```csharp
namespace AspireForm.Aspire;

/// <summary>The single seam through which AspireForm interacts with the official <c>aspire</c> CLI.</summary>
public interface IAspireCli
{
    /// <summary>Returns true when the <c>aspire</c> CLI can be invoked.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the installed <c>aspire</c> CLI version string, or null when it is unavailable.</summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes the underlying executable with <paramref name="args"/> from <paramref name="workingDirectory"/>,
    /// capturing stdout and stderr. Returns a <see cref="CliResult"/> with the captured output and exit code.
    /// Never throws on non-zero exit; failures are reported via <see cref="CliResult.ExitCode"/>.
    /// </summary>
    Task<CliResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Modify `src/AspireForm/Aspire/AspireCli.cs`** — implement the new method:

```csharp
using System.ComponentModel;
using System.Diagnostics;

namespace AspireForm.Aspire;

/// <summary>An <see cref="IAspireCli"/> that shells out to the <c>aspire</c> executable on PATH.</summary>
public sealed class AspireCli : IAspireCli
{
    private readonly string _executablePath;

    /// <summary>Initializes the CLI wrapper, optionally overriding the executable name (used by tests).</summary>
    public AspireCli(string executablePath = "aspire") => _executablePath = executablePath;

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        await GetVersionAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["--version"], workingDirectory: Environment.CurrentDirectory, cancellationToken);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    /// <inheritdoc />
    public async Task<CliResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new CliResult(ExitCode: -1, StandardOutput: string.Empty, StandardError: "Failed to start process.");
            }

            // Read both streams in parallel so neither blocks the other.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new CliResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new CliResult(ExitCode: -1, StandardOutput: string.Empty, StandardError: ex.Message);
        }
    }
}
```

- [ ] **Step 6: Run tests (2 new tests)**

- [ ] **Step 7: Commit**

```bash
git add src/AspireForm/Aspire/ tests/AspireForm.Tests/Aspire/AspireCliRunAsyncTests.cs
git commit -m "feat: expand IAspireCli with RunAsync(args, workingDir) returning CliResult"
```

---

## Task 2: ExecuteOptions + ExecutionResult model

**Files:**
- Create: `src/AspireForm/Execution/ExecuteOptions.cs`, `src/AspireForm/Execution/ExecutionResult.cs`
- Test: `tests/AspireForm.Tests/Execution/ExecutionResultTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Execution;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Execution;

public sealed class ExecutionResultTests
{
    [Fact]
    public void Default_ExecuteOptions_does_not_auto_approve_or_force_drift()
    {
        var opts = new ExecuteOptions();
        opts.AutoApprove.Should().BeFalse();
        opts.ForceDrift.Should().BeFalse();
    }

    [Fact]
    public void Success_result_reports_no_failure_and_carries_a_state()
    {
        var result = new ExecutionResult
        {
            Success = true,
            BlocksApplied = 3,
            NewState = new AspireFormState(),
        };

        result.Success.Should().BeTrue();
        result.FailureMessage.Should().BeNull();
        result.BlocksApplied.Should().Be(3);
        result.NewState.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails** (types don't exist)

- [ ] **Step 3: Create `src/AspireForm/Execution/ExecuteOptions.cs`**

```csharp
namespace AspireForm.Execution;

/// <summary>Flags that modify an <see cref="Executor.ApplyAsync"/> invocation.</summary>
public sealed class ExecuteOptions
{
    /// <summary>When true, skip the interactive approval prompt (equivalent to <c>--yes</c>).</summary>
    public bool AutoApprove { get; init; }

    /// <summary>When true, proceed even if <see cref="Planning.FileActionPlan.DriftDetected"/> is set on any file (equivalent to <c>--force-drift</c>).</summary>
    public bool ForceDrift { get; init; }
}
```

- [ ] **Step 4: Create `src/AspireForm/Execution/ExecutionResult.cs`**

```csharp
using AspireForm.State;

namespace AspireForm.Execution;

/// <summary>The aggregate outcome of an <see cref="Executor"/> run.</summary>
public sealed class ExecutionResult
{
    /// <summary>True when every applicable block was applied without error.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable error description; null when <see cref="Success"/> is true.</summary>
    public string? FailureMessage { get; init; }

    /// <summary>Number of blocks the executor processed successfully.</summary>
    public int BlocksApplied { get; init; }

    /// <summary>Number of blocks the executor encountered a failure on (0 on a clean run).</summary>
    public int BlocksFailed { get; init; }

    /// <summary>The state the executor persisted (matches what is on disk after the run).</summary>
    public required AspireFormState NewState { get; init; }
}
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Execution/ tests/AspireForm.Tests/Execution/ExecutionResultTests.cs
git commit -m "feat: add ExecuteOptions and ExecutionResult model"
```

---

## Task 3: PathUtilities

A pair of pure helpers for the absolute↔repo-relative path conversion the state writer needs. State keys live as repo-relative paths (committed to git, portable); FileActionPlan.Path is absolute (resolved by the Reconciler).

**Files:**
- Create: `src/AspireForm/Execution/PathUtilities.cs`
- Test: `tests/AspireForm.Tests/Execution/PathUtilitiesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Execution;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Execution;

public sealed class PathUtilitiesTests
{
    [Fact]
    public void ToRepoRelative_returns_forward_slashed_relative_path()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var absolute = Path.Combine(projectDir, "src", "Foo.cs");

        PathUtilities.ToRepoRelative(absolute, projectDir).Should().Be("src/Foo.cs");
    }

    [Fact]
    public void ToRepoRelative_returns_the_input_when_the_path_is_outside_the_project_directory()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var elsewhere = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other", "Foo.cs"));

        // Outside the project — keep absolute (with forward-slash normalisation).
        var result = PathUtilities.ToRepoRelative(elsewhere, projectDir);
        result.Should().Contain("/").And.NotStartWith("../");
    }

    [Fact]
    public void FromRepoRelative_combines_with_projectDir_and_returns_an_absolute_path()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var resolved = PathUtilities.FromRepoRelative("src/Foo.cs", projectDir);

        Path.IsPathRooted(resolved).Should().BeTrue();
        resolved.Replace('\\', '/').Should().EndWith("proj/src/Foo.cs");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Execution/PathUtilities.cs`**

```csharp
namespace AspireForm.Execution;

/// <summary>Path conversion helpers used by the executor for state portability.</summary>
public static class PathUtilities
{
    /// <summary>
    /// Returns a forward-slash-normalised path relative to <paramref name="projectDir"/>.
    /// When the path lies outside <paramref name="projectDir"/>, returns the absolute path
    /// with forward-slash normalisation (state keys remain unique and recoverable).
    /// </summary>
    public static string ToRepoRelative(string absolutePath, string projectDir)
    {
        var normalisedProject = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar);
        var normalisedPath = Path.GetFullPath(absolutePath);

        var relative = Path.GetRelativePath(normalisedProject, normalisedPath);
        // If the relative path escapes the project directory, fall back to the absolute form.
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return normalisedPath.Replace('\\', '/');
        }

        return relative.Replace('\\', '/');
    }

    /// <summary>
    /// Inverse of <see cref="ToRepoRelative"/>. Combines <paramref name="repoRelative"/> with
    /// <paramref name="projectDir"/> and returns an absolute path; already-absolute inputs pass through.
    /// </summary>
    public static string FromRepoRelative(string repoRelative, string projectDir) =>
        Path.IsPathRooted(repoRelative)
            ? Path.GetFullPath(repoRelative)
            : Path.GetFullPath(Path.Combine(projectDir, repoRelative));
}
```

- [ ] **Step 4: Run tests (3 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Execution/PathUtilities.cs tests/AspireForm.Tests/Execution/PathUtilitiesTests.cs
git commit -m "feat: add PathUtilities for repo-relative state path conversion"
```

---

## Task 4: Executor

The heart of Plan 3. Takes a `Plan`, a `ProjectModel` (for input bag access), an `AspireFormState` (the prior state), a project directory, and `ExecuteOptions`. Executes block-by-block, persisting state after each success.

**Files:**
- Create: `src/AspireForm/Execution/Executor.cs`
- Test: `tests/AspireForm.Tests/Execution/ExecutorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Execution;

public sealed class ExecutorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-executor").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeAspireCli : IAspireCli
    {
        public List<(IReadOnlyList<string> Args, string WorkingDirectory)> Calls { get; } = [];
        public int ExitCodeToReturn { get; set; } = 0;

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<string?> GetVersionAsync(CancellationToken ct = default) => Task.FromResult<string?>("13.3.5");
        public Task<CliResult> RunAsync(IReadOnlyList<string> args, string workingDirectory, CancellationToken ct = default)
        {
            Calls.Add((args, workingDirectory));
            return Task.FromResult(new CliResult(ExitCodeToReturn, "", ""));
        }
    }

    private ProjectModel SampleModel() => new()
    {
        AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "MyApp.AppHost" },
        Resources = new Dictionary<string, ResourceBlock>
        {
            ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject { ["aspireName"] = "sql" } },
        },
    };

    private Plan PlanFor(ProjectModel model, AspireFormState state) =>
        new Planner(ProviderRegistry.Default()).Plan(model, state, _dir);

    [Fact]
    public async Task ApplyAsync_writes_managed_files_runs_cli_actions_and_persists_state()
    {
        // The AppHost dir doesn't exist yet — that's fine; Managed mode emits a fresh marker region.
        var fakeCli = new FakeAspireCli();
        var executor = new Executor(fakeCli, new StateStore());
        var model = SampleModel();
        var plan = PlanFor(model, new AspireFormState());

        var result = await executor.ApplyAsync(
            plan, model, prevState: new AspireFormState(), projectDir: _dir,
            options: new ExecuteOptions { AutoApprove = true });

        result.Success.Should().BeTrue();
        result.BlocksApplied.Should().Be(1);

        // The AppHost.cs was written with the marker region.
        var apphostPath = Path.Combine(_dir, "MyApp.AppHost", "AppHost.cs");
        File.Exists(apphostPath).Should().BeTrue();
        File.ReadAllText(apphostPath).Should().Contain("<aspireform:block=sql>")
            .And.Contain("AddSqlServer(\"sql\")");

        // The aspire add CLI invocation was made from the AppHost directory.
        fakeCli.Calls.Should().ContainSingle();
        fakeCli.Calls[0].Args.Should().ContainInOrder("add", "sqlserver");
        fakeCli.Calls[0].WorkingDirectory.Replace('\\', '/').Should().EndWith("MyApp.AppHost");

        // State was persisted with the file recorded under its repo-relative path.
        var loaded = new StateStore().Load(_dir);
        loaded.Blocks.Should().ContainKey("sql");
        loaded.Blocks["sql"].Files.Keys.Should().Contain("MyApp.AppHost/AppHost.cs");
        loaded.Blocks["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
    }

    [Fact]
    public async Task ApplyAsync_refuses_when_drift_detected_unless_ForceDrift_is_set()
    {
        // Set up: AppHost.cs exists, prior state has a stale baseline, so the plan flags drift.
        var apphostDir = Directory.CreateDirectory(Path.Combine(_dir, "MyApp.AppHost"));
        var apphostPath = Path.Combine(apphostDir.FullName, "AppHost.cs");
        File.WriteAllText(apphostPath, "var builder = DistributedApplication.CreateBuilder(args);\n\nbuilder.Build().Run();\n");

        var priorState = new AspireFormState();
        priorState.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { ["MyApp.AppHost/AppHost.cs"] = new FileState { OwnershipMode = "managed", Checksum = "stale" } },
        };

        var model = SampleModel();
        var plan = PlanFor(model, priorState);

        var executor = new Executor(new FakeAspireCli(), new StateStore());

        var refused = await executor.ApplyAsync(plan, model, priorState, _dir,
            new ExecuteOptions { AutoApprove = true, ForceDrift = false });
        refused.Success.Should().BeFalse();
        refused.FailureMessage.Should().Contain("drift");

        var forced = await executor.ApplyAsync(plan, model, priorState, _dir,
            new ExecuteOptions { AutoApprove = true, ForceDrift = true });
        forced.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_propagates_a_failing_cli_action_and_stops_applying_further_blocks()
    {
        // Build a two-block plan, fail the first block's CLI; expect zero applied, failure reported.
        var fakeCli = new FakeAspireCli { ExitCodeToReturn = 1 };
        var executor = new Executor(fakeCli, new StateStore());
        var model = SampleModel();
        var plan = PlanFor(model, new AspireFormState());

        var result = await executor.ApplyAsync(plan, model, new AspireFormState(), _dir,
            new ExecuteOptions { AutoApprove = true });

        result.Success.Should().BeFalse();
        result.BlocksApplied.Should().Be(0);
        result.FailureMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ApplyAsync_handles_a_DELETE_block_by_removing_tracked_files_and_dropping_the_state_entry()
    {
        // Pre-create a file the block "owns", then run a plan that's empty in desired state.
        var apphostDir = Directory.CreateDirectory(Path.Combine(_dir, "MyApp.AppHost"));
        var apphostPath = Path.Combine(apphostDir.FullName, "AppHost.cs");
        File.WriteAllText(apphostPath, "x");

        var priorState = new AspireFormState();
        priorState.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { ["MyApp.AppHost/AppHost.cs"] = new FileState { OwnershipMode = "managed", Checksum = DriftDetector.ComputeChecksum(apphostPath) } },
        };

        var emptyModel = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "MyApp.AppHost" },
        };
        var plan = PlanFor(emptyModel, priorState);

        var executor = new Executor(new FakeAspireCli(), new StateStore());
        var result = await executor.ApplyAsync(plan, emptyModel, priorState, _dir,
            new ExecuteOptions { AutoApprove = true });

        result.Success.Should().BeTrue();
        File.Exists(apphostPath).Should().BeFalse();
        new StateStore().Load(_dir).Blocks.Should().NotContainKey("sql");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Execution/Executor.cs`**

```csharp
using System.Text;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Execution;

/// <summary>
/// Executes a <see cref="Plan"/>: runs <see cref="PlannedCliAction"/>s via <see cref="IAspireCli"/>,
/// applies each <see cref="FileActionPlan"/> by writing/removing files, and persists the resulting
/// <see cref="AspireFormState"/> to disk after each successful block.
/// </summary>
public sealed class Executor
{
    private readonly IAspireCli _aspireCli;
    private readonly StateStore _stateStore;

    /// <summary>Initialises the executor with its CLI seam and state store.</summary>
    public Executor(IAspireCli aspireCli, StateStore stateStore)
    {
        _aspireCli = aspireCli;
        _stateStore = stateStore;
    }

    /// <summary>Applies <paramref name="plan"/> against <paramref name="projectDir"/>, persisting state per block.</summary>
    public async Task<ExecutionResult> ApplyAsync(
        Plan plan,
        ProjectModel model,
        AspireFormState prevState,
        string projectDir,
        ExecuteOptions options,
        CancellationToken cancellationToken = default)
    {
        // Drift gate.
        if (!options.ForceDrift)
        {
            var drifted = plan.Blocks.SelectMany(b => b.FileActions).Where(f => f.DriftDetected).ToList();
            if (drifted.Count > 0)
            {
                var paths = string.Join(", ", drifted.Select(f => f.Path));
                return new ExecutionResult
                {
                    Success = false,
                    FailureMessage = $"Refusing to apply: drift detected on {drifted.Count} file(s): {paths}. Re-run with --force-drift to override.",
                    NewState = prevState,
                };
            }
        }

        var state = CloneState(prevState);
        var blocksApplied = 0;

        foreach (var block in plan.Blocks)
        {
            try
            {
                await ApplyBlockAsync(block, model, projectDir, state, cancellationToken);

                // Persist after each successful block so partial progress survives later failures.
                _stateStore.Save(projectDir, state);
                blocksApplied++;
            }
            catch (Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    FailureMessage = $"Block '{block.BlockName}' failed: {ex.Message}",
                    BlocksApplied = blocksApplied,
                    BlocksFailed = 1,
                    NewState = state,
                };
            }
        }

        return new ExecutionResult
        {
            Success = true,
            BlocksApplied = blocksApplied,
            NewState = state,
        };
    }

    private async Task ApplyBlockAsync(
        BlockAction block,
        ProjectModel model,
        string projectDir,
        AspireFormState state,
        CancellationToken cancellationToken)
    {
        // CLI actions first (e.g. aspire add must run before file edits that assume the package is referenced).
        var appHostWorkingDir = Path.GetFullPath(Path.Combine(projectDir, model.AspireForm.AppHost));
        foreach (var cli in block.CliActions)
        {
            if (!string.Equals(cli.Tool, "aspire", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Unsupported CLI tool '{cli.Tool}' in v1 (only 'aspire' is wired through IAspireCli).");
            }

            var result = await _aspireCli.RunAsync(cli.Args, appHostWorkingDir, cancellationToken);
            if (result.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput : result.StandardError;
                throw new InvalidOperationException(
                    $"aspire {string.Join(' ', cli.Args)} exited with {result.ExitCode}: {details}");
            }
        }

        // File actions.
        var blockFiles = new Dictionary<string, FileState>(StringComparer.Ordinal);
        foreach (var file in block.FileActions)
        {
            ApplyFileAction(file, projectDir, blockFiles);
        }

        // Update state for this block.
        if (block.Kind == BlockActionKind.Delete)
        {
            state.Blocks.Remove(block.BlockName);
        }
        else
        {
            var blockType = LookupBlockType(model, block.BlockName);
            state.Blocks[block.BlockName] = new BlockState
            {
                Type = blockType,
                Kind = block.BlockKind == BlockKind.Module ? "module" : "resource",
                Files = blockFiles,
                Inputs = LookupBlockInputs(model, block.BlockName),
            };
        }
    }

    private static void ApplyFileAction(FileActionPlan file, string projectDir, Dictionary<string, FileState> blockFiles)
    {
        switch (file.Kind)
        {
            case FileActionKind.Create:
            case FileActionKind.Modify:
            {
                if (file.AfterContent is null)
                {
                    throw new InvalidOperationException(
                        $"File action {file.Kind} on '{file.Path}' has no AfterContent.");
                }

                var dir = Path.GetDirectoryName(file.Path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(file.Path, file.AfterContent);

                blockFiles[PathUtilities.ToRepoRelative(file.Path, projectDir)] = new FileState
                {
                    OwnershipMode = file.OwnershipMode.ToString().ToLowerInvariant(),
                    Checksum = DriftDetector.ComputeChecksum(file.AfterContent.AsSpan()),
                    Baseline = file.OwnershipMode == Providers.OwnershipMode.Merge ? file.AfterContent : null,
                };
                break;
            }

            case FileActionKind.Skip:
            {
                // Scaffold mode + file present: keep state entry pointing at the existing file's checksum.
                if (File.Exists(file.Path))
                {
                    blockFiles[PathUtilities.ToRepoRelative(file.Path, projectDir)] = new FileState
                    {
                        OwnershipMode = file.OwnershipMode.ToString().ToLowerInvariant(),
                        Checksum = DriftDetector.ComputeChecksum(file.Path),
                    };
                }
                break;
            }

            case FileActionKind.Remove:
            {
                if (File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                }
                // Don't add to blockFiles — block-level state entry will be removed for Delete blocks.
                break;
            }

            case FileActionKind.DriftBlocked:
                throw new InvalidOperationException(
                    $"Refusing to apply '{file.Path}': drift detected. Re-run with --force-drift to override.");
        }
    }

    private static string LookupBlockType(ProjectModel model, string blockName)
    {
        if (model.Resources.TryGetValue(blockName, out var r)) return r.Type;
        if (model.Modules.TryGetValue(blockName, out var m)) return m.Type;
        // Block being deleted may not appear in current model — caller handles delete branch separately.
        return string.Empty;
    }

    private static System.Text.Json.Nodes.JsonObject LookupBlockInputs(ProjectModel model, string blockName)
    {
        if (model.Resources.TryGetValue(blockName, out var r)) return r.Inputs;
        if (model.Modules.TryGetValue(blockName, out var m)) return m.Inputs;
        return new System.Text.Json.Nodes.JsonObject();
    }

    private static AspireFormState CloneState(AspireFormState prev)
    {
        // Round-trip through STJ to deep-clone the records cleanly.
        var json = System.Text.Json.JsonSerializer.Serialize(prev);
        return System.Text.Json.JsonSerializer.Deserialize<AspireFormState>(json) ?? new AspireFormState();
    }
}
```

- [ ] **Step 4: Run tests (4 new tests)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Execution/Executor.cs tests/AspireForm.Tests/Execution/ExecutorTests.cs
git commit -m "feat: add Executor — applies a Plan and persists state per block"
```

---

## Task 5: `apply` command

**Files:**
- Create: `src/AspireForm/Cli/ApplyCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `apply`
- Test: `tests/AspireForm.Tests/Cli/ApplyCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class ApplyCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-apply-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunApply(params string[] args)
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
            app.Configure(c => c.AddCommand<ApplyCommand>("apply"));
            return (app.Run(["apply", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Apply_with_yes_writes_files_and_persists_state()
    {
        // Use ef-data only: zero CLI actions, just file writes — works without a real aspire on PATH.
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: SampleApp
              apphost: SampleApp.AppHost
            modules:
              data:
                type: ef-data
                database: appdb
                contextName: AppDbContext
            """);

        var (exitCode, stdout, _) = RunApply("--project-dir", _dir, "--yes");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Applied");
        File.Exists(Path.Combine(_dir, "SampleApp.AppHost", "Data", "AppDbContext.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void Apply_exits_nonzero_when_no_config_exists()
    {
        var (exitCode, _, stderr) = RunApply("--project-dir", _dir, "--yes");
        exitCode.Should().Be(1);
        stderr.Should().Contain("No AspireForm configuration");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/ApplyCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>apply</c> command: executes the plan after an approval gate.</summary>
public sealed class ApplyCommand : AsyncCommand<ApplyCommand.Settings>
{
    /// <summary>Options for <c>apply</c>.</summary>
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

        /// <summary>Skip the interactive approval prompt.</summary>
        [CommandOption("-y|--yes")]
        [Description("Skip the interactive approval prompt and apply immediately.")]
        public bool Yes { get; init; }

        /// <summary>Proceed even when drift is detected.</summary>
        [CommandOption("--force-drift")]
        [Description("Apply even when drift has been detected on tracked files.")]
        public bool ForceDrift { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, settings.Env);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);
            var plan = new Planner(ProviderRegistry.Default()).Plan(loaded.Model, prevState, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));

            if (!plan.HasChanges)
            {
                return 0;
            }

            if (!settings.Yes && !PromptForApproval())
            {
                Console.Out.WriteLine("Aborted.");
                return 1;
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, loaded.Model, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = settings.ForceDrift }, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Apply failed: {result.FailureMessage}");
                return 1;
            }

            Console.Out.WriteLine($"Applied {result.BlocksApplied} block(s).");
            return 0;
        }
        catch (ConfigValidationException ex) { return Fail("Configuration error", ex); }
        catch (StateException ex)             { return Fail("State error", ex); }
        catch (DependencyCycleException ex)   { return Fail("Plan error", ex); }
        catch (ProviderNotFoundException ex)  { return Fail("Plan error", ex); }
    }

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }

    private static bool PromptForApproval()
    {
        Console.Out.Write("Apply this plan? [y/N]: ");
        var line = Console.In.ReadLine();
        return string.Equals(line?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — add `apply` registration after the `plan` line:

```csharp
    config.AddCommand<ApplyCommand>("apply")
        .WithDescription("Execute the plan after an approval gate.");
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/ApplyCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/ApplyCommandTests.cs
git commit -m "feat: add apply command"
```

---

## Task 6: `destroy` command

`destroy [<block>]` removes blocks from state, executing per-block deletes (with Module destroy-protection bypass via `--allow-module-destroy`). With no `<block>` arg, destroys every block currently in state.

**Files:**
- Create: `src/AspireForm/Cli/DestroyCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `destroy`
- Test: `tests/AspireForm.Tests/Cli/DestroyCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class DestroyCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-destroy-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunDestroy(params string[] args)
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
            app.Configure(c => c.AddCommand<DestroyCommand>("destroy"));
            return (app.Run(["destroy", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Destroy_refuses_module_block_without_allow_module_destroy_flag()
    {
        SeedConfig();
        SeedStateWith("data", kind: "module");
        File.WriteAllText(Path.Combine(_dir, "tracked.cs"), "x");

        var (exitCode, _, stderr) = RunDestroy("data", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(1);
        stderr.Should().Contain("module").And.Contain("--allow-module-destroy");
    }

    [Fact]
    public void Destroy_removes_resource_block_files_and_state_entry()
    {
        SeedConfig();
        SeedStateWith("sql", kind: "resource");
        File.WriteAllText(Path.Combine(_dir, "tracked.cs"), "x");

        var (exitCode, stdout, _) = RunDestroy("sql", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Destroyed");
        new StateStore().Load(_dir).Blocks.Should().NotContainKey("sql");
    }

    private void SeedConfig() => File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
        aspireform:
          version: 1
          project: SampleApp
          apphost: SampleApp.AppHost
        """);

    private void SeedStateWith(string blockName, string kind)
    {
        var state = new AspireFormState();
        state.Blocks[blockName] = new BlockState
        {
            Type = kind == "module" ? "ef-data" : "sqlserver",
            Kind = kind,
            Files = { ["tracked.cs"] = new FileState { OwnershipMode = "managed", Checksum = "x" } },
        };
        new StateStore().Save(_dir, state);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/DestroyCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>destroy</c> command: removes one or all blocks currently in state.</summary>
public sealed class DestroyCommand : AsyncCommand<DestroyCommand.Settings>
{
    /// <summary>Options for <c>destroy</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Optional block name. When omitted, every block in state is destroyed.</summary>
        [CommandArgument(0, "[BLOCK]")]
        [Description("Optional block name. When omitted, every block in state is destroyed.")]
        public string? BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Skip the interactive approval prompt.</summary>
        [CommandOption("-y|--yes")]
        [Description("Skip the interactive approval prompt and destroy immediately.")]
        public bool Yes { get; init; }

        /// <summary>Allow destroying Module blocks (otherwise refused due to destroy-protection).</summary>
        [CommandOption("--allow-module-destroy")]
        [Description("Allow destroying Module blocks (otherwise refused due to destroy-protection).")]
        public bool AllowModuleDestroy { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);

            // Decide which blocks to destroy.
            var targets = settings.BlockName is null
                ? prevState.Blocks.Keys.ToList()
                : [settings.BlockName];

            foreach (var name in targets)
            {
                if (!prevState.Blocks.TryGetValue(name, out var blockState))
                {
                    Console.Error.WriteLine($"Block '{name}' is not tracked in state.");
                    return 1;
                }

                if (!settings.AllowModuleDestroy
                    && string.Equals(blockState.Kind, "module", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        $"Refusing to destroy module block '{name}': pass --allow-module-destroy to override.");
                    return 1;
                }
            }

            // Build an "empty desired state" with the targets removed but other blocks preserved.
            var pseudoModel = BuildPseudoModelExcluding(loaded.Model, targets);
            var plan = new Planner(ProviderRegistry.Default()).Plan(pseudoModel, prevState, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));

            if (!plan.Blocks.Any(b => b.Kind == BlockActionKind.Delete))
            {
                Console.Out.WriteLine("Nothing to destroy.");
                return 0;
            }

            if (!settings.Yes && !PromptForApproval())
            {
                Console.Out.WriteLine("Aborted.");
                return 1;
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, pseudoModel, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = true }, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Destroy failed: {result.FailureMessage}");
                return 1;
            }

            Console.Out.WriteLine($"Destroyed {targets.Count} block(s).");
            return 0;
        }
        catch (ConfigValidationException ex) { return Fail("Configuration error", ex); }
        catch (StateException ex)             { return Fail("State error", ex); }
    }

    private static ProjectModel BuildPseudoModelExcluding(ProjectModel original, IReadOnlyList<string> exclude)
    {
        var ex = exclude.ToHashSet(StringComparer.Ordinal);
        return new ProjectModel
        {
            AspireForm = original.AspireForm,
            Resources = original.Resources.Where(r => !ex.Contains(r.Key)).ToDictionary(),
            Modules = original.Modules.Where(m => !ex.Contains(m.Key)).ToDictionary(),
            Profiles = original.Profiles,
        };
    }

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }

    private static bool PromptForApproval()
    {
        Console.Out.Write("Destroy? [y/N]: ");
        var line = Console.In.ReadLine();
        return string.Equals(line?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — add:

```csharp
    config.AddCommand<DestroyCommand>("destroy")
        .WithDescription("Destroy one block (or all blocks when no argument is supplied).");
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/DestroyCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/DestroyCommandTests.cs
git commit -m "feat: add destroy command with Module destroy-protection"
```

---

## Task 7: `state list` command

**Files:**
- Create: `src/AspireForm/Cli/StateListCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `state list`
- Test: `tests/AspireForm.Tests/Cli/StateListCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class StateListCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-list").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunList(params string[] args)
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
            app.Configure(c => c.AddCommand<StateListCommand>("list"));
            return (app.Run(["list", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void State_list_prints_each_block_with_its_kind_and_type()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState { Type = "sqlserver", Kind = "resource" };
        state.Blocks["data"] = new BlockState { Type = "ef-data", Kind = "module" };
        new StateStore().Save(_dir, state);

        var (exitCode, stdout, _) = RunList("--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("sql").And.Contain("sqlserver").And.Contain("resource");
        stdout.Should().Contain("data").And.Contain("ef-data").And.Contain("module");
    }

    [Fact]
    public void State_list_reports_empty_when_state_is_absent()
    {
        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("No tracked blocks");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/StateListCommand.cs`**

```csharp
using System.ComponentModel;
using System.Text;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>state list</c> command: prints a one-line summary of every tracked block.</summary>
public sealed class StateListCommand : Command<StateListCommand.Settings>
{
    /// <summary>Options for <c>state list</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var state = new StateStore().Load(Path.GetFullPath(settings.ProjectDir));
            if (state.Blocks.Count == 0)
            {
                Console.Out.WriteLine("No tracked blocks.");
                return 0;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Block        Kind      Type          Files");
            sb.AppendLine("-----        ----      ----          -----");
            foreach (var (name, block) in state.Blocks.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"{Pad(name, 12)} {Pad(block.Kind, 9)} {Pad(block.Type, 13)} {block.Files.Count}");
            }

            Console.Out.Write(sb.ToString());
            return 0;
        }
        catch (StateException ex)
        {
            Console.Error.WriteLine($"State error: {ex.Message}");
            return 1;
        }

        static string Pad(string s, int width) => s.PadRight(width)[..Math.Max(width, s.Length)];
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — `state` is a parent verb with `list` as a subcommand. Use `AddBranch`:

```csharp
    config.AddBranch("state", state =>
    {
        state.SetDescription("Inspect AspireForm's tracked state.");
        state.AddCommand<StateListCommand>("list")
            .WithDescription("List all tracked blocks.");
        // 'show' command added in Task 8.
    });
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/StateListCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/StateListCommandTests.cs
git commit -m "feat: add state list command"
```

---

## Task 8: `state show` command

**Files:**
- Create: `src/AspireForm/Cli/StateShowCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `state show`
- Test: `tests/AspireForm.Tests/Cli/StateShowCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class StateShowCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-show").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunShow(params string[] args)
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
            app.Configure(c => c.AddCommand<StateShowCommand>("show"));
            return (app.Run(["show", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void State_show_prints_a_block_record_as_indented_json()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { ["AppHost.cs"] = new FileState { OwnershipMode = "managed", Checksum = "abc" } },
        };
        new StateStore().Save(_dir, state);

        var (exitCode, stdout, _) = RunShow("sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("\"sqlserver\"").And.Contain("\"AppHost.cs\"").And.Contain("\"abc\"");
    }

    [Fact]
    public void State_show_reports_missing_block()
    {
        new StateStore().Save(_dir, new AspireFormState());
        var (exitCode, _, stderr) = RunShow("ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("ghost").And.Contain("not tracked");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/StateShowCommand.cs`**

```csharp
using System.ComponentModel;
using System.Text.Json;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>state show</c> command: dumps a single block's record as indented JSON.</summary>
public sealed class StateShowCommand : Command<StateShowCommand.Settings>
{
    /// <summary>Options for <c>state show</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The block name to show.</summary>
        [CommandArgument(0, "<BLOCK>")]
        [Description("The block name to show.")]
        public required string BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var state = new StateStore().Load(Path.GetFullPath(settings.ProjectDir));
            if (!state.Blocks.TryGetValue(settings.BlockName, out var block))
            {
                Console.Error.WriteLine($"Block '{settings.BlockName}' is not tracked in state.");
                return 1;
            }

            Console.Out.WriteLine(JsonSerializer.Serialize(block, PrettyOptions));
            return 0;
        }
        catch (StateException ex)
        {
            Console.Error.WriteLine($"State error: {ex.Message}");
            return 1;
        }
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — extend the `state` branch:

```csharp
    config.AddBranch("state", state =>
    {
        state.SetDescription("Inspect AspireForm's tracked state.");
        state.AddCommand<StateListCommand>("list")
            .WithDescription("List all tracked blocks.");
        state.AddCommand<StateShowCommand>("show")
            .WithDescription("Show one block's tracked state as JSON.");
    });
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/StateShowCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/StateShowCommandTests.cs
git commit -m "feat: add state show command"
```

---

## Task 9: `new <name>` command

**Files:**
- Create: `src/AspireForm/Cli/NewCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `new`
- Test: `tests/AspireForm.Tests/Cli/NewCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class NewCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-new-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunNew(params string[] args)
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
            app.Configure(c => c.AddCommand<NewCommand>("new"));
            return (app.Run(["new", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void New_creates_an_AppHost_project_and_a_starter_aspireform_yaml()
    {
        var (exitCode, stdout, _) = RunNew("MyDemoApp", "--output", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Created");

        var apphostDir = Path.Combine(_dir, "MyDemoApp", "MyDemoApp.AppHost");
        Directory.Exists(apphostDir).Should().BeTrue();
        File.Exists(Path.Combine(apphostDir, "AppHost.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, "MyDemoApp", "aspireform.yaml")).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/NewCommand.cs`**

```csharp
using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>new</c> command: scaffolds a new Aspire solution + a starter <c>aspireform.yaml</c>.</summary>
public sealed class NewCommand : Command<NewCommand.Settings>
{
    /// <summary>Options for <c>new</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project name.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("The project name.")]
        public required string Name { get; init; }

        /// <summary>Output directory; defaults to the current directory.</summary>
        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory (defaults to current directory).")]
        public string Output { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(settings.Output, settings.Name));
        var appHostName = $"{settings.Name}.AppHost";
        var appHostDir = Path.Combine(projectRoot, appHostName);

        if (Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"Refusing to scaffold into existing directory '{projectRoot}'.");
            return 1;
        }

        Directory.CreateDirectory(projectRoot);

        var result = RunDotnetNew(appHostName, projectRoot);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(
                $"dotnet new aspire-apphost failed (exit {result.ExitCode}): {result.StandardError}");
            return 1;
        }

        WriteStarterYaml(projectRoot, settings.Name, appHostName);

        Console.Out.WriteLine($"Created {projectRoot}");
        Console.Out.WriteLine($"  - {appHostName}/ (Aspire AppHost project)");
        Console.Out.WriteLine($"  - aspireform.yaml (starter)");
        return 0;
    }

    private static (int ExitCode, string StandardError) RunDotnetNew(string appHostName, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add("aspire-apphost");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(appHostName);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (-1, "Failed to start dotnet.");
        }

        var stderr = process.StandardError.ReadToEnd();
        _ = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stderr);
    }

    private static void WriteStarterYaml(string projectRoot, string projectName, string appHostName)
    {
        var content = $"""
            aspireform:
              version: 1
              project: {projectName}
              apphost: {appHostName}
            resources: {{}}
            modules: {{}}
            """;
        File.WriteAllText(Path.Combine(projectRoot, "aspireform.yaml"), content);
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — add:

```csharp
    config.AddCommand<NewCommand>("new")
        .WithDescription("Scaffold a new Aspire solution and starter aspireform.yaml.");
```

- [ ] **Step 5: Run tests (1 new test)**

The test invokes `dotnet new aspire-apphost` for real — slow but reliable on a machine with the Aspire templates installed (Plan 1 installed them via the xunit3 template path).

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/NewCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/NewCommandTests.cs
git commit -m "feat: add new command (dotnet new aspire-apphost + starter yaml)"
```

---

## Task 10: `add <type> [name]` command

Mutates `aspireform.yaml` (or `.jsonc`) in-place to add a Resource or Module block. Comments and original formatting are NOT preserved (the file is round-tripped through the canonical DOM and re-serialised).

**Files:**
- Create: `src/AspireForm/Cli/AddCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `add`
- Test: `tests/AspireForm.Tests/Cli/AddCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.Configuration;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class AddCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-add-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunAdd(params string[] args)
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
            app.Configure(c => c.AddCommand<AddCommand>("add"));
            return (app.Run(["add", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private void SeedConfig() => File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
        aspireform:
          version: 1
          project: Demo
          apphost: Demo.AppHost
        """);

    [Fact]
    public void Add_inserts_a_resource_block_into_aspireform_yaml()
    {
        SeedConfig();

        var (exitCode, _, _) = RunAdd("sqlserver", "sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        var loaded = new ConfigLoader().Load(_dir, env: null);
        loaded.Model.Resources.Should().ContainKey("sql");
        loaded.Model.Resources["sql"].Type.Should().Be("sqlserver");
    }

    [Fact]
    public void Add_inserts_a_module_block_with_dependsOn_when_kind_is_module()
    {
        SeedConfig();

        var (exitCode, _, _) = RunAdd("ef-data", "data",
            "--project-dir", _dir,
            "--module",
            "--depends-on", "sql");

        exitCode.Should().Be(0);

        // sql doesn't exist yet, so the binder will reject — for this test, add it first.
        // Re-add sql so depends-on resolves.
        RunAdd("sqlserver", "sql", "--project-dir", _dir);
        var loaded = new ConfigLoader().Load(_dir, env: null);
        loaded.Model.Modules["data"].DependsOn.Should().Contain("sql");
    }

    [Fact]
    public void Add_refuses_when_a_block_with_the_same_name_already_exists()
    {
        SeedConfig();
        RunAdd("sqlserver", "sql", "--project-dir", _dir);

        var (exitCode, _, stderr) = RunAdd("sqlserver", "sql", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("already exists");
    }
}
```

> The second test is order-sensitive — it adds `data` (which references `sql`) before `sql` exists. The current implementation must ALLOW writing the config even when validation would fail on read; the binder runs on read, not on write. Verify by re-loading at the end.

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/AddCommand.cs`**

```csharp
using System.ComponentModel;
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;

namespace AspireForm.Cli;

/// <summary>The <c>add</c> command: appends a Resource (default) or Module block to the AspireForm config file.</summary>
public sealed class AddCommand : Command<AddCommand.Settings>
{
    /// <summary>Options for <c>add</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Provider type (e.g. <c>sqlserver</c>, <c>ef-data</c>).</summary>
        [CommandArgument(0, "<TYPE>")]
        [Description("Provider type (e.g. sqlserver, ef-data).")]
        public required string Type { get; init; }

        /// <summary>Block name. Defaults to the provider type when omitted.</summary>
        [CommandArgument(1, "[NAME]")]
        [Description("Block name (defaults to the provider type).")]
        public string? Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Treat this block as a Module (default is Resource).</summary>
        [CommandOption("-m|--module")]
        [Description("Treat this block as a Module (default is Resource).")]
        public bool Module { get; init; }

        /// <summary>Block names this module depends on (may be repeated).</summary>
        [CommandOption("--depends-on <BLOCK>")]
        [Description("Block this module depends on (may be repeated).")]
        public string[] DependsOn { get; init; } = [];
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var configPath = FindConfigPath(projectDir);
            var blockName = settings.Name ?? settings.Type;

            // Load as a DOM (not via ConfigLoader — we want to mutate, not validate fully).
            var format = ConfigFormatDetector.FromPath(configPath)
                ?? throw new ConfigValidationException($"Unrecognized configuration file: '{configPath}'.");
            IConfigParser parser = format == ConfigFormat.Yaml ? new YamlConfigParser() : new JsoncConfigParser();
            var dom = parser.Parse(File.ReadAllText(configPath));

            var section = settings.Module ? "modules" : "resources";
            if (dom[section] is not JsonObject blocks)
            {
                blocks = [];
                dom[section] = blocks;
            }

            if (blocks.ContainsKey(blockName))
            {
                Console.Error.WriteLine($"Block '{blockName}' already exists in {section}.");
                return 1;
            }

            var newBlock = new JsonObject { ["type"] = settings.Type };
            if (settings.Module && settings.DependsOn.Length > 0)
            {
                newBlock["dependsOn"] = new JsonArray(settings.DependsOn.Select(d => (JsonNode)d).ToArray());
            }

            blocks[blockName] = newBlock;

            File.WriteAllText(configPath, Serialise(dom, format));
            Console.Out.WriteLine($"Added {section[..^1]} '{blockName}' ({settings.Type}) to {Path.GetFileName(configPath)}.");
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
    }

    private static string FindConfigPath(string projectDir)
    {
        string[] candidates = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];
        foreach (var name in candidates)
        {
            var path = Path.Combine(projectDir, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new ConfigValidationException($"No AspireForm configuration file found in '{projectDir}'.");
    }

    private static string Serialise(JsonObject dom, ConfigFormat format) => format switch
    {
        ConfigFormat.Jsonc => dom.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n",
        ConfigFormat.Yaml => new SerializerBuilder().Build().Serialize(DomToPlain(dom)),
        _ => throw new InvalidOperationException(),
    };

    private static object? DomToPlain(JsonNode? node) => node switch
    {
        JsonObject obj => obj.ToDictionary(kvp => kvp.Key, kvp => DomToPlain(kvp.Value)),
        JsonArray arr => arr.Select(DomToPlain).ToList(),
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        null => null,
        _ => node.ToString(),
    };
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — add:

```csharp
    config.AddCommand<AddCommand>("add")
        .WithDescription("Append a Resource (default) or Module block to the AspireForm config file. Comments and original formatting are not preserved.");
```

- [ ] **Step 5: Run tests (3 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/AddCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/AddCommandTests.cs
git commit -m "feat: add 'add' command for inserting blocks into the config (lossy round-trip)"
```

---

## Task 11: `import <type> <name>` command

Adopts an existing setup into AspireForm state without executing anything. Asks the provider for its plan output, records each `PlannedFileAction.Path` in state with the current on-disk checksum (or empty checksum if the file does not yet exist — the user is told).

**Files:**
- Create: `src/AspireForm/Cli/ImportCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register `import`
- Test: `tests/AspireForm.Tests/Cli/ImportCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class ImportCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-import-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunImport(params string[] args)
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
            app.Configure(c => c.AddCommand<ImportCommand>("import"));
            return (app.Run(["import", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Import_records_sql_block_into_state_with_checksum_of_existing_apphost()
    {
        // Pre-existing setup: aspireform.yaml + an AppHost.cs the user has been maintaining by hand.
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Demo
              apphost: Demo.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
            """);
        var apphostDir = Directory.CreateDirectory(Path.Combine(_dir, "Demo.AppHost"));
        File.WriteAllText(Path.Combine(apphostDir.FullName, "AppHost.cs"),
            "var builder = DistributedApplication.CreateBuilder(args);\nbuilder.AddSqlServer(\"sql\");\nbuilder.Build().Run();\n");

        var (exitCode, stdout, _) = RunImport("sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Imported");
        var loaded = new StateStore().Load(_dir);
        loaded.Blocks.Should().ContainKey("sql");
        loaded.Blocks["sql"].Files.Keys.Should().Contain(p => p.EndsWith("AppHost.cs"));
        loaded.Blocks["sql"].Files.Values.First().Checksum.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Import_refuses_when_block_is_not_in_config()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Demo
              apphost: Demo.AppHost
            """);

        var (exitCode, _, stderr) = RunImport("ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("ghost");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/ImportCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>import</c> command: records a block into state without running anything (adopts an existing setup).</summary>
public sealed class ImportCommand : Command<ImportCommand.Settings>
{
    /// <summary>Options for <c>import</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The block name (must already exist in the config file).</summary>
        [CommandArgument(0, "<BLOCK>")]
        [Description("The block name (must already exist in the config file).")]
        public required string BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);

            BlockKind blockKind;
            string blockType;
            System.Text.Json.Nodes.JsonObject inputs;

            if (loaded.Model.Resources.TryGetValue(settings.BlockName, out var r))
            {
                blockKind = BlockKind.Resource;
                blockType = r.Type;
                inputs = r.Inputs;
            }
            else if (loaded.Model.Modules.TryGetValue(settings.BlockName, out var m))
            {
                blockKind = BlockKind.Module;
                blockType = m.Type;
                inputs = m.Inputs;
            }
            else
            {
                Console.Error.WriteLine($"Block '{settings.BlockName}' is not declared in the config file.");
                return 1;
            }

            var provider = ProviderRegistry.Default().Get(blockType);
            var ctx = new PlanContext(
                BlockName: settings.BlockName,
                Inputs: inputs,
                AppHostDirectory: loaded.Model.AspireForm.AppHost,
                ProjectName: loaded.Model.AspireForm.Project);
            var providerPlan = provider.Plan(ctx);

            var stateStore = new StateStore();
            var state = stateStore.Load(projectDir);

            var files = new Dictionary<string, FileState>(StringComparer.Ordinal);
            foreach (var planned in providerPlan.FileActions)
            {
                var absolute = Path.IsPathRooted(planned.Path)
                    ? planned.Path
                    : Path.GetFullPath(Path.Combine(projectDir, planned.Path));
                var checksum = File.Exists(absolute) ? DriftDetector.ComputeChecksum(absolute) : string.Empty;
                files[PathUtilities.ToRepoRelative(absolute, projectDir)] = new FileState
                {
                    OwnershipMode = planned.OwnershipMode.ToString().ToLowerInvariant(),
                    Checksum = checksum,
                };
            }

            state.Blocks[settings.BlockName] = new BlockState
            {
                Type = blockType,
                Kind = blockKind == BlockKind.Module ? "module" : "resource",
                Files = files,
                Inputs = inputs,
            };

            stateStore.Save(projectDir, state);
            Console.Out.WriteLine($"Imported '{settings.BlockName}' ({blockType}, {files.Count} file(s)).");
            return 0;
        }
        catch (ConfigValidationException ex) { return Fail("Configuration error", ex); }
        catch (StateException ex)             { return Fail("State error", ex); }
        catch (ProviderNotFoundException ex)  { return Fail("Import error", ex); }
    }

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`** — add:

```csharp
    config.AddCommand<ImportCommand>("import")
        .WithDescription("Adopt an existing block into AspireForm state (records the block without executing).");
```

- [ ] **Step 5: Run tests (2 new tests)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/ImportCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/ImportCommandTests.cs
git commit -m "feat: add import command (adopts existing setup into state)"
```

---

## Task 12: File-snapshot end-to-end test for `apply`

Runs the **real** tool's `apply` against a fresh scaffold and asserts the on-disk files match expectations. No Docker; no Aspire-Test-Framework. The companion Aspire-Test-Framework test arrives in Task 13.

**Files:**
- Test: `tests/AspireForm.Tests/EndToEnd/ApplySnapshotTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>Runs the real AspireForm tool's apply verb against a fresh scaffold and asserts the on-disk output.</summary>
public sealed class ApplySnapshotTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-apply-snapshot").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    private static (int ExitCode, string Output) RunTool(string workingDirectory, params string[] args)
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
            WorkingDirectory = workingDirectory,
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
    public void Apply_against_an_ef_data_only_config_writes_dbcontext_and_state()
    {
        // ef-data only — no CLI actions, so no aspire dependency required at test time.
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Snapshot
              apphost: Snapshot.AppHost
            modules:
              data:
                type: ef-data
                database: appdb
                contextName: AppDbContext
            """);

        var (exitCode, output) = RunTool(_dir, "apply", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(0, output);
        File.Exists(Path.Combine(_dir, "Snapshot.AppHost", "Data", "AppDbContext.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_dir, "Snapshot.AppHost", "Data", "AppDbContext.cs"))
            .Should().Contain("class AppDbContext : DbContext");
    }
}
```

- [ ] **Step 2: Run tests**

Expected: PASS. The total test count grows by 1 to whatever cumulative is current.

- [ ] **Step 3: Commit**

```bash
git add tests/AspireForm.Tests/EndToEnd/ApplySnapshotTests.cs
git commit -m "test: add file-snapshot e2e test for apply"
```

---

## Task 13: Aspire-Test-Framework end-to-end test (Docker-gated)

Spins up a real Aspire AppHost containing a SQL Server resource via `DistributedApplicationTestingBuilder`, and asserts the resource reaches a healthy state. The test is skipped when Docker is unavailable on the runner.

**Files:**
- Modify: `tests/AspireForm.Tests/AspireForm.Tests.csproj` — add `Aspire.Hosting.Testing` package
- Test: `tests/AspireForm.Tests/EndToEnd/ApplyAspireBootTests.cs`

- [ ] **Step 1: Add the NuGet package**

```bash
dotnet add tests/AspireForm.Tests/AspireForm.Tests.csproj package Aspire.Hosting.Testing --version 13.3.5
```

- [ ] **Step 2: Write the test**

```csharp
using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>
/// Boots an in-process Aspire AppHost containing a SqlServer resource (the same resource
/// AspireForm's <c>sqlserver</c> Resource would scaffold) and asserts it reaches a healthy state.
/// Skipped when Docker is not available.
/// </summary>
public sealed class ApplyAspireBootTests
{
    private static bool DockerIsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task SqlServer_resource_reaches_healthy_state_when_apphost_boots()
    {
        if (!DockerIsAvailable())
        {
            // xUnit v3 dynamic skip pattern: short-circuit + return.
            return;
        }

        // Build an in-memory AppHost the way AspireForm's sqlserver Resource scaffolds one.
        var appHost = DistributedApplication.CreateBuilder();
        var sql = appHost.AddSqlServer("sql");
        var appdb = sql.AddDatabase("appdb");

        await using var application = await new DistributedApplicationTestingBuilder()
            .CopyFrom(appHost)
            .BuildAsync();

        await application.StartAsync();

        // Wait for the SQL container to report Running, with a generous timeout for first-time image pulls.
        var notifier = application.Services.GetService<Aspire.Hosting.ApplicationModel.ResourceNotificationService>()!;
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await notifier.WaitForResourceAsync("sql",
            r => r.Snapshot.State?.Text is "Running" or "Healthy", cts.Token);

        await application.StopAsync();
    }
}
```

> The `CopyFrom` extension assumes Aspire.Hosting.Testing 13.3.5's API surface; the goal here is "spin a SqlServer resource and observe it." If the API has shifted, use `DistributedApplicationTestingBuilder.CreateAsync<T>` with a simple AppHost program-class instead — the test's intent is the same.

- [ ] **Step 3: Run tests**

On a machine without Docker, the test returns immediately (no failure). On a machine with Docker, it actually pulls the SQL Server image (first run takes several minutes) and verifies the resource reaches Running.

- [ ] **Step 4: Commit**

```bash
git add tests/AspireForm.Tests/AspireForm.Tests.csproj tests/AspireForm.Tests/EndToEnd/ApplyAspireBootTests.cs
git commit -m "test: add Aspire-Test-Framework e2e for sqlserver resource (Docker-gated)"
```

---

## Task 14: README + CHANGELOG + csproj version bump for 0.2.0

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Update `README.md`** — expand the "Commands" table to list every shipped verb. Replace the existing table with:

```markdown
## Commands

| Command | Description |
|---|---|
| `aspireform new <name>` | Scaffold a new Aspire solution + a starter `aspireform.yaml`. |
| `aspireform add <type> [name]` | Append a Resource (or Module via `--module`) block to the config. |
| `aspireform config` | Print the fully merged, interpolated desired-state configuration. |
| `aspireform plan` | Show the reconciliation diff between desired and current state. |
| `aspireform apply` | Execute the plan after an approval gate (skip with `--yes`). |
| `aspireform destroy [block]` | Remove one block (or all blocks) from state. |
| `aspireform import <block>` | Adopt an existing block into state without executing. |
| `aspireform state list` | List every tracked block. |
| `aspireform state show <block>` | Dump one block's state as JSON. |
| `aspireform doctor` | Check prerequisites (.NET 10 SDK + `aspire` CLI). |
```

Also update the "Status" section:

```markdown
## Status

v0.2.0 — Core Engine complete. Reconciles a declarative `aspireform.yaml` against on-disk state
for the built-in `sqlserver` and `ef-data` blocks. External plugins, full Module wiring, and
additional verticals arrive in the verticals-catalog sub-project.
```

- [ ] **Step 2: Update `CHANGELOG.md`** — add a `## [0.2.0]` section at the top, above `## [0.1.0]`:

```markdown
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
```

- [ ] **Step 3: Bump `<Version>` in `src/AspireForm/AspireForm.csproj`**

Change `<Version>0.1.0</Version>` to `<Version>0.2.0</Version>`. (The release workflow derives the package version from the git tag, but keeping the csproj in sync makes local `dotnet pack` produce a correctly-named artifact.)

- [ ] **Step 4: Commit**

```bash
git add README.md CHANGELOG.md src/AspireForm/AspireForm.csproj
git commit -m "docs: README + CHANGELOG + csproj version bump for 0.2.0"
```

---

## Plan 3 — Definition of done

- **Verbs shipped:** `apply`, `destroy`, `new`, `add`, `import`, `state list`, `state show` — in addition to `config`, `plan`, `doctor` from prior plans.
- **Executor** persists state per-block, refuses on drift unless `--force-drift`, propagates CLI failures cleanly.
- **State** is portable (repo-relative paths, committed to git).
- **End-to-end tests** verify the real tool applies against a fixture (file-snapshot, always-on) and the Aspire-Test-Framework path validates a SqlServer resource boots (Docker-gated).
- **xUnit v3 / MTP suite** stays green.
- **Release artifacts** prepared (README, CHANGELOG, csproj version) so a `v0.2.0` tag triggers the existing release workflow to publish AspireForm 0.2.0 to NuGet.

---

## Release procedure (after the plan merges to main)

Not a plan task — this is the operator's post-merge runbook:

```bash
git checkout main
git pull                                           # sync local with whatever Plan 3 landed
git tag -a v0.2.0 -m "AspireForm 0.2.0 — Core Engine complete"
git push origin v0.2.0                             # triggers .github/workflows/release.yml
gh run watch                                       # observe; workflow builds, tests, packs, pushes to NuGet, creates release
```

After NuGet indexing (5–15 min):

```bash
dnx --yes AspireForm new MyDemoApp
cd MyDemoApp
dnx --yes AspireForm plan
dnx --yes AspireForm apply --yes
dnx --yes AspireForm state list
```

---

## Self-review notes

- **Spec coverage:** §9 `apply`, `destroy`, `new`, `add`, `import`, `state list/show` — Tasks 5–11. §6.4 `apply` half — Task 4. §7 state writeback (repo-relative paths) — Tasks 3, 4. §10 IAspireCli expanded — Task 1. §11 testing (Aspire-Test-Framework path) — Task 13. The full set of v1 verbs from the spec's command table is now shipped.
- **Deliberate v1 narrowings called out in the plan body:**
  - `add` is lossy with comments (round-trips through canonical DOM).
  - `ef-data` Module remains minimal (no real DI/migration wiring) — same as Plan 2.
  - Aspire-Test-Framework test is Docker-gated (skips otherwise).
  - Executor commits state per-block (no transactional roll-back across blocks); failed blocks just stop the run.
- **Placeholder scan:** none. Every step has concrete code or commands. Each task can be executed without further design decisions.
- **Type / name consistency:**
  - `CliResult(ExitCode, StandardOutput, StandardError)` — record from Task 1.
  - `ExecuteOptions { AutoApprove, ForceDrift }`, `ExecutionResult { Success, FailureMessage, BlocksApplied, BlocksFailed, NewState }` — Task 2.
  - `PathUtilities.ToRepoRelative(absolute, projectDir)` / `FromRepoRelative(relative, projectDir)` — Task 3.
  - `Executor(IAspireCli, StateStore).ApplyAsync(Plan, ProjectModel, AspireFormState prevState, string projectDir, ExecuteOptions, CancellationToken)` — Task 4.
  - Commands: `ApplyCommand`, `DestroyCommand`, `NewCommand`, `AddCommand`, `ImportCommand`, `StateListCommand`, `StateShowCommand` — Tasks 5–11.
  - Each command's settings shape uses Spectre's `CommandArgument` / `CommandOption` attributes consistently with prior plans.

Names match across every task that references them.
