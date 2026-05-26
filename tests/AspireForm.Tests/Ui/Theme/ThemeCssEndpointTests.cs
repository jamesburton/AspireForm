using AspireForm.Ui;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeCssEndpointTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"af-theme-ep-{Guid.NewGuid():N}");

    public ThemeCssEndpointTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static int FindFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<HttpResponseMessage?> PollAsync(HttpClient http, string path, int attempts = 20)
    {
        HttpResponseMessage? resp = null;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                resp = await http.GetAsync(path);
                if (resp.IsSuccessStatusCode) break;
            }
            catch (HttpRequestException) { await Task.Delay(150); }
        }
        return resp;
    }

    [Fact]
    public async Task ThemeCss_returns_200_with_css_content_type()
    {
        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var resp = await PollAsync(http, "/theme.css");

            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            resp.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }

    [Fact]
    public async Task ThemeCss_contains_root_block_with_expected_tokens()
    {
        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var resp = await PollAsync(http, "/theme.css");
            var body = await resp!.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            body.Should().Contain(":root");
            body.Should().Contain("--background:");
            body.Should().Contain("--primary:");
            body.Should().Contain("--foreground:");
            body.Should().Contain("--radius:");

            // Verify all 19 known tokens are emitted.
            foreach (var token in AspireForm.Ui.Theme.ThemeTokenNames.All)
                body.Should().Contain($"--{token}:", because: $"token '{token}' must appear in /theme.css");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }
}
