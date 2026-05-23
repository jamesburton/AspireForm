using AspireForm.Planning;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class DependencyGraphTests
{
    [Fact]
    public void TopologicallySort_returns_dependencies_before_dependents()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["data"] = ["sql"],
            ["sql"] = [],
        };

        var order = DependencyGraph.TopologicallySort(edges).ToList();

        order.Should().HaveCount(2);
        order.IndexOf("sql").Should().BeLessThan(order.IndexOf("data"));
    }

    [Fact]
    public void TopologicallySort_orders_independent_nodes_alphabetically_for_determinism()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["z"] = [], ["a"] = [], ["m"] = [],
        };

        var order = DependencyGraph.TopologicallySort(edges);
        order.Should().Equal("a", "m", "z");
    }

    [Fact]
    public void TopologicallySort_throws_on_a_cycle_and_names_the_blocks_involved()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["a"] = ["b"],
            ["b"] = ["c"],
            ["c"] = ["a"],
        };

        var act = () => DependencyGraph.TopologicallySort(edges);

        var ex = act.Should().Throw<DependencyCycleException>().Which;
        ex.Cycle.Should().Contain("a").And.Contain("b").And.Contain("c");
    }

    [Fact]
    public void TopologicallySort_throws_on_self_loop()
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>
        {
            ["x"] = ["x"],
        };

        var act = () => DependencyGraph.TopologicallySort(edges);
        act.Should().Throw<DependencyCycleException>();
    }
}
