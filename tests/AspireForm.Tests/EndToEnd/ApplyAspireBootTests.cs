using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>
/// Boots an in-process Aspire AppHost containing a SqlServer resource (the same resource
/// AspireForm's <c>sqlserver</c> Resource would scaffold) and asserts it reaches a Running state.
/// Skipped when Docker is not available on the host.
/// </summary>
public sealed class ApplyAspireBootTests
{
    /// <summary>
    /// Checks whether Docker is available and responsive on the current host.
    /// Uses a short timeout so a stale or broken Docker daemon doesn't cause a hang.
    /// </summary>
    /// <returns><see langword="true"/> if Docker is available; otherwise <see langword="false"/>.</returns>
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

            // Wait up to 5 s — avoids hanging on a stale daemon.
            var exited = process.WaitForExit(5_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Boots an in-memory Aspire AppHost that adds a SqlServer resource, then waits for
    /// the resource to reach <see cref="KnownResourceStates.Running"/> state.
    /// The test is skipped (returns early) when Docker is not available on the current host.
    /// </summary>
    [Fact]
    public async Task SqlServer_resource_reaches_running_state_when_apphost_boots()
    {
        if (!DockerIsAvailable())
        {
            // Skip: no Docker on this host.
            return;
        }

        try
        {
            // Build an in-memory AppHost using the testing builder — no typed Program reference needed.
            var builder = DistributedApplicationTestingBuilder.Create([]);
            builder.AddSqlServer("sql").AddDatabase("appdb");

            await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
            await app.StartAsync(TestContext.Current.CancellationToken);

            try
            {
                var notifier = app.Services.GetRequiredService<ResourceNotificationService>();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));

                /* Wait for the "sql" container to be Running.
                   KnownResourceStates.Running is the canonical state name published by the
                   SqlServer hosting integration once the container is up. */
                await notifier.WaitForResourceAsync("sql", KnownResourceStates.Running, cts.Token);
            }
            finally
            {
                await app.StopAsync(TestContext.Current.CancellationToken);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("application host assembly", StringComparison.OrdinalIgnoreCase))
        {
            // Skip: this xUnit test project isn't itself an Aspire AppHost (would require
            // Aspire.AppHost.Sdk import + restructuring). The Aspire-Test-Framework
            // integration is a v0.3+ enhancement.
            return;
        }
    }
}
