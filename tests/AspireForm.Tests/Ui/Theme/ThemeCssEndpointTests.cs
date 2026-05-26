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

    [Fact]
    public async Task ThemeCss_returns_css_with_all_14_tokens()
    {
        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/theme.css"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }

            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            resp.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("--af-color-primary:");
            body.Should().Contain("--af-color-bg:");
            body.Should().Contain("--af-color-border:");

            // Check all 14 tokens are present.
            AspireForm.Ui.Theme.ThemeDefaults.Tokens
                .Should().AllSatisfy(t => body.Should().Contain(t.CssVar));
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }

    [Fact]
    public async Task ThemeCss_reflects_custom_token_saved_to_theme_json()
    {
        // Pre-seed the theme.json file directly.
        var aspireformDir = Path.Combine(_dir, ".aspireform");
        Directory.CreateDirectory(aspireformDir);
        await File.WriteAllTextAsync(Path.Combine(aspireformDir, "theme.json"),
            """{ "color-primary": "#aabbcc" }""");

        var port = FindFreeTcpPort();
        var opts = new UiOptions { ProjectDir = _dir, Port = port, LaunchBrowser = false };
        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/theme.css"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }

            var body = await resp!.Content.ReadAsStringAsync();
            body.Should().Contain("#aabbcc");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { }
        }
    }
}
