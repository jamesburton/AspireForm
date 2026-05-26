using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using AspireForm.Ui.Theme;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace AspireForm.Ui;

/// <summary>Hosts Kestrel + Blazor Server inside the dnx tool process.</summary>
internal static class UiHost
{
    /// <summary>Runs the host until <paramref name="ct"/> fires or Ctrl-C is received.</summary>
    public static async Task RunAsync(UiOptions opts, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        /* Blueprint requires its own service registration for portals, focus traps, toasts, etc. */
        builder.Services.AddBlazorBlueprintComponents();

        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Services.AddSingleton<IThemeStore>(_ => new ThemeStore(opts.ProjectDir));
        builder.Logging.ClearProviders(); // keep stdout clean for dnx users

        var app = builder.Build();

        /* Serve wwwroot (app.css, theme-interop.js, etc.). */
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
        }

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
        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await app.RunAsync(ct);
    }

    private sealed record SetActiveRequest(string Name);
    private sealed record SetDarkModeRequest(bool Dark);
}
