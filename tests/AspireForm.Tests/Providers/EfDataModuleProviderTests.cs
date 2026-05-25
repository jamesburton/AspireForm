using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;
using Catalog = AspireForm.EntityCatalog.EntityCatalog;

namespace AspireForm.Tests.Providers;

public sealed class EfDataModuleProviderTests
{
    private sealed class FakeCatalogService : IEntityCatalogService
    {
        public required Catalog Catalog { get; init; }
        public Task<Catalog> ScanAsync(string csprojPath, CancellationToken ct) => Task.FromResult(Catalog);
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            throw new NotSupportedException("MutateAsync should not be called from the provider plan path.");
    }

    private static PlanContext Ctx(JsonObject inputs) =>
        new("data", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    private static Catalog SingleContextCatalog(params Entity[] entities) =>
        new(entities, [new DbContextInfo("AppDbContext", "Demo.Data", "Demo.Data/AppDbContext.cs", entities.Select(e => e.Name).ToList())], []);

    [Fact]
    public void Type_and_kind_are_correct()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        p.Type.Should().Be("ef-data");
        p.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_throws_with_migration_hint_when_legacy_database_input_is_present()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject { ["database"] = "appdb" }));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*0.5.0*projectPath*");
    }

    [Fact]
    public void Plan_throws_when_projectPath_is_missing()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject()));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*projectPath*required*");
    }

    [Fact]
    public void Plan_emits_managed_dbcontext_file_using_entities_from_catalog()
    {
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject { ["projectPath"] = "Demo/Demo.csproj" }));

        plan.FileActions.Should().HaveCount(1);
        var dbContextFile = plan.FileActions[0];
        dbContextFile.OwnershipMode.Should().Be(OwnershipMode.Managed);
        var rendered = dbContextFile.RenderContent();
        rendered.Should().Contain("public class AppDbContext : DbContext");
        rendered.Should().Contain("DbSet<Book> Books");
    }

    [Fact]
    public void Plan_emits_dab_config_when_any_entity_has_DabExpose()
    {
        var attr = new AttributeInstance("AspireForm.Annotations.DabExposeAttribute", [], new Dictionary<string, object?>());
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: [attr]);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject
        {
            ["projectPath"] = "Demo/Demo.csproj",
            ["dependsOn"] = new JsonArray("sql"),
        }));

        plan.FileActions.Should().HaveCount(2);
        var dab = plan.FileActions.Single(f => f.Path.EndsWith("dab-config.json"));
        dab.OwnershipMode.Should().Be(OwnershipMode.Managed);
        dab.RenderContent().Should().Contain("\"Book\":");
        dab.RenderContent().Should().Contain("@env('ConnectionStrings__sql')");
    }

    [Fact]
    public void Plan_skips_dab_config_when_no_entity_has_DabExpose()
    {
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject { ["projectPath"] = "Demo/Demo.csproj" }));
        plan.FileActions.Should().HaveCount(1);
        plan.FileActions[0].Path.Should().EndWith(".cs");
    }

    [Fact]
    public void Plan_throws_when_dbContext_input_does_not_match_any_discovered_context()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject
        {
            ["projectPath"] = "Demo/Demo.csproj",
            ["dbContext"] = "Demo.Data.NotARealContext",
        }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*NotARealContext*");
    }

    [Fact]
    public void Plan_throws_when_multiple_dbcontexts_and_dbContext_not_set()
    {
        var two = new Catalog([],
            [
                new DbContextInfo("A", "X", "X/A.cs", []),
                new DbContextInfo("B", "X", "X/B.cs", []),
            ],
            []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = two });
        var act = () => p.Plan(Ctx(new JsonObject { ["projectPath"] = "X/X.csproj" }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*dbContext*disambiguate*");
    }
}
