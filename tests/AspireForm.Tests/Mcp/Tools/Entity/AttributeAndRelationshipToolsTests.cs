using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class AttributeAndRelationshipToolsTests
{
    [Fact]
    public void All_four_tools_have_aspireform_prefix()
    {
        new AttributeSetTool(".").Name.Should().Be("aspireform_attribute_set");
        new AttributeClearTool(".").Name.Should().Be("aspireform_attribute_clear");
        new RelationshipAddTool(".").Name.Should().Be("aspireform_relationship_add");
        new RelationshipRemoveTool(".").Name.Should().Be("aspireform_relationship_remove");
    }

    [Fact]
    public async Task AttributeSetTool_sets_class_level_attribute_with_constructor_args()
    {
        using var fix = new FixtureProjectBuilder("attr_set");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } }");
        var result = await new AttributeSetTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["attributeFullName"] = "AspireForm.Annotations.DabPermissionAttribute",
            ["ctorArgs"] = new JsonArray("anonymous", "read"),
            ["projectPath"] = fix.CsprojPath,
        }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeFalse();
        var src = File.ReadAllText(bookFile);
        src.Should().Contain("DabPermission").And.Contain("\"anonymous\"").And.Contain("\"read\"");
    }

    [Fact]
    public async Task AttributeClearTool_removes_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("attr_clear");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            [AspireForm.Annotations.DabExpose]
            public class Book { public int Id { get; set; } }
            """);
        var result = await new AttributeClearTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["attributeFullName"] = "AspireForm.Annotations.DabExposeAttribute",
            ["projectPath"] = fix.CsprojPath,
        }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().NotContain("DabExpose");
    }

    [Fact]
    public async Task RelationshipAddTool_OneToMany_adds_nav_on_both_sides()
    {
        using var fix = new FixtureProjectBuilder("rel_add");
        var modelsFile = fix.AddFile("Models.cs", """
            namespace Demo;
            public class Author { public int Id { get; set; } }
            public class Book { public int Id { get; set; } }
            """);
        var result = await new RelationshipAddTool(".").ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "Author",
            ["toEntity"] = "Book",
            ["cardinality"] = "OneToMany",
            ["projectPath"] = fix.CsprojPath,
        }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeFalse();
        File.ReadAllText(modelsFile).Should().Contain("ICollection<Book>");
    }

    [Fact]
    public async Task RelationshipAddTool_rejects_unknown_cardinality()
    {
        var tool = new RelationshipAddTool(".");
        var result = await tool.ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "A",
            ["toEntity"] = "B",
            ["cardinality"] = "Bogus",
            ["projectPath"] = "x.csproj",
        }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("Unknown cardinality 'Bogus'");
    }

    [Fact]
    public async Task RelationshipAddTool_rejects_ManyToMany_in_v1()
    {
        using var fix = new FixtureProjectBuilder("rel_add_m2m");
        fix.AddFile("Models.cs", "namespace Demo; public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");
        var result = await new RelationshipAddTool(".").ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "A",
            ["toEntity"] = "B",
            ["cardinality"] = "ManyToMany",
            ["projectPath"] = fix.CsprojPath,
        }, TestContext.Current.CancellationToken);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ManyToMany");
    }

    [Fact]
    public async Task Missing_inputs_return_tool_level_errors_on_each_tool()
    {
        (await new AttributeSetTool(".").ExecuteAsync(new JsonObject(), TestContext.Current.CancellationToken)).IsError.Should().BeTrue();
        (await new AttributeClearTool(".").ExecuteAsync(new JsonObject(), TestContext.Current.CancellationToken)).IsError.Should().BeTrue();
        (await new RelationshipAddTool(".").ExecuteAsync(new JsonObject(), TestContext.Current.CancellationToken)).IsError.Should().BeTrue();
        (await new RelationshipRemoveTool(".").ExecuteAsync(new JsonObject(), TestContext.Current.CancellationToken)).IsError.Should().BeTrue();
    }
}
