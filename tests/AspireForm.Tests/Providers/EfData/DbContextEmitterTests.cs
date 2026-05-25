using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;
using AwesomeAssertions;
using Xunit;
using Catalog = AspireForm.EntityCatalog.EntityCatalog;

namespace AspireForm.Tests.Providers.EfData;

public sealed class DbContextEmitterTests
{
    private static Catalog CatalogOf(params Entity[] entities) =>
        new(entities, [], []);

    private static Entity SimpleEntity(string name) =>
        new(name, "Demo", $"{name}.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);

    [Fact]
    public void Render_emits_namespace_and_class_header()
    {
        var src = DbContextEmitter.Render("AppDbContext", "Demo.Data", CatalogOf(SimpleEntity("Book")));
        src.Should().Contain("namespace Demo.Data;");
        src.Should().Contain("public class AppDbContext : DbContext");
        src.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
    }

    [Fact]
    public void Render_emits_DbSet_per_entity_pluralised()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Book"), SimpleEntity("Category"), SimpleEntity("Brush")));
        src.Should().Contain("public DbSet<Book> Books { get; set; }");
        src.Should().Contain("public DbSet<Category> Categories { get; set; }");
        src.Should().Contain("public DbSet<Brush> Brushes { get; set; }");
    }

    [Fact]
    public void Render_emits_entities_in_alphabetical_order_for_deterministic_diffs()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Zebra"), SimpleEntity("Apple")));
        var apple = src.IndexOf("Apple", StringComparison.Ordinal);
        var zebra = src.IndexOf("Zebra", StringComparison.Ordinal);
        apple.Should().BeGreaterThan(0);
        zebra.Should().BeGreaterThan(apple);
    }

    [Fact]
    public void Render_omits_OnModelCreating_when_no_fluent_config_required()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Book")));
        src.Should().NotContain("OnModelCreating");
    }
}
