using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.EntityCatalog;

/// <summary>Scans a user csproj for EF Core <c>DbContext</c>s and entity classes via Roslyn.</summary>
public sealed class RoslynEntityScanner : IAsyncDisposable
{
    private MSBuildWorkspace? _workspace;
    private string? _projectPath;
    private Project? _project;

    /// <summary>Opens the supplied csproj as a Roslyn <see cref="MSBuildWorkspace"/>. The workspace is cached for subsequent scans against the same path.</summary>
    public async Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();

        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            throw new EntityCatalogException($"Project file not found: '{absolute}'.");
        }

        if (_workspace is null || _projectPath != absolute)
        {
            _workspace?.Dispose();
            _workspace = MSBuildWorkspace.Create();
            _projectPath = absolute;
            _project = await _workspace.OpenProjectAsync(absolute, cancellationToken: ct);
        }
        else
        {
            // Force a fresh re-parse of the existing project's documents.
            _project = _workspace.CurrentSolution.GetProject(_project!.Id);
        }

        var compilation = await _project!.GetCompilationAsync(ct)
            ?? throw new EntityCatalogException("Roslyn returned a null Compilation.");

        var workspaceDiagnostics = _workspace.Diagnostics
            .Select(d => new CatalogDiagnostic(
                MapWorkspaceDiagnosticSeverity(d.Kind),
                d.Message,
                FilePath: null,
                Line: null))
            .ToList();

        var allTypes = CollectAllTypes(compilation.Assembly.GlobalNamespace);
        var contexts = DiscoverDbContexts(allTypes);
        var entityTypes = ClassifyEntities(allTypes, contexts);
        var entities = entityTypes
            .Select(t => BuildEntity(t, entityTypes))
            .ToList();

        return new EntityCatalog(entities, contexts, workspaceDiagnostics);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _workspace?.Dispose();
        _workspace = null;
        return ValueTask.CompletedTask;
    }

    private static string MapWorkspaceDiagnosticSeverity(WorkspaceDiagnosticKind kind) => kind switch
    {
        WorkspaceDiagnosticKind.Failure => "error",
        WorkspaceDiagnosticKind.Warning => "warning",
        _ => "info",
    };

    private static List<INamedTypeSymbol> CollectAllTypes(INamespaceSymbol root)
    {
        var result = new List<INamedTypeSymbol>();
        Walk(root);
        return result;

        void Walk(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
            {
                result.Add(t);
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                Walk(child);
            }
        }
    }

    private static List<DbContextInfo> DiscoverDbContexts(IEnumerable<INamedTypeSymbol> all)
    {
        var contexts = new List<DbContextInfo>();
        foreach (var t in all.Where(IsDbContext))
        {
            var dbSets = t.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.Type is INamedTypeSymbol nt && nt.Name == "DbSet" && nt.TypeArguments.Length == 1)
                .Select(p => ((INamedTypeSymbol)p.Type).TypeArguments[0].Name)
                .ToList();
            contexts.Add(new DbContextInfo(
                Name: t.Name,
                Namespace: t.ContainingNamespace?.ToDisplayString() ?? "",
                FilePath: t.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
                DbSetEntityNames: dbSets));
        }
        return contexts;
    }

    private static bool IsDbContext(INamedTypeSymbol t)
    {
        for (var bt = t.BaseType; bt is not null; bt = bt.BaseType)
        {
            if (bt.Name == "DbContext") return true;
        }
        return false;
    }

    private static HashSet<INamedTypeSymbol> ClassifyEntities(
        IReadOnlyList<INamedTypeSymbol> all,
        IReadOnlyList<DbContextInfo> contexts)
    {
        var byName = all.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var entities = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Seed from DbSet<T> mentions.
        foreach (var c in contexts)
        {
            foreach (var n in c.DbSetEntityNames)
            {
                if (byName.TryGetValue(n, out var sym)) entities.Add(sym);
            }
        }

        // Add anything carrying [Table] or AspireForm.Annotations.* attributes.
        foreach (var t in all)
        {
            if (t.TypeKind != TypeKind.Class) continue;
            if (t.GetAttributes().Any(a => IsRelevantAttribute(a))) entities.Add(t);
        }

        return entities;
    }

    private static bool IsRelevantAttribute(AttributeData a)
    {
        var cls = a.AttributeClass;
        if (cls is null) return false;
        if (cls.Name == "TableAttribute" && cls.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema")
            return true;
        return cls.ContainingNamespace?.ToDisplayString() == "AspireForm.Annotations";
    }

    private static Entity BuildEntity(INamedTypeSymbol symbol, IReadOnlyCollection<INamedTypeSymbol> allEntities)
    {
        var properties = new List<Property>();
        var relationships = new List<Relationship>();
        var entityNames = new HashSet<string>(allEntities.Select(e => e.Name), StringComparer.Ordinal);

        foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.DeclaredAccessibility == Accessibility.Public))
        {
            if (IsCollectionOfEntity(p.Type, entityNames, out var navTarget))
            {
                relationships.Add(new Relationship(
                    Name: p.Name,
                    TargetEntity: navTarget!,
                    Cardinality: RelationshipCardinality.OneToMany,
                    ForeignKeyProperty: null));
            }
            else if (IsScalarEntityRef(p.Type, entityNames, out var refTarget))
            {
                relationships.Add(new Relationship(
                    Name: p.Name,
                    TargetEntity: refTarget!,
                    Cardinality: RelationshipCardinality.ManyToOne,
                    ForeignKeyProperty: null));
            }
            else
            {
                properties.Add(new Property(
                    Name: p.Name,
                    ClrType: p.Type.ToDisplayString(),
                    IsNullable: p.NullableAnnotation == NullableAnnotation.Annotated,
                    IsPrimaryKey: IsLikelyPrimaryKey(p),
                    Attributes: p.GetAttributes().Select(MapAttribute).ToList()));
            }
        }

        return new Entity(
            Name: symbol.Name,
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            FilePath: symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
            Properties: properties,
            Relationships: relationships,
            Attributes: symbol.GetAttributes().Select(MapAttribute).ToList());
    }

    private static bool IsCollectionOfEntity(ITypeSymbol t, HashSet<string> entityNames, out string? entity)
    {
        entity = null;
        if (t is INamedTypeSymbol nt && nt.IsGenericType && nt.TypeArguments.Length == 1)
        {
            var arg = nt.TypeArguments[0];
            if (entityNames.Contains(arg.Name) &&
                (nt.Name is "ICollection" or "IList" or "List" or "IReadOnlyCollection" or "IReadOnlyList" or "IEnumerable" or "HashSet"))
            {
                entity = arg.Name;
                return true;
            }
        }
        return false;
    }

    private static bool IsScalarEntityRef(ITypeSymbol t, HashSet<string> entityNames, out string? entity)
    {
        entity = null;
        if (t is INamedTypeSymbol nt && entityNames.Contains(nt.Name))
        {
            entity = nt.Name;
            return true;
        }
        return false;
    }

    private static bool IsLikelyPrimaryKey(IPropertySymbol p)
    {
        if (p.GetAttributes().Any(a => a.AttributeClass?.Name == "KeyAttribute")) return true;
        return string.Equals(p.Name, "Id", StringComparison.Ordinal)
            || string.Equals(p.Name, p.ContainingType.Name + "Id", StringComparison.Ordinal);
    }

    private static AttributeInstance MapAttribute(AttributeData a)
    {
        var ns = a.AttributeClass?.ContainingNamespace?.ToDisplayString() ?? "";
        var name = a.AttributeClass?.Name ?? "Unknown";
        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var ctorArgs = a.ConstructorArguments.Select(c => (object?)c.Value).ToList();
        var named = a.NamedArguments.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.Value);
        return new AttributeInstance(fullName, ctorArgs, named);
    }
}
