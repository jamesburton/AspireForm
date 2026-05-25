using AspireForm.EntityCatalog;
using AspireForm.Ui;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

// Alias to avoid ambiguity with System.Index from implicit usings.
using IndexPage = AspireForm.Ui.Components.Pages.Index;

// Alias to avoid clash between the namespace AspireForm.EntityCatalog and the record type of the same name.
using EntityCatalogSnapshot = AspireForm.EntityCatalog.EntityCatalog;

namespace AspireForm.Tests.Ui;

public sealed class IndexPageTests
{
    [Fact]
    public void Index_renders_project_dir_and_link_to_entities()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton(new UiOptions { ProjectDir = "C:/demo", Port = 5050 });
        ctx.Services.AddSingleton<IEntityCatalogService>(new FakeCatalogService());
        var cut = ctx.RenderComponent<IndexPage>();
        cut.Markup.Should().Contain("C:/demo");
        cut.Markup.Should().Contain("/entities");
    }

    private sealed class FakeCatalogService : IEntityCatalogService
    {
        public Task<EntityCatalogSnapshot> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(new EntityCatalogSnapshot([], [], []));
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }
}
