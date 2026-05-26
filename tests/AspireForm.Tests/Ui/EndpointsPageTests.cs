using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Ui;
using AspireForm.Ui.Components.Pages;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

// Alias to avoid clash between the namespace AspireForm.ApiCatalog and the record type of the same name.
using EndpointCatalogSnapshot = AspireForm.ApiCatalog.EndpointCatalog;

namespace AspireForm.Tests.Ui;

public sealed class EndpointsPageTests
{
    [Fact]
    public void Endpoints_shows_load_error_banner_when_no_csproj_in_project_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ep-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            using var ctx = new BunitTestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
            ctx.Services.AddBlazorBlueprintComponents();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEndpointCatalogService>(new EmptyCatalogService());

            var cut = ctx.RenderComponent<Endpoints>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("No .csproj found"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Endpoints_renders_sidebar_with_endpoints_from_catalog()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ep-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        try
        {
            using var ctx = new BunitTestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
            ctx.Services.AddBlazorBlueprintComponents();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEndpointCatalogService>(new SeededCatalogService(
                new EndpointCatalogSnapshot(
                    [new EndpointInfo("BooksHandler", "GetBooks", "/books", "GET", null, null, [], [], [], "BooksHandler.cs")],
                    [])));

            var cut = ctx.RenderComponent<Endpoints>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("/books"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Endpoints_shows_no_endpoints_message_when_catalog_is_empty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ep-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        try
        {
            using var ctx = new BunitTestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
            ctx.Services.AddBlazorBlueprintComponents();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEndpointCatalogService>(new EmptyCatalogService());

            var cut = ctx.RenderComponent<Endpoints>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("No endpoints"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class EmptyCatalogService : IEndpointCatalogService
    {
        public Task<EndpointCatalogSnapshot> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(new EndpointCatalogSnapshot([], []));

        public Task<EndpointMutationResult> MutateAsync(string csprojPath, EndpointChangeRequest request, CancellationToken ct) =>
            Task.FromResult(EndpointMutationResult.Ok([]));
    }

    private sealed class SeededCatalogService : IEndpointCatalogService
    {
        private readonly EndpointCatalogSnapshot _snap;

        public SeededCatalogService(EndpointCatalogSnapshot snap) { _snap = snap; }

        public Task<EndpointCatalogSnapshot> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(_snap);

        public Task<EndpointMutationResult> MutateAsync(string csprojPath, EndpointChangeRequest request, CancellationToken ct) =>
            Task.FromResult(EndpointMutationResult.Ok([]));
    }
}
