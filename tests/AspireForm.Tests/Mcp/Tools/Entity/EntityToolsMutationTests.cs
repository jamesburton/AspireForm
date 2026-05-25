using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class EntityToolsMutationTests
{
    [Fact]
    public void All_five_tools_have_aspireform_prefix()
    {
        new EntityCreateTool(".").Name.Should().Be("aspireform_entity_create");
        new EntityDeleteTool(".").Name.Should().Be("aspireform_entity_delete");
        new PropertyAddTool(".").Name.Should().Be("aspireform_property_add");
        new PropertyRemoveTool(".").Name.Should().Be("aspireform_property_remove");
        new PropertyRenameTool(".").Name.Should().Be("aspireform_property_rename");
    }

    [Fact]
    public async Task EntityCreateTool_creates_a_new_entity_file()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_create");
        var target = Path.Combine(fix.Root, "Models", "Book.cs");
        var result = await new EntityCreateTool(".").ExecuteAsync(new JsonObject
        {
            ["name"] = "Book",
            ["namespace"] = "Demo.Models",
            ["filePath"] = target,
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.Exists(target).Should().BeTrue();
    }

    [Fact]
    public async Task PropertyAddTool_appends_a_property_to_an_entity()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_propadd");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } }");
        var result = await new PropertyAddTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["name"] = "Title",
            ["clrType"] = "string",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().Contain("Title");
    }

    [Fact]
    public async Task PropertyRemoveTool_strips_the_property()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_proprm");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } public string Title { get; set; } = \"\"; }");
        var result = await new PropertyRemoveTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["property"] = "Title",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().NotContain("Title");
    }

    [Fact]
    public async Task Missing_inputs_return_tool_level_errors_on_each_tool()
    {
        (await new EntityCreateTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new EntityDeleteTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyAddTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyRemoveTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyRenameTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
    }
}
