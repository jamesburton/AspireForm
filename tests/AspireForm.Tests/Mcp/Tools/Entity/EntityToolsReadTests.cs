using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class EntityToolsReadTests
{
    private static FixtureProjectBuilder NewFixWithBook(string testName)
    {
        var fix = new FixtureProjectBuilder(testName);
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Book> Books { get; set; } = null!; }
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);
        return fix;
    }

    [Fact]
    public async Task EntityListTool_returns_a_table_with_at_least_the_seeded_entity()
    {
        using var fix = NewFixWithBook("read_list");
        var tool = new EntityListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Book").And.Contain("Demo");
    }

    [Fact]
    public async Task EntityListTool_returns_tool_level_error_when_projectPath_missing()
    {
        var result = await new EntityListTool(".").ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'projectPath'");
    }

    [Fact]
    public async Task EntityShowTool_returns_indented_json_for_known_entity()
    {
        using var fix = NewFixWithBook("read_show");
        var tool = new EntityShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["entity"] = "Book", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("\"Name\": \"Book\"").And.Contain("\"Properties\"");
    }

    [Fact]
    public async Task EntityShowTool_returns_tool_level_error_for_unknown_entity()
    {
        using var fix = NewFixWithBook("read_show_missing");
        var tool = new EntityShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["entity"] = "Missing", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found");
    }

    [Fact]
    public async Task DbContextListTool_returns_a_table_with_the_seeded_context()
    {
        using var fix = NewFixWithBook("read_ctxlist");
        var tool = new DbContextListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Ctx").And.Contain("Book");
    }
}
