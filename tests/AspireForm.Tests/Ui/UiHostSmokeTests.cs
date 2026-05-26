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
                try { resp = await http.GetAsync("/", TestContext.Current.CancellationToken); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150, TestContext.Current.CancellationToken); }
            }
            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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
    public async Task UiHost_serves_blazor_framework_javascript_with_non_empty_body()
    {
        // Regression for the "200 OK with 0 bytes" / "500 dev-handler" saga: _framework/blazor.web.js
        // lives in the .NET SDK NuGet cache (not our wwwroot). The Web SDK's dev-time static-asset
        // patching handler will 500 here if the runtime environment doesn't match the build-time
        // manifest (UiHost forces EnvironmentName=Development for this reason).
        var body = await AssertAssetServedWithBody(
            "/_framework/blazor.web.js",
            expectedContentTypePrefix: "text/javascript",
            minBytes: 50_000);
        body.Should().Contain("Blazor", "the bundled blazor.web.js identifies itself");
    }

    [Fact]
    public async Task UiHost_serves_app_css_from_wwwroot_with_non_empty_body()
    {
        // Regression for the wwwroot-location bug: app.css moved from src/AspireForm/Ui/wwwroot/
        // to src/AspireForm/wwwroot/ so the Web SDK manifest picks it up. Body must contain at
        // least one of the AspireForm-specific utility selectors that this file provides.
        var body = await AssertAssetServedWithBody(
            "/app.css",
            expectedContentTypePrefix: "text/css",
            minBytes: 200);
        body.Should().Contain(".w-56", "AspireForm-only Tailwind utility supplied by app.css");
    }

    [Fact]
    public async Task UiHost_serves_blazorblueprint_css_from_package_static_assets_with_non_empty_body()
    {
        // Regression for the package-asset 500 bug: Blueprint's CSS comes from
        // _content/BlazorBlueprint.Components/ in the .nuget package cache. The dev-time
        // patching handler must be able to reach across multiple ContentRoots, not just
        // the project's own wwwroot/.
        var body = await AssertAssetServedWithBody(
            "/_content/BlazorBlueprint.Components/blazorblueprint.css",
            expectedContentTypePrefix: "text/css",
            minBytes: 50_000);
        body.Should().Contain("tailwindcss", "the bundled Blueprint CSS is a Tailwind build");
    }

    [Fact]
    public async Task UiHost_serves_theme_interop_js_from_wwwroot_with_non_empty_body()
    {
        var body = await AssertAssetServedWithBody(
            "/theme-interop.js",
            expectedContentTypePrefix: "text/javascript",
            minBytes: 100);
        body.Should().Contain("setDarkMode", "theme-interop.js exposes the dark-mode bridge");
    }

    [Fact]
    public async Task UiHost_serves_dynamic_theme_css_endpoint()
    {
        // /theme.css is OUR endpoint (not a static asset) — emits :root { --background: ...; ... }
        // for the active theme. Different code path from MapStaticAssets, worth its own check.
        var body = await AssertAssetServedWithBody(
            "/theme.css",
            expectedContentTypePrefix: "text/css",
            minBytes: 100);
        body.Should().Contain(":root", "theme.css declares CSS variables on :root");
        body.Should().Contain("--background", "the active theme defines --background");
    }

    [Fact]
    public async Task UiHost_returns_no_5xx_errors_for_any_resource_referenced_by_the_root_page()
    {
        // End-to-end regression for the 500-on-static-assets class of bug. Loads the root HTML,
        // extracts every <link href=...> and <script src=...>, GETs each, and asserts they all
        // return success. The original bug (StaticAssetDevelopmentRuntimeHandler firing in
        // Production env) produced 500s on _framework/blazor.web.js and _content/*.css while
        // the per-asset tests above would have passed because they each used isolated hosts.
        // This walks the real graph the browser walks.
        var port = FindFreeTcpPort();
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var html = await GetWithRetry(http, "/");
            var hrefs = ExtractAssetUrls(html);
            hrefs.Should().NotBeEmpty("the root page must link to at least one stylesheet/script");

            foreach (var href in hrefs)
            {
                using var resp = await http.GetAsync(href, TestContext.Current.CancellationToken);
                ((int)resp.StatusCode).Should().BeLessThan(500,
                    $"GET {href} (linked from the root page) must not 5xx — got {(int)resp.StatusCode}. " +
                    "This was the failure mode of the dev-handler-in-Production bug.");
                resp.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"GET {href} should return 200, got {(int)resp.StatusCode}");
                var bodyBytes = await resp.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
                bodyBytes.Should().NotBeEmpty($"asset {href} returned an empty body");
            }
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { /* host shutdown best-effort */ }
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task<string> GetWithRetry(HttpClient http, string path)
    {
        HttpResponseMessage? resp = null;
        for (int i = 0; i < 20; i++)
        {
            try { resp = await http.GetAsync(path, TestContext.Current.CancellationToken); if (resp.IsSuccessStatusCode) break; }
            catch (HttpRequestException) { await Task.Delay(150, TestContext.Current.CancellationToken); }
        }
        resp.Should().NotBeNull();
        resp!.IsSuccessStatusCode.Should().BeTrue($"GET {path} must succeed");
        return await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static List<string> ExtractAssetUrls(string html)
    {
        var urls = new List<string>();
        // <link ... href="..."> — stylesheets
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     html, @"<link[^>]*\bhref\s*=\s*[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            urls.Add(m.Groups[1].Value);
        }
        // <script ... src="..."> — scripts
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     html, @"<script[^>]*\bsrc\s*=\s*[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            urls.Add(m.Groups[1].Value);
        }
        return urls;
    }

    // Asserts the asset returns 200 with the expected content-type AND a non-trivial body.
    // The 500-with-empty-body and 200-with-empty-body bugs that bit us would both be caught
    // by the minBytes check.
    private static async Task<string> AssertAssetServedWithBody(string path, string expectedContentTypePrefix, int minBytes)
    {
        var port = FindFreeTcpPort();
        var dir = Path.Combine(Path.GetTempPath(), $"af-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);

        try
        {
            // Enable automatic decompression — MapStaticAssets ships pre-compressed .gz variants
            // and picks them when the client advertises gzip. Without an explicit handler,
            // HttpClient receives compressed bytes and ReadAsStringAsync returns nonsense.
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli };
            using var http = new HttpClient(handler) { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync(path, TestContext.Current.CancellationToken); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150, TestContext.Current.CancellationToken); }
            }
            resp.Should().NotBeNull($"the host must serve {path} via MapStaticAssets");
            resp!.StatusCode.Should().Be(HttpStatusCode.OK,
                $"GET {path} should return 200, not {(int)resp.StatusCode}. " +
                "Likely cause: dev-time static-asset handler running outside Development env, " +
                "or MapStaticAssets() not registered, or ApplicationName mis-set.");
            resp.Content.Headers.ContentType?.MediaType.Should().StartWith(expectedContentTypePrefix);
            var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Length.Should().BeGreaterThanOrEqualTo(minBytes,
                $"GET {path} returned {body.Length} bytes, expected >= {minBytes}. " +
                "An empty/tiny body usually means the static-asset endpoint matched but the " +
                "asset file couldn't be read at runtime — see the StaticAssetDevelopmentRuntimeHandler bug.");
            return body;
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { /* host shutdown best-effort */ }
            Directory.Delete(dir, recursive: true);
        }
    }
}
