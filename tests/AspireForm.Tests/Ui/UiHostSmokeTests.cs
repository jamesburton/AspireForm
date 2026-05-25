using System.Net.Sockets;
using AspireForm.Ui;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui;

public sealed class UiHostSmokeTests
{
    private static int FindFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task UiHost_serves_index_page_html_on_ephemeral_port()
    {
        var port = FindFreeTcpPort();
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);

        try
        {
            /* Give Kestrel a moment to come up — up to 20×150ms = 3 s. */
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }
            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("AspireForm");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { /* host shutdown best-effort */ }
            Directory.Delete(dir, recursive: true);
        }
    }
}
