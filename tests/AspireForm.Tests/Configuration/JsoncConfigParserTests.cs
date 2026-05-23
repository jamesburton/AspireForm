using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class JsoncConfigParserTests
{
    private readonly JsoncConfigParser _parser = new();

    [Fact]
    public void Parses_object_with_line_and_block_comments_and_trailing_commas()
    {
        const string text = """
            {
              // a line comment
              "aspireform": { "version": 1, "project": "MyApp", },
              /* block comment */
              "resources": {}
            }
            """;

        var root = _parser.Parse(text);

        root["aspireform"]!["version"]!.GetValue<int>().Should().Be(1);
        root["aspireform"]!["project"]!.GetValue<string>().Should().Be("MyApp");
    }

    [Fact]
    public void Throws_ConfigValidationException_when_root_is_not_an_object()
    {
        var act = () => _parser.Parse("[1, 2, 3]");
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Throws_ConfigValidationException_on_malformed_json()
    {
        var act = () => _parser.Parse("{ not json");
        act.Should().Throw<ConfigValidationException>();
    }
}
