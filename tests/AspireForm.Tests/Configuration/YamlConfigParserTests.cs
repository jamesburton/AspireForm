using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class YamlConfigParserTests
{
    private readonly YamlConfigParser _yaml = new();
    private readonly JsoncConfigParser _jsonc = new();

    [Fact]
    public void Infers_scalar_types_from_unquoted_yaml()
    {
        const string text = """
            aspireform:
              version: 1
              project: MyApp
            enabled: true
            ratio: 1.5
            """;

        var root = _yaml.Parse(text);

        root["aspireform"]!["version"]!.GetValue<int>().Should().Be(1);
        root["aspireform"]!["project"]!.GetValue<string>().Should().Be("MyApp");
        root["enabled"]!.GetValue<bool>().Should().BeTrue();
        root["ratio"]!.GetValue<double>().Should().Be(1.5);
    }

    [Fact]
    public void Converts_sequences_to_json_arrays()
    {
        const string text = """
            databases:
              - appdb
              - reportdb
            """;

        var root = _yaml.Parse(text);
        var dbs = root["databases"]!.AsArray();

        dbs.Count.Should().Be(2);
        dbs[0]!.GetValue<string>().Should().Be("appdb");
    }

    [Fact]
    public void Yaml_and_jsonc_produce_identical_dom_for_equivalent_input()
    {
        const string yaml = """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            resources:
              sql:
                type: sqlserver
                databases: [appdb]
            """;
        const string jsonc = """
            {
              "aspireform": { "version": 1, "project": "MyApp", "apphost": "./MyApp.AppHost" },
              "resources": { "sql": { "type": "sqlserver", "databases": ["appdb"] } }
            }
            """;

        var fromYaml = _yaml.Parse(yaml).ToJsonString();
        var fromJsonc = _jsonc.Parse(jsonc).ToJsonString();

        fromYaml.Should().Be(fromJsonc);
    }

    [Fact]
    public void Throws_ConfigValidationException_when_root_is_a_sequence()
    {
        var act = () => _yaml.Parse("- one\n- two");
        act.Should().Throw<ConfigValidationException>();
    }
}
