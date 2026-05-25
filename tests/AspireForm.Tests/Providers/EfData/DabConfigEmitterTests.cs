using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;
using AwesomeAssertions;
using Xunit;
using Catalog = AspireForm.EntityCatalog.EntityCatalog;

namespace AspireForm.Tests.Providers.EfData;

public sealed class DabConfigEmitterTests
{
    private static Entity EntityWithAttrs(string name, params AttributeInstance[] attrs) =>
        new(name, "Demo", $"{name}.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: attrs);

    private static AttributeInstance Attr(string fullName, params object[] ctorArgs) =>
        new(fullName, ctorArgs, new Dictionary<string, object?>());

    [Fact]
    public void Render_returns_null_when_no_entities_are_exposed()
    {
        var catalog = new Catalog([EntityWithAttrs("Book")], [], []);
        DabConfigEmitter.Render(catalog, "sql", new()).Should().BeNull();
    }

    [Fact]
    public void Render_emits_exposed_entity_with_default_anonymous_read_permission()
    {
        var catalog = new Catalog(
            [EntityWithAttrs("Book", Attr("AspireForm.Annotations.DabExposeAttribute"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        var node = (JsonObject)JsonNode.Parse(json)!;
        node["entities"]!["Book"]!["source"]!.GetValue<string>().Should().Be("dbo.Book");
        node["entities"]!["Book"]!["permissions"]![0]!["role"]!.GetValue<string>().Should().Be("anonymous");
        node["entities"]!["Book"]!["rest"]!["path"]!.GetValue<string>().Should().Be("/book");
    }

    [Fact]
    public void Render_honors_DabPath_override()
    {
        var catalog = new Catalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabPathAttribute", "/books"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        json.Should().Contain("\"path\": \"/books\"");
    }

    [Fact]
    public void Render_honors_DabHidden_to_suppress_an_otherwise_exposed_entity()
    {
        var catalog = new Catalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabHiddenAttribute"))],
            [], []);
        DabConfigEmitter.Render(catalog, "sql", new()).Should().BeNull();
    }

    [Fact]
    public void Render_uses_Table_attribute_value_for_source()
    {
        var catalog = new Catalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("System.ComponentModel.DataAnnotations.Schema.TableAttribute", "library.books"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        json.Should().Contain("\"source\": \"library.books\"");
    }

    [Fact]
    public void Render_emits_last_wins_permission_warning_for_duplicate_roles()
    {
        var diagnostics = new List<CatalogDiagnostic>();
        var catalog = new Catalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabPermissionAttribute", "anonymous", "read"),
                Attr("AspireForm.Annotations.DabPermissionAttribute", "anonymous", "*"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", diagnostics)!;
        diagnostics.Should().ContainSingle(d => d.Severity == "warning" && d.Message.Contains("anonymous"));
        json.Should().Contain("\"*\"");
    }

    [Fact]
    public void Render_includes_connection_string_token_with_supplied_name()
    {
        var catalog = new Catalog(
            [EntityWithAttrs("Book", Attr("AspireForm.Annotations.DabExposeAttribute"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "mydb", new())!;
        json.Should().Contain("@env('ConnectionStrings__mydb')");
    }
}
