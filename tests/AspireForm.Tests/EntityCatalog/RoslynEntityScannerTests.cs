using AspireForm.EntityCatalog;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class RoslynEntityScannerTests
{
    [Fact]
    public async Task Scan_finds_dbcontext_and_its_dbset_entity_types()
    {
        using var fix = new FixtureProjectBuilder("scan_dbset");
        fix.AddFile("DbContextStub.cs", """
            namespace Microsoft.EntityFrameworkCore;
            public class DbContext { }
            public class DbSet<T> { }
            """);
        fix.AddFile("AppDbContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class AppDbContext : DbContext
            {
                public DbSet<Book> Books { get; set; } = null!;
            }
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.DbContexts.Should().ContainSingle()
            .Which.Name.Should().Be("AppDbContext");
        catalog.Entities.Should().ContainSingle(e => e.Name == "Book");
    }

    [Fact]
    public async Task Scan_classifies_class_with_Table_attribute_as_entity_even_without_dbset()
    {
        using var fix = new FixtureProjectBuilder("scan_tableattr");
        fix.AddFile("Models.cs", """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Demo;
            [Table("authors")]
            public class Author { public int Id { get; set; } }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Entities.Should().ContainSingle(e => e.Name == "Author");
    }

    [Fact]
    public async Task Scan_includes_workspace_diagnostics_without_failing()
    {
        using var fix = new FixtureProjectBuilder("scan_nomodels");
        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Entities.Should().BeEmpty();
        catalog.DbContexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_throws_when_csproj_does_not_exist()
    {
        await using var scanner = new RoslynEntityScanner();
        var act = async () => await scanner.ScanAsync("does-not-exist.csproj", default);
        await act.Should().ThrowAsync<EntityCatalogException>();
    }
}
