using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginLockfileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-pluginlock").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_returns_empty_lockfile_when_file_is_absent()
    {
        var lockfile = PluginLockfile.Load(_dir);
        lockfile.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void Save_then_Load_round_trips_plugin_entries()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis",
            Package = "AspireForm.Plugin.Redis",
            Version = "0.1.0",
        });

        PluginLockfile.Save(_dir, lockfile);
        var reloaded = PluginLockfile.Load(_dir);

        reloaded.Plugins.Should().ContainSingle();
        reloaded.Plugins[0].Name.Should().Be("Redis");
        reloaded.Plugins[0].Package.Should().Be("AspireForm.Plugin.Redis");
        reloaded.Plugins[0].Version.Should().Be("0.1.0");
    }

    [Fact]
    public void Save_writes_to_the_dot_aspireform_directory()
    {
        PluginLockfile.Save(_dir, new PluginLockfile());
        File.Exists(Path.Combine(_dir, ".aspireform", "plugins.lock.yaml")).Should().BeTrue();
    }
}
