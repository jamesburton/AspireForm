using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using AspireForm.Ui.Theme;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspireForm.Ui;

/// <summary>Hosts Kestrel + Blazor Server inside the dnx tool process.</summary>
internal static class UiHost
{
    /// <summary>Runs the host until <paramref name="ct"/> fires or Ctrl-C is received.</summary>
    public static async Task RunAsync(UiOptions opts, CancellationToken ct)
    {
        // Force ApplicationName to this assembly so MapStaticAssets() locates
        // AspireForm.staticwebassets.endpoints.json regardless of which host process
        // (production exe, or AspireForm.Tests when running smoke tests) is running.
        //
        // EnvironmentName=Development is set because Debug-built tools ship a dev-time
        // static-assets manifest that uses Microsoft.AspNetCore.Builder.StaticAssetDevelopmentRuntimeHandler.
        // That handler must run in a Development environment, otherwise it tries to
        // resolve every asset URL against WebRoot (wwwroot/) — fine for our own
        // wwwroot/app.css, but it 500s with FileNotFoundException for
        // _framework/blazor.web.js (framework asset, lives in the .NET SDK NuGet cache)
        // and _content/BlazorBlueprint.Components/* (lives in the Blueprint package
        // cache). AspireForm UI is always a local dev tool, never deployed, so
        // Development is the semantically correct environment.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(UiHost).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        /* Blueprint requires its own service registration for portals, focus traps, toasts, etc. */
        builder.Services.AddBlazorBlueprintComponents();

        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Services.AddSingleton<IThemeStore>(_ => new ThemeStore(opts.ProjectDir));

        // Suppress the noisy Info-level Kestrel/Routing/DataProtection chatter that dnx users
        // don't need, but keep Warning+ visible so failures (e.g. a 500 from a static-asset
        // handler) actually surface rather than dying silently behind the curtain.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();

        // MapStaticAssets serves both the project's wwwroot/ AND the Blazor framework assets
        // (_framework/blazor.web.js, etc.) via the static-assets manifest produced by the Web SDK.
        // UseStaticFiles alone does NOT serve framework assets — the interactive circuit will fail
        // to bootstrap without MapStaticAssets.
        app.MapStaticAssets();
        app.UseAntiforgery();

        /* /theme.css — emits :root { --background: ...; ... } for the active theme. */
        app.MapGet("/theme.css", async (IThemeStore themeStore) =>
        {
            var activation = await themeStore.GetActiveAsync();
            ThemeDefinition theme;
            try { theme = await themeStore.GetAsync(activation.ActiveName); }
            catch (ThemeLoadException)
            {
                /* Fallback: return an empty stylesheet if the active theme is broken. */
                return Results.Content(":root {}", "text/css");
            }

            var tokens = activation.DarkMode ? theme.Dark : theme.Light;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(":root {");
            foreach (var kv in tokens)
                sb.AppendLine($"  --{kv.Key}: {kv.Value};");
            sb.AppendLine($"  --radius: {theme.Radius.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}rem;");
            sb.AppendLine("}");
            if (activation.DarkMode)
                sb.AppendLine("html { color-scheme: dark; }");

            return Results.Content(sb.ToString(), "text/css");
        });

        /* POST /themes/set-active — switches the active theme (called from ThemeSwitcherDropdown JS). */
        app.MapPost("/themes/set-active", async (IThemeStore themeStore, SetActiveRequest req) =>
        {
            try
            {
                await themeStore.SetActiveAsync(req.Name);
                return Results.Ok(new { ok = true });
            }
            catch (ThemeLoadException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        /* POST /themes/set-dark-mode — toggles dark mode. */
        app.MapPost("/themes/set-dark-mode", async (IThemeStore themeStore, SetDarkModeRequest req) =>
        {
            await themeStore.SetDarkModeAsync(req.Dark);
            return Results.Ok(new { ok = true });
        });

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        var url = $"http://localhost:{opts.Port}";
        Console.Out.WriteLine($"AspireForm UI listening at {url} (project-dir: {opts.ProjectDir})");
        Console.Out.WriteLine("Press Ctrl+C to stop.");

        // Run the server in the background so we can probe it for self-check.
        var runTask = app.RunAsync(ct);

        // Self-check: confirm the critical assets the browser will fetch actually serve
        // correctly. Catches configuration drifts (dev-handler-in-Production env, wrong
        // ApplicationName, missing MapStaticAssets, broken /theme.css endpoint) and reports
        // them loudly to Console.Error rather than letting the user see a silently-broken UI.
        await PerformStartupSelfCheckAsync(url, ct);

        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await runTask;
    }

    /// <summary>
    /// Probes critical browser-fetched resources after Kestrel comes up. If any fail to
    /// serve, prints a clearly-formatted diagnostic block to Console.Error so the user
    /// sees the problem instead of silently broken UI.
    /// </summary>
    /// <param name="baseUrl">The URL the host is listening on.</param>
    /// <param name="ct">Cancellation token; if it fires before the self-check completes,
    /// the self-check is abandoned without producing a report.</param>
    internal static async Task PerformStartupSelfCheckAsync(string baseUrl, CancellationToken ct)
    {
        // Static assets shipped via MapStaticAssets that the root page links to.
        var criticalAssets = new[]
        {
            "/_framework/blazor.web.js",
            "/_content/BlazorBlueprint.Components/blazorblueprint.css",
            "/app.css",
            "/theme-interop.js",
            "/theme.css",
        };

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
        var failures = new List<(string Path, string Detail)>();

        foreach (var path in criticalAssets)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                using var resp = await http.GetAsync(path, ct);
                if ((int)resp.StatusCode >= 500)
                {
                    failures.Add((path, $"HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase})"));
                }
                else if (!resp.IsSuccessStatusCode)
                {
                    failures.Add((path, $"HTTP {(int)resp.StatusCode}"));
                }
                else
                {
                    var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    if (bytes.Length == 0)
                        failures.Add((path, $"HTTP {(int)resp.StatusCode} with empty body"));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                failures.Add((path, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        if (failures.Count == 0) return;

        Console.Error.WriteLine();
        Console.Error.WriteLine("==================================================================");
        Console.Error.WriteLine("AspireForm UI self-check failed — the UI will not work correctly:");
        Console.Error.WriteLine();
        foreach (var (path, detail) in failures)
            Console.Error.WriteLine($"  {path,-60} -> {detail}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Common causes:");
        Console.Error.WriteLine("  * Debug-built tools must run in Development env so the Web SDK's");
        Console.Error.WriteLine("    dev-time static-asset handler can resolve framework/package");
        Console.Error.WriteLine("    assets. UiHost forces EnvironmentName=Development for this; if");
        Console.Error.WriteLine("    you've overridden it via ASPNETCORE_ENVIRONMENT or similar, undo.");
        Console.Error.WriteLine("  * MapStaticAssets() not registered, or ApplicationName mismatched");
        Console.Error.WriteLine("    so the static-asset manifest isn't discovered at startup.");
        Console.Error.WriteLine("  * The package containing the failing _content/* asset isn't");
        Console.Error.WriteLine("    referenced or isn't restored in the bin output.");
        Console.Error.WriteLine("==================================================================");
        Console.Error.WriteLine();
    }

    private sealed record SetActiveRequest(string Name);
    private sealed record SetDarkModeRequest(bool Dark);
}
