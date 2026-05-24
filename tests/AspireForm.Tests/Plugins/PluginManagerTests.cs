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
        var registry = await manager.DiscoverAndLoadAsync(model, _dir);

        registry.Get("sqlserver").Should().NotBeNull();
        // No lockfile written because no plugin was needed.
        File.Exists(Path.Combine(_dir, ".aspireform", "plugins.lock.yaml")).Should().BeFalse();
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
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
        };

        var manager = new PluginManager();
        var registry = await manager.DiscoverAndLoadAsync(model, _dir);

        // Built-ins still present.
        registry.Get("sqlserver").Should().NotBeNull();

        // Lockfile entry untouched.
        PluginLockfile.Load(_dir).Plugins.Should().ContainSingle(p => p.Name == "Unused");
    }
}
