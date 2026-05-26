using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Plugins;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginManagerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-pluginmgr").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* AssemblyLoadContext file locks may prevent cleanup */ }
        catch (UnauthorizedAccessException) { /* same */ }
    }

    [Fact]
    public async Task DiscoverAndLoadAsync_returns_only_builtin_providers_when_model_uses_only_builtin_types()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
        };

        var manager = new PluginManager();
        var registry = await manager.DiscoverAndLoadAsync(model, _dir, TestContext.Current.CancellationToken);

        registry.Get("sqlserver").Should().NotBeNull();
        // No lockfile written because no plugin was needed.
        File.Exists(Path.Combine(_dir, ".aspireform", "plugins.lock.yaml")).Should().BeFalse();
    }

    [Fact]
    public void CheckContractCompatibility_throws_when_plugin_requires_a_higher_AspireForm_version()
    {
        var manifest = PluginManifest.Parse("""
            {
              "name": "Future",
              "version": "1.0.0",
              "minAspireFormVersion": "99.0.0",
              "providers": []
            }
            """);

        var act = () => PluginManager.CheckContractCompatibility(manifest);
        act.Should().Throw<PluginContractException>()
           .WithMessage("*99.0.0*");
    }

    [Fact]
    public void CheckContractCompatibility_throws_when_minAspireFormVersion_is_unparseable()
    {
        var manifest = PluginManifest.Parse("""
            {
              "name": "Garbage",
              "version": "1.0.0",
              "minAspireFormVersion": "not-a-version",
              "providers": []
            }
            """);

        var act = () => PluginManager.CheckContractCompatibility(manifest);
        act.Should().Throw<PluginContractException>().WithMessage("*not-a-version*");
    }

    [Fact]
    public void CheckContractCompatibility_accepts_compatible_versions()
    {
        var manifest = PluginManifest.Parse("""
            {
              "name": "Current",
              "version": "1.0.0",
              "minAspireFormVersion": "0.1.0",
              "providers": []
            }
            """);

        var act = () => PluginManager.CheckContractCompatibility(manifest);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DiscoverAndLoadAsync_does_not_rewrite_lockfile_when_no_new_plugins_are_resolved()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Unused",
            Package = "AspireForm.Plugin.Unused",
            Version = "0.0.0",
        });
        PluginLockfile.Save(_dir, lockfile);

        var lockPath = Path.Combine(_dir, ".aspireform", "plugins.lock.yaml");
        var before = File.ReadAllText(lockPath);

        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
        };

        await new PluginManager().DiscoverAndLoadAsync(model, _dir, TestContext.Current.CancellationToken);

        var after = File.ReadAllText(lockPath);
        after.Should().Be(before, "DiscoverAndLoadAsync must not rewrite the lockfile when nothing changed");
    }

    [Fact]
    public async Task DiscoverAndLoadAsync_compiles_and_loads_a_script_plugin()
    {
        var scriptsDir = Path.Combine(_dir, ".aspireform", "scripts");
        Directory.CreateDirectory(scriptsDir);
        await File.WriteAllTextAsync(Path.Combine(scriptsDir, "my-vertical.cs"), """
            using AspireForm.Providers;
            namespace MyScript;
            public sealed class MyVerticalProvider : IProvider
            {
                public string Type => "my-vertical";
                public BlockKind Kind => BlockKind.Module;
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """, TestContext.Current.CancellationToken);

        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
            Modules = new Dictionary<string, ModuleBlock>
            {
                ["mine"] = new() { Name = "mine", Type = "my-vertical", Inputs = new() },
            },
        };

        var registry = await new PluginManager().DiscoverAndLoadAsync(model, _dir, TestContext.Current.CancellationToken);

        registry.Get("my-vertical").Type.Should().Be("my-vertical");
    }

    [Fact]
    public async Task DiscoverAndLoadAsync_preserves_pre_existing_lockfile_entries()
    {
        // Pre-seed lockfile with an entry that's not needed (no model block references it).
        // PluginManager should leave it alone — no restore triggered.
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Unused",
            Package = "AspireForm.Plugin.Unused",
            Version = "0.0.0",
        });
        PluginLockfile.Save(_dir, lockfile);

        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
        };

        var manager = new PluginManager();
        var registry = await manager.DiscoverAndLoadAsync(model, _dir, TestContext.Current.CancellationToken);

        // Built-ins still present.
        registry.Get("sqlserver").Should().NotBeNull();

        // Lockfile entry untouched.
        PluginLockfile.Load(_dir).Plugins.Should().ContainSingle(p => p.Name == "Unused");
    }
}
