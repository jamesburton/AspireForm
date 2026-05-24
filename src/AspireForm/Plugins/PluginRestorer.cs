using System.Diagnostics;

namespace AspireForm.Plugins;

/// <summary>The outcome of a plugin restore attempt.</summary>
/// <param name="Success">True when the package was restored and located.</param>
/// <param name="PackageDirectory">Absolute path to the restored package root (containing lib/, aspireform-plugin.json, etc.); null on failure.</param>
/// <param name="ErrorMessage">A human-readable error description when <paramref name="Success"/> is false.</param>
public sealed record PluginRestoreResult(bool Success, string? PackageDirectory, string? ErrorMessage);

/// <summary>
/// Restores a plugin package by shelling out to <c>dotnet restore</c> against a temporary csproj,
/// then locates the package in the global NuGet cache.
/// Avoids embedding the NuGet client library; lets the SDK handle dependency resolution.
/// </summary>
public sealed class PluginRestorer
{
    /// <summary>Returns the global NuGet packages directory (honours <c>NUGET_PACKAGES</c> env var, falls back to the standard location).</summary>
    public static string GetGlobalPackagesPath()
    {
        var env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".nuget", "packages");
    }

    /// <summary>Restores <paramref name="packageId"/>@<paramref name="version"/> via <c>dotnet restore</c> and returns the path to the cached package directory.</summary>
    /// <param name="packageId">The NuGet package ID to restore.</param>
    /// <param name="version">The exact version to restore.</param>
    /// <param name="workingDirectory">A writable directory where the temporary probe csproj is created.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PluginRestoreResult"/> describing success or failure.</returns>
    public async Task<PluginRestoreResult> RestoreAsync(
        string packageId,
        string version,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var probeDir = Path.Combine(workingDirectory, ".aspireform", "restore-probes",
            $"{packageId}-{version}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(probeDir);

        var csprojPath = Path.Combine(probeDir, "Probe.csproj");
        await File.WriteAllTextAsync(csprojPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{packageId}}" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = probeDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add("--nologo");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new PluginRestoreResult(false, null, "Failed to start 'dotnet'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return new PluginRestoreResult(false, null, $"dotnet restore exited with {process.ExitCode}: {msg.Trim()}");
            }
        }
        finally
        {
            try { Directory.Delete(probeDir, recursive: true); } catch { /* ignore */ }
        }

        // Locate the package in the global cache: <globalPackages>/<id-lower>/<version>/
        var packageDir = Path.Combine(GetGlobalPackagesPath(), packageId.ToLowerInvariant(), version);
        if (!Directory.Exists(packageDir))
        {
            return new PluginRestoreResult(false, null,
                $"Restore reported success but package directory '{packageDir}' was not found.");
        }

        return new PluginRestoreResult(true, packageDir, null);
    }
}
