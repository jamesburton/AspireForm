using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;
using AspireForm.Mcp.Tools.Endpoint;
using AspireForm.Tests.ApiCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Endpoint;

public sealed class EndpointToolsMutationTests
{
    private static EndpointFixtureProjectBuilder NewFixWithStatementBodyEndpoint(string testName)
    {
        var fix = new EndpointFixtureProjectBuilder(testName);
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
        return fix;
    }

    [Fact]
    public async Task EndpointCreateTool_creates_new_file_with_endpoint_method()
    {
        using var fix = new EndpointFixtureProjectBuilder("mcp_create");
        var targetFile = Path.Combine(fix.Root, "Handlers", "AuthorHandler.cs");
        var tool = new EndpointCreateTool(".");
        var result = await tool.ExecuteAsync(new JsonObject
        {
            ["methodName"] = "GetAuthors",
            ["typeName"] = "AuthorHandler",
            ["route"] = "/authors",
            ["projectPath"] = fix.CsprojPath,
            ["filePath"] = targetFile,
            ["namespace"] = "Demo",
        }, default);

        result.IsError.Should().BeFalse();
        File.Exists(targetFile).Should().BeTrue();
        File.ReadAllText(targetFile).Should().Contain("[ApiEndpoint(\"/authors\", \"GET\")]");
    }

    [Fact]
    public async Task EndpointDeleteTool_removes_endpoint_and_confirms_via_scan()
    {
        using var fix = NewFixWithStatementBodyEndpoint("mcp_delete");
        var deleteTool = new EndpointDeleteTool(".");
        var result = await deleteTool.ExecuteAsync(new JsonObject
        {
            ["methodName"] = "GetBooks",
            ["typeName"] = "BooksHandler",
            ["projectPath"] = fix.CsprojPath,
        }, default);

        result.IsError.Should().BeFalse();

        // Confirm endpoint is gone via re-scan.
        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);
        catalog.Endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task EndpointAuthSetTool_adds_ApiAuth_attribute()
    {
        using var fix = NewFixWithStatementBodyEndpoint("mcp_auth_set");
        var tool = new EndpointAuthSetTool(".");
        var result = await tool.ExecuteAsync(new JsonObject
        {
            ["methodName"] = "GetBooks",
            ["typeName"] = "BooksHandler",
            ["policy"] = "readers",
            ["projectPath"] = fix.CsprojPath,
        }, default);

        result.IsError.Should().BeFalse();

        // Confirm auth policy via re-scan.
        var scanner = new RoslynEndpointScanner();
        await using var _ = scanner;
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);
        catalog.Endpoints.Should().ContainSingle().Which.AuthPolicy.Should().Be("readers");
    }

    [Fact]
    public async Task EndpointParameterAddTool_adds_parameter_to_method_signature()
    {
        using var fix = NewFixWithStatementBodyEndpoint("mcp_param_add");
        var tool = new EndpointParameterAddTool(".");
        var result = await tool.ExecuteAsync(new JsonObject
        {
            ["methodName"] = "GetBooks",
            ["typeName"] = "BooksHandler",
            ["paramName"] = "ctx",
            ["clrType"] = "HttpContext",
            ["projectPath"] = fix.CsprojPath,
        }, default);

        result.IsError.Should().BeFalse();
        // Confirm parameter was added by reading the modified file.
        var files = Directory.GetFiles(fix.Root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("_AttributeStubs"));
        var content = files.Select(File.ReadAllText).First(c => c.Contains("GetBooks"));
        content.Should().Contain("HttpContext ctx");
    }

    [Fact]
    public async Task EndpointCreateTool_returns_error_when_required_args_missing()
    {
        var result = await new EndpointCreateTool(".").ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires");
    }
}
