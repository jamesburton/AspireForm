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
        var fakeCli = new FakeAspireCli();
        var executor = new Executor(fakeCli, new StateStore());
        var model = SampleModel();
        var plan = PlanFor(model, new AspireFormState());

        var result = await executor.ApplyAsync(
            plan, model, prevState: new AspireFormState(), projectDir: _dir,
            options: new ExecuteOptions { AutoApprove = true });

        result.Success.Should().BeTrue();
        result.BlocksApplied.Should().Be(1);

        var apphostPath = Path.Combine(_dir, "MyApp.AppHost", "AppHost.cs");
        File.Exists(apphostPath).Should().BeTrue();
        File.ReadAllText(apphostPath).Should().Contain("<aspireform:block=sql>")
            .And.Contain("AddSqlServer(\"sql\")");

        fakeCli.Calls.Should().ContainSingle();
        fakeCli.Calls[0].Args.Should().ContainInOrder("add", "sqlserver");
        fakeCli.Calls[0].WorkingDirectory.Replace('\\', '/').Should().EndWith("MyApp.AppHost");

        var loaded = new StateStore().Load(_dir);
        loaded.Blocks.Should().ContainKey("sql");
        loaded.Blocks["sql"].Files.Keys.Should().Contain("MyApp.AppHost/AppHost.cs");
        loaded.Blocks["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
    }

    [Fact]
    public async Task ApplyAsync_refuses_when_drift_detected_unless_ForceDrift_is_set()
    {
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
