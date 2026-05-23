namespace AspireForm.Planning;

/// <summary>Raised when a dependency graph contains a cycle.</summary>
public sealed class DependencyCycleException : Exception
{
    /// <summary>The block names participating in the cycle, in traversal order.</summary>
    public IReadOnlyList<string> Cycle { get; }

    /// <summary>Initialises the exception with the offending cycle.</summary>
    public DependencyCycleException(IReadOnlyList<string> cycle)
        : base($"Dependency cycle detected: {string.Join(" → ", cycle)}.")
    {
        Cycle = cycle;
    }
}

/// <summary>Pure utility for topologically sorting block dependency graphs.</summary>
public static class DependencyGraph
{
    /// <summary>
    /// Returns a deterministic topological sort of the nodes in <paramref name="edges"/>.
    /// Dependencies precede dependents; ties are broken by ordinal string comparison of the
    /// node name. Throws <see cref="DependencyCycleException"/> on any cycle (including self-loops).
    /// </summary>
    public static IReadOnlyList<string> TopologicallySort(
        IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        var result = new List<string>(edges.Count);

        foreach (var node in edges.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            Visit(node);
        }

        return result;

        void Visit(string node)
        {
            if (visited.Contains(node))
            {
                return;
            }

            if (!stack.Add(node))
            {
                var cycleStart = path.IndexOf(node);
                var cycle = path.Skip(cycleStart).Append(node).ToList();
                throw new DependencyCycleException(cycle);
            }

            path.Add(node);

            if (edges.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps.OrderBy(d => d, StringComparer.Ordinal))
                {
                    Visit(dep);
                }
            }

            stack.Remove(node);
            path.RemoveAt(path.Count - 1);
            visited.Add(node);
            result.Add(node);
        }
    }
}
