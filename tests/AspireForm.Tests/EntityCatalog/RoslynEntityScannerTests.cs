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

    [Fact]
    public async Task Scan_infers_one_to_many_for_collection_navigation()
    {
        using var fix = new FixtureProjectBuilder("scan_1n");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Author> Authors { get; set; } = null!; public DbSet<Book> Books { get; set; } = null!; }
            public class Author { public int Id { get; set; } public ICollection<Book> Books { get; set; } = new List<Book>(); }
            public class Book { public int Id { get; set; } public Author Author { get; set; } = null!; }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var author = catalog.Entities.Single(e => e.Name == "Author");
        author.Relationships.Should().ContainSingle(r => r.TargetEntity == "Book" && r.Cardinality == RelationshipCardinality.OneToMany);

        var book = catalog.Entities.Single(e => e.Name == "Book");
        book.Relationships.Should().ContainSingle(r => r.TargetEntity == "Author" && r.Cardinality == RelationshipCardinality.ManyToOne);
    }

    [Fact]
    public async Task Scan_infers_one_to_one_for_paired_scalar_navigations()
    {
        using var fix = new FixtureProjectBuilder("scan_11");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<User> Users { get; set; } = null!; public DbSet<Profile> Profiles { get; set; } = null!; }
            public class User { public int Id { get; set; } public Profile? Profile { get; set; } }
            public class Profile { public int Id { get; set; } public User? User { get; set; } }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var user = catalog.Entities.Single(e => e.Name == "User");
        user.Relationships.Single(r => r.TargetEntity == "Profile")
            .Cardinality.Should().Be(RelationshipCardinality.OneToOne);
    }

    [Fact]
    public async Task Scan_maps_property_attributes_with_constructor_args()
    {
        using var fix = new FixtureProjectBuilder("scan_attr");
        fix.AddFile("Models.cs", """
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Demo;
            [Table("books")]
            public class Book
            {
                [Key] public int Id { get; set; }
                [Required, MaxLength(200)] public string Title { get; set; } = "";
            }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);
        var book = catalog.Entities.Single(e => e.Name == "Book");
        book.Attributes.Should().ContainSingle(a => a.FullTypeName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute");

        var title = book.Properties.Single(p => p.Name == "Title");
        title.Attributes.Should().Contain(a => a.FullTypeName.EndsWith("RequiredAttribute"));
        title.Attributes.Should().Contain(a => a.FullTypeName.EndsWith("MaxLengthAttribute") && a.ConstructorArgs.Count == 1 && Equals(a.ConstructorArgs[0], 200));
    }
}
