using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class ProviderRegistryTests
{
    private sealed class FakeProvider(string type) : IProvider
    {
        public string Type => type;
        public BlockKind Kind => BlockKind.Resource;
        public ProviderPlan Plan(PlanContext context) => new();
    }

    [Fact]
    public void Get_returns_the_provider_for_a_known_type()
    {
        var registry = new ProviderRegistry([new FakeProvider("sqlserver")]);
        registry.Get("sqlserver").Type.Should().Be("sqlserver");
    }

    [Fact]
    public void Get_throws_a_clear_error_for_an_unknown_type()
    {
        var registry = new ProviderRegistry([new FakeProvider("sqlserver")]);
        var act = () => registry.Get("ghost");
        act.Should().Throw<ProviderNotFoundException>().WithMessage("*ghost*");
    }

    [Fact]
    public void Constructor_throws_when_two_providers_register_the_same_type()
    {
        var act = () => new ProviderRegistry(
            [new FakeProvider("dupe"), new FakeProvider("dupe")]);
        act.Should().Throw<ArgumentException>().WithMessage("*dupe*");
    }

    [Fact]
    public void Default_registry_contains_the_v1_built_in_providers()
    {
        var registry = ProviderRegistry.Default();
        registry.Get("sqlserver").Should().NotBeNull();
        registry.Get("ef-data").Should().NotBeNull();
    }

    [Fact]
    public void AllProviders_returns_every_registered_provider()
    {
        var registry = new ProviderRegistry([new FakeProvider("a"), new FakeProvider("b")]);
        registry.AllProviders().Select(p => p.Type).Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void Combine_builds_a_new_registry_from_two_provider_lists()
    {
        var defaults = ProviderRegistry.Default();
        var plugins = new IProvider[] { new FakeProvider("custom") };

        var combined = ProviderRegistry.Combine(defaults.AllProviders(), plugins);

        combined.Get("sqlserver").Should().NotBeNull();
        combined.Get("custom").Type.Should().Be("custom");
    }
}
