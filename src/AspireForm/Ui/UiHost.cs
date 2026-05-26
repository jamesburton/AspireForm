using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using AspireForm.Ui.Theme;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(UiHost).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(typeof(UiHost).Assembly.Location),
        });
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Services.AddSingleton<IThemeStore>(_ => new ThemeStore(opts.ProjectDir));
        builder.Logging.ClearProviders(); // keep stdout clean for dnx users

        var app = builder.Build();

        // MapStaticAssets serves both the project's wwwroot/ AND the Blazor framework assets
        // (_framework/blazor.web.js, etc.) via the static-assets manifest produced by the Web SDK.
        // UseStaticFiles alone does NOT serve framework assets — the interactive circuit will fail
        // to bootstrap without MapStaticAssets.
        app.MapStaticAssets();
        app.UseAntiforgery();

        // Serve the active theme tokens as CSS custom properties.
        app.MapGet("/theme.css", (IThemeStore themeStore) =>
        {
            var tokens = themeStore.GetTokens();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(":root {");
            foreach (var kv in tokens)
                sb.AppendLine($"  --af-{kv.Key}: {kv.Value};");
            sb.AppendLine("}");
            return Results.Content(sb.ToString(), "text/css");
        });

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        var url = $"http://localhost:{opts.Port}";
        Console.Out.WriteLine($"AspireForm UI listening at {url} (project-dir: {opts.ProjectDir})");
        Console.Out.WriteLine("Press Ctrl+C to stop.");
        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await app.RunAsync(ct);
    }
}
