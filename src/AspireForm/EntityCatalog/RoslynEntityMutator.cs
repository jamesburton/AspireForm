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

            case AddProperty add:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, add.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{add.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == add.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{add.EntityName}' not found in {doc.FilePath}.");

                var prop = RenderProperty(add.Property);
                var newClass = classNode.AddMembers(prop);
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.NormalizeWhitespace().ToFullString();
                break;
            }

            case RemoveProperty remove:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, remove.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{remove.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == remove.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{remove.EntityName}' not found.");

                var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == remove.PropertyName);
                if (propNode is null) return MutationResult.Fail($"Property '{remove.PropertyName}' not found on '{remove.EntityName}'.");

                var newClass = classNode.RemoveNode(propNode, SyntaxRemoveOptions.KeepNoTrivia)!;
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case RenameProperty rename:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var compilation = await project.GetCompilationAsync(ct);
                var entitySym = compilation!.Assembly.GlobalNamespace.GetAllTypes()
                    .FirstOrDefault(t => t.Name == rename.EntityName);
                if (entitySym is null) return MutationResult.Fail($"Entity '{rename.EntityName}' not found.");

                var propSym = entitySym.GetMembers().OfType<IPropertySymbol>()
                    .FirstOrDefault(p => p.Name == rename.OldName);
                if (propSym is null) return MutationResult.Fail($"Property '{rename.OldName}' not found on '{rename.EntityName}'.");

                var newSolution = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(
                    project.Solution, propSym, new Microsoft.CodeAnalysis.Rename.SymbolRenameOptions(), rename.NewName, ct);

                // Stage all changed documents in pending.
                var changes = newSolution.GetChanges(project.Solution);
                foreach (var projChange in changes.GetProjectChanges())
                {
                    foreach (var docId in projChange.GetChangedDocuments())
                    {
                        var newDoc = newSolution.GetDocument(docId)!;
                        var text = await newDoc.GetTextAsync(ct);
                        pending[newDoc.FilePath!] = text.ToString();
                    }
                }
                if (pending.Count == 0)
                {
                    diagnostics.Add(new CatalogDiagnostic("warning",
                        $"Rename produced no file changes — '{rename.OldName}' may already be '{rename.NewName}' or symbol resolution failed.",
                        null, null));
                }
                break;
            }

            case SetAttribute set:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, set.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{set.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == set.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{set.EntityName}' not found.");

                var attrText = RenderAttribute(set.Attribute);
                var attrList = (AttributeListSyntax)SyntaxFactory.ParseCompilationUnit($"{attrText}\nclass X {{}}")
                    .DescendantNodes().OfType<AttributeListSyntax>().First();

                SyntaxNode newRoot;
                if (set.PropertyName is null)
                {
                    var clearedClass = WithoutAttribute(classNode, set.Attribute.FullTypeName);
                    var newClass = clearedClass.WithAttributeLists(clearedClass.AttributeLists.Add(attrList));
                    newRoot = root.ReplaceNode(classNode, newClass);
                }
                else
                {
                    var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == set.PropertyName);
                    if (propNode is null) return MutationResult.Fail($"Property '{set.PropertyName}' not found.");
                    var clearedProp = WithoutAttribute(propNode, set.Attribute.FullTypeName);
                    var newProp = clearedProp.WithAttributeLists(clearedProp.AttributeLists.Add(attrList));
                    newRoot = root.ReplaceNode(propNode, newProp);
                }

                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case ClearAttribute clear:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, clear.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{clear.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == clear.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{clear.EntityName}' not found.");

                SyntaxNode newRoot;
                if (clear.PropertyName is null)
                {
                    newRoot = root.ReplaceNode(classNode, WithoutAttribute(classNode, clear.AttributeFullTypeName));
                }
                else
                {
                    var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == clear.PropertyName);
                    if (propNode is null) return MutationResult.Fail($"Property '{clear.PropertyName}' not found.");
                    newRoot = root.ReplaceNode(propNode, WithoutAttribute(propNode, clear.AttributeFullTypeName));
                }
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case AddRelationship rel:
            {
                if (rel.Cardinality == RelationshipCardinality.ManyToMany)
                {
                    return MutationResult.Fail("ManyToMany relationships are not supported in v1 (deferred to #4a.1).");
                }

                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var fromDoc = await FindEntityDocumentAsync(project, rel.FromEntity, ct);
                var toDoc = await FindEntityDocumentAsync(project, rel.ToEntity, ct);
                if (fromDoc is null) return MutationResult.Fail($"Entity '{rel.FromEntity}' not found.");
                if (toDoc is null) return MutationResult.Fail($"Entity '{rel.ToEntity}' not found.");

                if (!string.Equals(fromDoc.FilePath, toDoc.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    pending[fromDoc.FilePath!] = await AddRelationshipToFromAsync(fromDoc, rel, ct);
                    pending[toDoc.FilePath!] = await AddRelationshipToToAsync(toDoc, rel, ct);
                }
                else
                {
                    pending[fromDoc.FilePath!] = await AddBothSidesInOneFileAsync(fromDoc, rel, ct);
                }
                break;
            }

            case RemoveRelationship rrm:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, rrm.FromEntity, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{rrm.FromEntity}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == rrm.FromEntity);
                if (classNode is null) return MutationResult.Fail($"Class '{rrm.FromEntity}' not found.");

                var navProp = classNode.Members.OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == rrm.RelationshipName);
                if (navProp is null) return MutationResult.Fail($"Relationship '{rrm.RelationshipName}' not found.");

                var newClass = classNode.RemoveNode(navProp, SyntaxRemoveOptions.KeepNoTrivia)!;
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.ToFullString();
                diagnostics.Add(new CatalogDiagnostic("warning",
                    "Removed navigation property only. FK property + reverse navigation (if any) must be removed manually in v1.",
                    doc.FilePath, null));
                break;
            }

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

    private static PropertyDeclarationSyntax RenderProperty(Property p)
    {
        var typeName = p.IsNullable && !p.ClrType.EndsWith("?")
            ? p.ClrType + "?"
            : p.ClrType;
        var src = $"public {typeName} {p.Name} {{ get; set; }}";
        return (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(src)!;
    }

    private static string RenderAttribute(AttributeInstance a)
    {
        var shortName = a.FullTypeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];
        var args = new List<string>();
        foreach (var ctor in a.ConstructorArgs)
            args.Add(FormatLiteral(ctor));
        foreach (var (k, v) in a.NamedArgs)
            args.Add($"{k} = {FormatLiteral(v)}");
        var body = args.Count == 0 ? "" : $"({string.Join(", ", args)})";
        return $"[{shortName}{body}]";
    }

    private static string FormatLiteral(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s.Replace("\"", "\\\"")}\"",
        bool b => b ? "true" : "false",
        char c => $"'{c}'",
        _ => v.ToString() ?? "null",
    };

    private static TNode WithoutAttribute<TNode>(TNode node, string attributeFullTypeName) where TNode : SyntaxNode
    {
        var shortName = attributeFullTypeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];

        var listsToRewrite = node.DescendantNodes().OfType<AttributeListSyntax>()
            .Where(al => al.Parent == node).ToList();

        foreach (var list in listsToRewrite)
        {
            var keep = list.Attributes.Where(a => a.Name.ToString().Split('.').Last() != shortName
                                               && a.Name.ToString().Split('.').Last() != shortName + "Attribute").ToList();
            if (keep.Count == list.Attributes.Count) continue;

            if (keep.Count == 0)
            {
                node = node.RemoveNode(list, SyntaxRemoveOptions.KeepNoTrivia)!;
            }
            else
            {
                var newList = list.WithAttributes(SyntaxFactory.SeparatedList(keep));
                node = node.ReplaceNode(list, newList);
            }
        }
        return node;
    }

    private static async Task<string> AddRelationshipToFromAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        var tree = await doc.GetSyntaxTreeAsync(ct);
        var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
        var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.FromEntity);

        var navType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? $"System.Collections.Generic.ICollection<{rel.ToEntity}>"
            : rel.ToEntity;
        var navInit = rel.Cardinality == RelationshipCardinality.OneToMany
            ? $" = new System.Collections.Generic.List<{rel.ToEntity}>();"
            : "";
        var fkLine = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $"public int {rel.ForeignKeyProperty ?? rel.ToEntity + "Id"} {{ get; set; }}"
            : null;

        var members = new List<MemberDeclarationSyntax>();
        if (fkLine is not null)
            members.Add((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(fkLine)!);
        members.Add((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {navType} {rel.ToEntity} {{ get; set; }}{navInit}")!);

        var newClass = classNode.AddMembers(members.ToArray());
        return root.ReplaceNode(classNode, newClass).NormalizeWhitespace().ToFullString();
    }

    private static async Task<string> AddRelationshipToToAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        var tree = await doc.GetSyntaxTreeAsync(ct);
        var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
        var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.ToEntity);

        var reverseType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? rel.FromEntity
            : rel.Cardinality == RelationshipCardinality.ManyToOne
                ? $"System.Collections.Generic.ICollection<{rel.FromEntity}>"
                : rel.FromEntity;
        var reverseInit = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $" = new System.Collections.Generic.List<{rel.FromEntity}>();"
            : "";

        var member = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {reverseType} {rel.FromEntity} {{ get; set; }}{reverseInit}")!;
        var newClass = classNode.AddMembers(member);
        return root.ReplaceNode(classNode, newClass).NormalizeWhitespace().ToFullString();
    }

    private static async Task<string> AddBothSidesInOneFileAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        var firstPass = await AddRelationshipToFromAsync(doc, rel, ct);
        var tree = CSharpSyntaxTree.ParseText(firstPass);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var toClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.ToEntity);
        var reverseType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? rel.FromEntity
            : rel.Cardinality == RelationshipCardinality.ManyToOne
                ? $"System.Collections.Generic.ICollection<{rel.FromEntity}>"
                : rel.FromEntity;
        var reverseInit = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $" = new System.Collections.Generic.List<{rel.FromEntity}>();"
            : "";
        var member = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {reverseType} {rel.FromEntity} {{ get; set; }}{reverseInit}")!;
        var newToClass = toClass.AddMembers(member);
        return root.ReplaceNode(toClass, newToClass).NormalizeWhitespace().ToFullString();
    }
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
