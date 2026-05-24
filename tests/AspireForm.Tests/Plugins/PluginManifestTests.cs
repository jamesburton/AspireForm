using System.Text.Json;
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginManifestTests
{
    [Fact]
    public void Parses_a_well_formed_manifest()
    {
        const string json = """
            {
              "name": "Redis",
              "version": "0.1.0",
              "minAspireFormVersion": "0.3.0",
              "providers": [
                { "type": "redis", "kind": "resource", "className": "AspireForm.Plugin.Redis.RedisResourceProvider" }
              ]
            }
            """;

        var manifest = PluginManifest.Parse(json);

        manifest.Name.Should().Be("Redis");
        manifest.Version.Should().Be("0.1.0");
        manifest.MinAspireFormVersion.Should().Be("0.3.0");
        manifest.Providers.Should().ContainSingle();
        manifest.Providers[0].Type.Should().Be("redis");
        manifest.Providers[0].Kind.Should().Be("resource");
        manifest.Providers[0].ClassName.Should().Be("AspireForm.Plugin.Redis.RedisResourceProvider");
    }

    [Fact]
    public void Throws_PluginContractException_on_malformed_json()
    {
        var act = () => PluginManifest.Parse("{ not json");
        act.Should().Throw<PluginContractException>();
    }

    [Fact]
    public void Throws_PluginContractException_when_a_required_field_is_missing()
    {
        const string missingName = """
            { "version": "0.1.0", "minAspireFormVersion": "0.3.0", "providers": [] }
            """;
        var act = () => PluginManifest.Parse(missingName);
        act.Should().Throw<PluginContractException>().WithMessage("*name*");
    }
}
