using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

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
        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton<IEndpointCatalogService>(_ => new RoslynEndpointCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Logging.ClearProviders(); // keep stdout clean for dnx users

        // Serve embedded wwwroot files. With <FrameworkReference Microsoft.AspNetCore.App />, Blazor's
        // static file infrastructure handles framework assets (blazor.web.js, etc.); we only need to
        // map our own site.css from the source-controlled wwwroot/.
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var app = builder.Build();
        if (Directory.Exists(wwwroot))
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
        }
        app.UseAntiforgery();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        var url = $"http://localhost:{opts.Port}";
        Console.Out.WriteLine($"AspireForm UI listening at {url} (project-dir: {opts.ProjectDir})");
        Console.Out.WriteLine("Press Ctrl+C to stop.");
        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await app.RunAsync(ct);
    }
}
