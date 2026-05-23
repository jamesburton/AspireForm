using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class InterpolatorTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Substitutes_a_known_variable_in_string_values()
    {
        var dom = Obj("""{ "project": "${NAME}" }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string> { ["NAME"] = "MyApp" });
        result["project"]!.GetValue<string>().Should().Be("MyApp");
    }

    [Fact]
    public void Uses_default_when_variable_is_undefined()
    {
        var dom = Obj("""{ "host": "${DB_HOST:-localhost}" }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string>());
        result["host"]!.GetValue<string>().Should().Be("localhost");
    }

    [Fact]
    public void Throws_when_variable_is_undefined_and_has_no_default()
    {
        var dom = Obj("""{ "host": "${MISSING}" }""");
        var act = () => Interpolator.Apply(dom, new Dictionary<string, string>());
        act.Should().Throw<ConfigValidationException>().WithMessage("*MISSING*");
    }

    [Fact]
    public void Does_not_touch_numbers_or_booleans()
    {
        var dom = Obj("""{ "version": 1, "enabled": true }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string>());
        result["version"]!.GetValue<int>().Should().Be(1);
        result["enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Interpolates_inside_nested_objects_and_arrays()
    {
        var dom = Obj("""{ "a": { "b": "${V}" }, "c": ["${V}"] }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string> { ["V"] = "x" });
        result["a"]!["b"]!.GetValue<string>().Should().Be("x");
        result["c"]![0]!.GetValue<string>().Should().Be("x");
    }
}
