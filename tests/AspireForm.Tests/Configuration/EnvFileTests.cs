using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class EnvFileTests
{
    [Fact]
    public void Parses_key_value_pairs_ignoring_comments_and_blank_lines()
    {
        const string text = """
            # a comment
            DB_NAME=appdb

            DB_HOST = localhost
            QUOTED="with spaces"
            """;

        var values = EnvFile.Parse(text);

        values["DB_NAME"].Should().Be("appdb");
        values["DB_HOST"].Should().Be("localhost");
        values["QUOTED"].Should().Be("with spaces");
        values.Should().HaveCount(3);
    }

    [Fact]
    public void Ignores_lines_without_an_equals_sign()
    {
        var values = EnvFile.Parse("NOT_A_PAIR\nVALID=1");
        values.Should().ContainKey("VALID").And.NotContainKey("NOT_A_PAIR");
    }
}
