using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class ScriptDirectiveParserTests
{
    [Fact]
    public void Parses_package_directive_with_version()
    {
        const string source = """
            #:package Newtonsoft.Json@13.0.3
            using System;
            """;

        var directives = ScriptDirectiveParser.Parse(source).ToList();

        directives.Should().ContainSingle();
        directives[0].Kind.Should().Be(ScriptDirectiveKind.Package);
        directives[0].PackageId.Should().Be("Newtonsoft.Json");
        directives[0].Version.Should().Be("13.0.3");
    }

    [Fact]
    public void Parses_package_directive_without_version_as_floating()
    {
        var directives = ScriptDirectiveParser.Parse("#:package SomeLib").ToList();
        directives.Should().ContainSingle();
        directives[0].PackageId.Should().Be("SomeLib");
        directives[0].Version.Should().Be("*");
    }

    [Fact]
    public void Stops_parsing_at_first_non_directive_line()
    {
        const string source = """
            #:package A@1.0.0
            // a comment
            #:package B@2.0.0
            using System;
            """;

        var directives = ScriptDirectiveParser.Parse(source).ToList();
        directives.Should().ContainSingle();
        directives[0].PackageId.Should().Be("A");
    }

    [Fact]
    public void Ignores_blank_lines_at_the_top()
    {
        const string source = """


            #:package A@1.0.0
            """;
        ScriptDirectiveParser.Parse(source).Should().ContainSingle();
    }

    [Fact]
    public void Returns_empty_for_a_source_with_no_directives()
    {
        ScriptDirectiveParser.Parse("using System;\nclass X { }").Should().BeEmpty();
    }
}
