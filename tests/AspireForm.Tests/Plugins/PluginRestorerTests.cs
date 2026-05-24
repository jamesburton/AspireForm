using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginRestorerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-restore").FullName;
    private readonly PluginRestorer _restorer = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task RestoreAsync_returns_path_to_restored_package_when_package_exists()
    {
        // Newtonsoft.Json 13.0.3 is universally cached on .NET dev machines.
        var result = await _restorer.RestoreAsync(
            packageId: "Newtonsoft.Json", version: "13.0.3", workingDirectory: _dir);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.PackageDirectory.Should().NotBeNull();
        Directory.Exists(result.PackageDirectory).Should().BeTrue();
        Directory.GetFiles(Path.Combine(result.PackageDirectory!, "lib"), "Newtonsoft.Json.dll",
            SearchOption.AllDirectories).Should().NotBeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_reports_failure_for_a_nonexistent_package()
    {
        var result = await _restorer.RestoreAsync(
            packageId: "This.Package.Does.Not.Exist.AspireForm.Test", version: "0.0.1", workingDirectory: _dir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetGlobalPackagesPath_returns_a_real_directory()
    {
        var path = PluginRestorer.GetGlobalPackagesPath();
        Directory.Exists(path).Should().BeTrue($"global packages path '{path}' should exist on a dev machine");
    }
}
