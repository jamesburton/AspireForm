using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class JsonObjectMergeTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Mappings_are_deep_merged()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "a": { "x": 1, "y": 2 } }"""),
            Obj("""{ "a": { "y": 20, "z": 30 } }"""));

        result["a"]!["x"]!.GetValue<int>().Should().Be(1);
        result["a"]!["y"]!.GetValue<int>().Should().Be(20);
        result["a"]!["z"]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public void Sequences_are_replaced_wholesale()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "items": [1, 2, 3] }"""),
            Obj("""{ "items": [9] }"""));

        result["items"]!.AsArray().Count.Should().Be(1);
        result["items"]![0]!.GetValue<int>().Should().Be(9);
    }

    [Fact]
    public void Empty_sequence_in_override_replaces_to_empty()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "items": [1, 2] }"""),
            Obj("""{ "items": [] }"""));

        result["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public void Key_absent_from_override_is_preserved_from_base()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "keep": "me", "change": "old" }"""),
            Obj("""{ "change": "new" }"""));

        result["keep"]!.GetValue<string>().Should().Be("me");
        result["change"]!.GetValue<string>().Should().Be("new");
    }

    [Fact]
    public void Explicit_null_in_override_removes_the_key()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "drop": { "nested": true }, "keep": 1 }"""),
            Obj("""{ "drop": null }"""));

        result.ContainsKey("drop").Should().BeFalse();
        result.ContainsKey("keep").Should().BeTrue();
    }

    [Fact]
    public void Merge_does_not_mutate_its_inputs()
    {
        var baseObj = Obj("""{ "a": 1 }""");
        JsonObjectMerge.Merge(baseObj, Obj("""{ "a": 2 }"""));
        baseObj["a"]!.GetValue<int>().Should().Be(1);
    }
}
