using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;
using AspireForm.Tests.ApiCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.ApiCatalog;

public sealed class RoslynEndpointMutatorTests
{
    [Fact]
    public async Task CreateEndpoint_writes_new_file_with_ApiEndpoint_attribute()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_create");
        var target = Path.Combine(fix.Root, "Handlers", "BooksHandler.cs");

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEndpoint("GetBooks", "BooksHandler", "/books", "GET", target, "Demo.Handlers"),
            default);

        result.Success.Should().BeTrue();
        result.ChangedFiles.Should().Contain(target);
        File.Exists(target).Should().BeTrue();
        var content = File.ReadAllText(target);
        content.Should().Contain("[ApiEndpoint(\"/books\", \"GET\")]")
            .And.Contain("public static IResult GetBooks")
            .And.Contain("namespace Demo.Handlers");
    }

    [Fact]
    public async Task CreateEndpoint_refuses_to_overwrite_existing_file()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_create_dup");
        var target = fix.AddFile("Handlers/Existing.cs", "// existing");

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEndpoint("GetBooks", "BooksHandler", "/books", "GET", target, "Demo"),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("Refusing to overwrite");
        File.ReadAllText(target).Should().Be("// existing");
    }

    [Fact]
    public async Task AddParameter_appends_typed_parameter_to_method_signature()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_add_param");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books/{id:int}", "GET")]
                public static string GetBook()
                {
                    return "book";
                }
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var __ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);
        catalog.Endpoints.Should().ContainSingle();

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddParameter("GetBook", "BooksHandler", "id", "int"),
            default);

        result.Success.Should().BeTrue();
        var content = File.ReadAllText(result.ChangedFiles[0]);
        content.Should().Contain("int id");
    }

    [Fact]
    public async Task SetAuthPolicy_adds_ApiAuth_attribute_to_method()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_set_auth");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class AdminHandler
            {
                [ApiEndpoint("/admin", "GET")]
                public static string GetAdmin()
                {
                    return "admin";
                }
            }
            """);

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new SetAuthPolicy("GetAdmin", "AdminHandler", "admin"),
            default);

        result.Success.Should().BeTrue();
        var content = File.ReadAllText(result.ChangedFiles[0]);
        content.Should().Contain("[ApiAuth(\"admin\")]");
    }

    [Fact]
    public async Task ExpressionBodied_method_mutation_returns_failure()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_expr_body");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooks() => "books";
            }
            """);

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddParameter("GetBooks", "BooksHandler", "ctx", "HttpContext"),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("Expression-bodied");
    }

    [Fact]
    public async Task DeleteEndpoint_on_sole_method_deletes_file()
    {
        using var fix = new EndpointFixtureProjectBuilder("mut_delete");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooks()
                {
                    return "books";
                }
            }
            """);

        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new DeleteEndpoint("GetBooks", "BooksHandler"),
            default);

        result.Success.Should().BeTrue();
        File.Exists(result.ChangedFiles[0]).Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_returns_failure_when_csproj_does_not_exist()
    {
        var mutator = new RoslynEndpointMutator();
        var result = await mutator.ApplyAsync(
            "does-not-exist.csproj",
            new CreateEndpoint("GetBooks", "BooksHandler", "/books", "GET", "Handlers.cs", "Demo"),
            default);
        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("not found");
    }
}
