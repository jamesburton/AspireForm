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

// Alias to avoid clash between the namespace AspireForm.EntityCatalog and the record type of the same name.
using EntityCatalogSnapshot = AspireForm.EntityCatalog.EntityCatalog;

namespace AspireForm.Tests.Ui;

public sealed class EntitiesPageTests
{
    [Fact]
    public void Entities_shows_load_error_banner_when_no_csproj_in_project_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            using var ctx = new BunitTestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.Services.AddBlazorBlueprintComponents();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEntityCatalogService>(new EmptyCatalog());

            var cut = ctx.RenderComponent<Entities>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("No .csproj found"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Entities_renders_sidebar_with_entities_from_catalog()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        try
        {
            using var ctx = new BunitTestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
            ctx.Services.AddBlazorBlueprintComponents();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEntityCatalogService>(new SeededCatalog(
                new EntityCatalogSnapshot(
                    [new Entity("Book", "Demo", "Demo/Book.cs", [new Property("Id", "int", false, true, [])], [], [])],
                    [], [])));

            var cut = ctx.RenderComponent<Entities>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("Book"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class EmptyCatalog : IEntityCatalogService
    {
        public Task<EntityCatalogSnapshot> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(new EntityCatalogSnapshot([], [], []));
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }

    private sealed class SeededCatalog : IEntityCatalogService
    {
        private readonly EntityCatalogSnapshot _snap;
        public SeededCatalog(EntityCatalogSnapshot snap) { _snap = snap; }
        public Task<EntityCatalogSnapshot> ScanAsync(string csprojPath, CancellationToken ct) => Task.FromResult(_snap);
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }
}
