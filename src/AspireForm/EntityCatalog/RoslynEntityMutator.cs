using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.EntityCatalog;

/// <summary>Roslyn-backed mutator for entity .cs files. Each <see cref="EntityChangeRequest"/> applies transactionally.</summary>
public sealed class RoslynEntityMutator
{
    /// <summary>Applies one mutation request transactionally against the project at <paramref name="csprojPath"/>.</summary>
    public async Task<MutationResult> ApplyAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();
        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            return MutationResult.Fail($"Project file not found: '{absolute}'.", absolute);
        }

        // Buffered writes: path -> new content (null means delete).
        var pending = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new List<CatalogDiagnostic>();

        switch (request)
        {
            case CreateEntity create:
                if (File.Exists(create.FilePath))
                {
                    return MutationResult.Fail($"Refusing to overwrite existing file '{create.FilePath}'.", create.FilePath);
                }
                pending[create.FilePath] = RenderNewEntityFile(create);
                break;

            case DeleteEntity delete:
                using (var ws = MSBuildWorkspace.Create())
                {
                    var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                    var doc = await FindEntityDocumentAsync(project, delete.EntityName, ct);
                    if (doc is null) return MutationResult.Fail($"Entity '{delete.EntityName}' not found.");

                    pending[doc.FilePath!] = null; // delete the file
                    // v1 limitation: DbSet<T> on the DbContext and reverse navigations on other entities
                    // are not pruned automatically — the warning below tells the user to do it manually.
                    // Auto-pruning is a candidate for #4a.1 (would require a second multi-file Roslyn pass).
                    diagnostics.Add(new CatalogDiagnostic("warning",
                        $"Deleted entity '{delete.EntityName}'. DbSet<T> on DbContext + reverse navigations are NOT automatically pruned in this version; remove them manually.",
                        doc.FilePath, null));
                }
                break;

            default:
                return MutationResult.Fail($"Mutation '{request.GetType().Name}' is not implemented yet.");
        }

        return CommitWrites(pending, diagnostics);
    }

    private static MutationResult CommitWrites(IDictionary<string, string?> pending, List<CatalogDiagnostic> diagnostics)
    {
        var changed = new List<string>();
        try
        {
            foreach (var (path, content) in pending)
            {
                if (content is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, content);
                }
                changed.Add(path);
            }
            return new MutationResult(true, changed, diagnostics);
        }
        catch (Exception ex)
        {
            return MutationResult.Fail($"Commit failed after {changed.Count} file(s): {ex.Message}");
        }
    }

    private static async Task<Document?> FindEntityDocumentAsync(Project project, string entityName, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null) return null;
        var sym = compilation.Assembly.GlobalNamespace.GetAllTypes()
            .FirstOrDefault(t => t.TypeKind == TypeKind.Class && t.Name == entityName);
        if (sym is null) return null;
        var path = sym.Locations.FirstOrDefault()?.SourceTree?.FilePath;
        if (path is null) return null;
        return project.Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderNewEntityFile(CreateEntity req) => $$"""
        namespace {{req.Namespace}};

        public sealed class {{req.Name}}
        {
            public int Id { get; set; }
        }
        """;
}

internal static class SymbolWalker
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers()) yield return t;
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var t in child.GetAllTypes()) yield return t;
    }
}
