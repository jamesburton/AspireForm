using AspireForm.EntityCatalog;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class RoslynEntityMutatorTests
{
    [Fact]
    public async Task CreateEntity_writes_a_new_file_with_a_skeleton_class()
    {
        using var fix = new FixtureProjectBuilder("mut_create");
        var target = Path.Combine(fix.Root, "Models", "Book.cs");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEntity("Book", "Demo.Models", target),
            default);

        result.Success.Should().BeTrue();
        result.ChangedFiles.Should().Contain(target);
        File.Exists(target).Should().BeTrue();
        var content = File.ReadAllText(target);
        content.Should().Contain("namespace Demo.Models;").And.Contain("public sealed class Book").And.Contain("public int Id { get; set; }");
    }

    [Fact]
    public async Task CreateEntity_refuses_to_overwrite_existing_file()
    {
        using var fix = new FixtureProjectBuilder("mut_create_dup");
        var target = fix.AddFile("Models/Book.cs", "// existing");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEntity("Book", "Demo.Models", target),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("Refusing to overwrite");
        File.ReadAllText(target).Should().Be("// existing");
    }

    [Fact]
    public async Task DeleteEntity_removes_the_source_file_and_warns_about_unpruned_refs()
    {
        using var fix = new FixtureProjectBuilder("mut_delete");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        var bookFile = fix.AddFile("Book.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Book> Books { get; set; } = null!; }
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new DeleteEntity("Book"),
            default);

        result.Success.Should().BeTrue();
        File.Exists(bookFile).Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Severity == "warning" && d.Message.Contains("NOT automatically pruned"));
    }

    [Fact]
    public async Task ApplyAsync_returns_failure_when_csproj_does_not_exist()
    {
        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            "does-not-exist.csproj",
            new CreateEntity("X", "Demo", "X.cs"),
            default);
        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task AddProperty_appends_a_property_to_the_class()
    {
        using var fix = new FixtureProjectBuilder("mut_addprop");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddProperty("Book", new Property("Title", "string", IsNullable: false, IsPrimaryKey: false, Attributes: [])),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("public string Title");
        updated.Should().Contain("public int Id");
    }

    [Fact]
    public async Task RemoveProperty_strips_the_property_declaration()
    {
        using var fix = new FixtureProjectBuilder("mut_rmprop");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new RemoveProperty("Book", "Title"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().NotContain("Title");
        updated.Should().Contain("public int Id");
    }

    [Fact]
    public async Task RenameProperty_renames_declarations_via_symbol_rename()
    {
        using var fix = new FixtureProjectBuilder("mut_rename");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } public string Name { get; set; } = ""; }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new RenameProperty("Book", "Name", "Title"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("Title");
        updated.Should().NotContain("Name");
    }

    [Fact]
    public async Task SetAttribute_adds_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("mut_setattr");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } }");

        var mutator = new RoslynEntityMutator();
        var attr = new AttributeInstance("AspireForm.Annotations.DabExposeAttribute", [], new Dictionary<string, object?>());
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new SetAttribute("Book", null, attr),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("[DabExpose]");
    }

    [Fact]
    public async Task ClearAttribute_removes_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("mut_clearattr");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            [AspireForm.Annotations.DabExpose]
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new ClearAttribute("Book", null, "AspireForm.Annotations.DabExposeAttribute"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().NotContain("DabExpose");
    }

    [Fact]
    public async Task AddRelationship_OneToMany_adds_collection_on_from_and_scalar_back_on_to()
    {
        using var fix = new FixtureProjectBuilder("mut_addrel");
        var modelsFile = fix.AddFile("Models.cs", """
            namespace Demo;
            public class Author { public int Id { get; set; } }
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddRelationship("Author", "Book", RelationshipCardinality.OneToMany, null),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(modelsFile);
        updated.Should().Contain("ICollection<Book> Book");
        updated.Should().Contain("Author Author");
    }

    [Fact]
    public async Task AddRelationship_ManyToMany_returns_failure_in_v1()
    {
        using var fix = new FixtureProjectBuilder("mut_addrel_m2m");
        fix.AddFile("Models.cs", "namespace Demo; public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddRelationship("A", "B", RelationshipCardinality.ManyToMany, null),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("ManyToMany");
    }
}
