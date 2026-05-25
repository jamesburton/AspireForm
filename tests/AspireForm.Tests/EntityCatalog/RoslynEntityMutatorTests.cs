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
}
