using System.Text.Json.Nodes;
using AspireForm.Mcp.Tools.Endpoint;
using AspireForm.Tests.ApiCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Endpoint;

public sealed class EndpointToolsReadTests
{
    private static EndpointFixtureProjectBuilder NewFixWithBookEndpoint(string testName)
    {
        var fix = new EndpointFixtureProjectBuilder(testName);
        fix.AddEndpointHandlerFile("Handlers.cs", """
            using AspireForm.Annotations;
            namespace Demo;
            public static class BooksHandler
            {
                [ApiEndpoint("/books", "GET")]
                public static string GetBooks() => "books";
            }
            """);
        return fix;
    }

    [Fact]
    public async Task EndpointListTool_returns_non_error_with_empty_table_for_empty_project()
    {
        using var fix = new EndpointFixtureProjectBuilder("list_empty");
        var tool = new EndpointListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("(no endpoints found)");
    }

    [Fact]
    public async Task EndpointListTool_returns_table_row_for_seeded_endpoint()
    {
        using var fix = NewFixWithBookEndpoint("list_seeded");
        var tool = new EndpointListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("GetBooks").And.Contain("/books");
    }

    [Fact]
    public async Task EndpointListTool_returns_error_when_projectPath_missing()
    {
        var result = await new EndpointListTool(".").ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'projectPath'");
    }

    [Fact]
    public async Task EndpointShowTool_returns_indented_json_for_known_endpoint()
    {
        using var fix = NewFixWithBookEndpoint("show_known");
        var tool = new EndpointShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["methodName"] = "GetBooks", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("\"MethodName\"").And.Contain("GetBooks");
    }

    [Fact]
    public async Task EndpointShowTool_returns_error_for_unknown_endpoint()
    {
        using var fix = NewFixWithBookEndpoint("show_missing");
        var tool = new EndpointShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["methodName"] = "NonExistent", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found");
    }
}
