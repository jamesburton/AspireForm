using System.Net;
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

    [Fact]
    public async Task UiHost_serves_blazor_framework_javascript()
    {
        // Regression for the original "buttons don't respond" bug: _framework/blazor.web.js
        // is not in wwwroot — it ships via the Web SDK's static-assets manifest, which is
        // mounted by app.MapStaticAssets(). UseStaticFiles alone does NOT serve framework
        // assets; if this 404s, the interactive Blazor circuit can't bootstrap.
        await AssertAssetServed("/_framework/blazor.web.js", expectedContentTypePrefix: "text/javascript");
    }

    [Fact]
    public async Task UiHost_serves_app_css_from_wwwroot()
    {
        // Regression for the wwwroot-location bug: app.css lived at src/AspireForm/Ui/wwwroot/
        // before being moved to the conventional src/AspireForm/wwwroot/ location so the
        // Web SDK could bundle it into the static-assets manifest.
        await AssertAssetServed("/app.css", expectedContentTypePrefix: "text/css");
    }

    [Fact]
    public async Task UiHost_serves_blazorblueprint_css_from_package_static_assets()
    {
        // Blueprint's CSS comes from _content/BlazorBlueprint.Components/, served via the
        // static-assets manifest. Without MapStaticAssets the layout has no Tailwind base.
        await AssertAssetServed("/_content/BlazorBlueprint.Components/blazorblueprint.css", expectedContentTypePrefix: "text/css");
    }

    // Confirms the route is mounted (returns 200 with the right Content-Type via MapStaticAssets).
    // Body content is NOT asserted here — see CssBundleTests for a fast file-level check on app.css
    // contents that doesn't depend on the HTTP pipeline.
    private static async Task AssertAssetServed(string path, string expectedContentTypePrefix)
    {
        var port = FindFreeTcpPort();
        var dir = Path.Combine(Path.GetTempPath(), $"af-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync(path); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }
            resp.Should().NotBeNull($"the host must serve {path} via MapStaticAssets");
            resp!.StatusCode.Should().Be(HttpStatusCode.OK,
                $"GET {path} should return 200, not {(int)resp.StatusCode}. Most likely cause: app.MapStaticAssets() is missing or ApplicationName is mis-set so the manifest isn't discovered.");
            resp.Content.Headers.ContentType?.MediaType.Should().StartWith(expectedContentTypePrefix);
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { /* host shutdown best-effort */ }
            Directory.Delete(dir, recursive: true);
        }
    }
}
