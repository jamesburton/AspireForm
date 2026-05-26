using AspireForm.ApiCatalog;
using AspireForm.Tests.ApiCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.ApiCatalog;

public sealed class RoslynEndpointScannerTests
{
    [Fact]
    public async Task ScanAsync_returns_empty_catalog_for_project_with_no_endpoints()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_empty");

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_discovers_single_ApiEndpoint_method_with_correct_route_and_method()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_single");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooks() => "books";
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Endpoints.Should().ContainSingle();
        var ep = catalog.Endpoints[0];
        ep.HandlerTypeName.Should().Be("BooksHandler");
        ep.MethodName.Should().Be("GetBooks");
        ep.Route.Should().Be("/books");
        ep.HttpMethod.Should().Be("GET");
    }

    [Fact]
    public async Task ScanAsync_parses_route_parameter_with_constraint()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_route_param");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books/{id:int}", "GET")]
                public static string GetBook(int id) => id.ToString();
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var ep = catalog.Endpoints.Should().ContainSingle().Which;
        ep.Parameters.Should().ContainSingle()
            .Which.Should().Match<AspireForm.ApiCatalog.RouteParameter>(
                p => p.Name == "id" && p.Constraint == "int" && !p.IsOptional);
    }

    [Fact]
    public async Task ScanAsync_extracts_ApiAuth_policy_from_sibling_attribute()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_auth");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class AdminHandler
            {
                [ApiEndpoint("/admin", "GET")]
                [ApiAuth("admin")]
                public static string GetAdmin() => "admin";
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var ep = catalog.Endpoints.Should().ContainSingle().Which;
        ep.AuthPolicy.Should().Be("admin");
    }

    [Fact]
    public async Task ScanAsync_emits_warning_diagnostic_for_ambiguous_routes()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_ambiguous");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class FirstHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooksA() => "a";
            }
            public static class SecondHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooksB() => "b";
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        // First endpoint wins; second is skipped with a warning.
        catalog.Endpoints.Should().ContainSingle();
        catalog.Diagnostics.Should().Contain(d => d.Severity == "warning" && d.Message.Contains("Ambiguous route"));
    }

    [Fact]
    public async Task ScanAsync_extracts_ApiSummary_and_ApiTag()
    {
        using var fix = new EndpointFixtureProjectBuilder("scan_summary_tag");
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books", "GET")]
                [ApiSummary("Returns all books")]
                [ApiTag("Books")]
                [ApiTag("Public")]
                public static string GetBooks() => "books";
            }
            """);

        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var ep = catalog.Endpoints.Should().ContainSingle().Which;
        ep.Summary.Should().Be("Returns all books");
        ep.Tags.Should().BeEquivalentTo(new[] { "Books", "Public" });
    }

    [Fact]
    public async Task ScanAsync_throws_EndpointCatalogException_when_csproj_does_not_exist()
    {
        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var act = async () => await scanner.ScanAsync("does-not-exist.csproj", default);
        await act.Should().ThrowAsync<EndpointCatalogException>()
            .WithMessage("*not found*");
    }
}
