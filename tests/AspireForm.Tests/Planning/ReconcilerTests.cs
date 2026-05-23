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

    [Fact]
    public void Provider_emitting_a_relative_path_resolves_against_projectDir()
    {
        // Write a file at <projectDir>/relative/file.cs
        var relative = Path.Combine("relative", "file.cs");
        var absolute = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, "// pre-existing");

        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(relative, OwnershipMode.Scaffold, "// new")],
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Create,
            providerPlan, previousState: null, projectDir: _dir);

        // File existed under projectDir, so Scaffold mode skips.
        actions.FileActions[0].Kind.Should().Be(FileActionKind.Skip);
    }

    [Fact]
    public void Managed_file_that_does_not_exist_proposes_create_with_just_the_marker_region()
    {
        var path = Path.Combine(_dir, "AppHost.cs");
        var providerPlan = new ProviderPlan
        {
            FileActions = [Action(path, OwnershipMode.Managed, "var sql = builder.AddSqlServer(\"sql\");")],
        };

        var actions = _reconciler.Reconcile("sql", BlockKind.Resource, BlockActionKind.Create,
            providerPlan, previousState: null, projectDir: _dir);

        var fa = actions.FileActions[0];
        fa.Kind.Should().Be(FileActionKind.Create);
        fa.AfterContent.Should().Contain("// <aspireform:block=sql>");
        fa.AfterContent.Should().Contain("var sql = builder.AddSqlServer(\"sql\");");

        // No synthesised host scaffold:
        fa.AfterContent.Should().NotContain("CreateBuilder(args)");
        fa.AfterContent.Should().NotContain("Build().Run");
    }

    [Fact]
    public void DELETE_block_skips_scaffold_files_and_removes_managed_files()
    {
        var managedPath = Path.Combine(_dir, "managed.cs");
        var scaffoldPath = Path.Combine(_dir, "scaffold.cs");
        File.WriteAllText(managedPath, "m");
        File.WriteAllText(scaffoldPath, "s");

        var prev = new BlockState
        {
            Type = "ef-data", Kind = "module",
            Files =
            {
                [managedPath] = new FileState { OwnershipMode = "managed", Checksum = DriftDetector.ComputeChecksum(managedPath) },
                [scaffoldPath] = new FileState { OwnershipMode = "scaffold", Checksum = DriftDetector.ComputeChecksum(scaffoldPath) },
            },
        };

        var actions = _reconciler.Reconcile("data", BlockKind.Module, BlockActionKind.Delete,
            new ProviderPlan(), prev, _dir);

        actions.FileActions.Should().HaveCount(2);
        actions.FileActions.Single(f => f.Path == managedPath).Kind.Should().Be(FileActionKind.Remove);
        actions.FileActions.Single(f => f.Path == scaffoldPath).Kind.Should().Be(FileActionKind.Skip);
    }
}
