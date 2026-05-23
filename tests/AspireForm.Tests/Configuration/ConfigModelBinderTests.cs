using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ConfigModelBinderTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    private const string ValidHeader =
        """ "aspireform": { "version": 1, "project": "MyApp", "apphost": "./MyApp.AppHost" } """;

    [Fact]
    public void Binds_header_resources_and_modules()
    {
        var dom = Obj($$"""
            {
              {{ValidHeader}},
              "resources": { "sql": { "type": "sqlserver", "aspireName": "sql" } },
              "modules": { "data": { "type": "ef-data", "dependsOn": ["sql"] } }
            }
            """);

        var model = ConfigModelBinder.Bind(dom);

        model.AspireForm.Project.Should().Be("MyApp");
        model.Resources["sql"].Type.Should().Be("sqlserver");
        model.Resources["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
        model.Modules["data"].DependsOn.Should().ContainSingle().Which.Should().Be("sql");
        model.Modules["data"].PreventDestroy.Should().BeTrue();
    }

    [Fact]
    public void Inputs_exclude_reserved_keys()
    {
        var dom = Obj($$"""
            {
              {{ValidHeader}},
              "modules": { "data": { "type": "ef-data", "dependsOn": ["x"], "preventDestroy": false, "database": "appdb" } },
              "resources": { "x": { "type": "sqlserver" } }
            }
            """);

        var model = ConfigModelBinder.Bind(dom);

        model.Modules["data"].PreventDestroy.Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("type").Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("dependsOn").Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("preventDestroy").Should().BeFalse();
        model.Modules["data"].Inputs["database"]!.GetValue<string>().Should().Be("appdb");
    }

    [Theory]
    [InlineData(""" { "resources": {} } """)]                                                  // no header
    [InlineData(""" { "aspireform": { "version": 2, "project": "X", "apphost": "./X" } } """)]  // bad version
    [InlineData(""" { "aspireform": { "version": 1, "project": "", "apphost": "./X" } } """)]   // empty project
    public void Rejects_invalid_headers(string json)
    {
        var act = () => ConfigModelBinder.Bind(Obj(json));
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Rejects_block_without_a_type()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "resources": { "sql": { "aspireName": "sql" } } }""");
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>().WithMessage("*type*");
    }

    [Fact]
    public void Rejects_dependsOn_referencing_an_unknown_block()
    {
        var dom = Obj($$"""
            { {{ValidHeader}}, "modules": { "data": { "type": "ef-data", "dependsOn": ["ghost"] } } }
            """);
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>().WithMessage("*ghost*");
    }

    [Theory]
    [InlineData("""{ "aspireform": { "version": "1", "project": "X", "apphost": "./X" } }""")]
    [InlineData("""{ "aspireform": { "version": 1, "project": "X", "apphost": "./X" }, "modules": { "data": { "type": "ef-data", "preventDestroy": "no" } } }""")]
    public void Reports_friendly_error_on_type_mismatch(string json)
    {
        var act = () => ConfigModelBinder.Bind(Obj(json));
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Rejects_dependsOn_that_is_not_an_array()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "modules": { "data": { "type": "ef-data", "dependsOn": "sql" } }, "resources": { "sql": { "type": "sqlserver" } } }""");
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>().WithMessage("*dependsOn*");
    }

    [Fact]
    public void Rejects_dependsOn_with_a_non_string_element()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "resources": { "sql": { "type": "sqlserver" } }, "modules": { "data": { "type": "ef-data", "dependsOn": [1] } } }""");
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Profiles_are_captured_raw_without_validation()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "profiles": { "observability": { "anything": true } } }""");
        var model = ConfigModelBinder.Bind(dom);
        model.Profiles.Should().ContainKey("observability");
    }
}
