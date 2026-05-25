using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalog = AspireForm.EntityCatalog.EntityCatalog;
using AspireForm.EntityCatalog;

namespace AspireForm.Providers.EfData;

/// <summary>Emits <c>dab-config.json</c> contents from a catalog. Entities carrying <c>[DabExpose]</c> become DAB entities; <c>[DabHidden]</c> overrides.</summary>
public static class DabConfigEmitter
{
    private const string ExposeAttr = "AspireForm.Annotations.DabExposeAttribute";
    private const string HiddenAttr = "AspireForm.Annotations.DabHiddenAttribute";
    private const string PathAttr = "AspireForm.Annotations.DabPathAttribute";
    private const string PermissionAttr = "AspireForm.Annotations.DabPermissionAttribute";
    private const string RestOnlyAttr = "AspireForm.Annotations.DabRestOnlyAttribute";
    private const string GraphqlOnlyAttr = "AspireForm.Annotations.DabGraphqlOnlyAttribute";
    private const string TableAttr = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";

    /// <summary>Renders the <c>dab-config.json</c> file contents. Returns null when no exposed entities are present.</summary>
    public static string? Render(Catalog catalog, string databaseConnectionName, List<CatalogDiagnostic> diagnostics)
    {
        var exposedEntities = catalog.Entities
            .Where(e => e.Attributes.Any(a => a.FullTypeName == ExposeAttr)
                     && !e.Attributes.Any(a => a.FullTypeName == HiddenAttr))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        if (exposedEntities.Count == 0) return null;

        var entities = new JsonObject();
        foreach (var e in exposedEntities)
        {
            entities[e.Name] = BuildEntityNode(e, diagnostics);
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json",
            ["data-source"] = new JsonObject
            {
                ["database-type"] = "mssql",
                ["connection-string"] = $"@env('ConnectionStrings__{databaseConnectionName}')",
            },
            ["runtime"] = new JsonObject
            {
                ["rest"] = new JsonObject { ["enabled"] = true, ["path"] = "/api" },
                ["graphql"] = new JsonObject { ["enabled"] = true, ["path"] = "/graphql" },
            },
            ["entities"] = entities,
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            // Prevent single quotes in @env(...) tokens from being escaped as '.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";
    }

    private static JsonNode BuildEntityNode(Entity e, List<CatalogDiagnostic> diagnostics)
    {
        var tableAttr = e.Attributes.FirstOrDefault(a => a.FullTypeName == TableAttr);
        var source = tableAttr?.ConstructorArgs.FirstOrDefault() as string ?? $"dbo.{e.Name}";

        var pathAttr = e.Attributes.FirstOrDefault(a => a.FullTypeName == PathAttr);
        var restPath = pathAttr?.ConstructorArgs.FirstOrDefault() as string ?? $"/{e.Name.ToLowerInvariant()}";

        var restOnly = e.Attributes.Any(a => a.FullTypeName == RestOnlyAttr);
        var graphqlOnly = e.Attributes.Any(a => a.FullTypeName == GraphqlOnlyAttr);

        var permissions = e.Attributes
            .Where(a => a.FullTypeName == PermissionAttr)
            .ToList();

        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permArr = new JsonArray();
        foreach (var p in permissions)
        {
            var role = p.ConstructorArgs.ElementAtOrDefault(0) as string ?? "";
            var actions = p.ConstructorArgs.ElementAtOrDefault(1) as string ?? "*";
            if (!seenRoles.Add(role))
            {
                diagnostics.Add(new CatalogDiagnostic("warning",
                    $"Entity '{e.Name}' declares multiple [DabPermission] for role '{role}'. Last-wins applied.",
                    e.FilePath, null));
                for (int i = permArr.Count - 1; i >= 0; i--)
                {
                    if (permArr[i]!["role"]?.GetValue<string>() == role)
                    {
                        permArr.RemoveAt(i);
                        break;
                    }
                }
            }
            permArr.Add(new JsonObject
            {
                ["role"] = role,
                ["actions"] = new JsonArray(actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(a => (JsonNode)JsonValue.Create(a)!)
                    .ToArray()),
            });
        }

        if (permArr.Count == 0)
        {
            permArr.Add(new JsonObject { ["role"] = "anonymous", ["actions"] = new JsonArray("read") });
        }

        var node = new JsonObject
        {
            ["source"] = source,
            ["permissions"] = permArr,
        };
        if (!graphqlOnly)
        {
            node["rest"] = new JsonObject { ["path"] = restPath };
        }
        if (restOnly)
        {
            node["graphql"] = false;
        }

        if (e.Relationships.Count > 0)
        {
            var rels = new JsonObject();
            foreach (var r in e.Relationships.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                rels[r.Name] = new JsonObject
                {
                    ["target.entity"] = r.TargetEntity,
                    ["cardinality"] = r.Cardinality is RelationshipCardinality.OneToMany or RelationshipCardinality.ManyToMany ? "many" : "one",
                };
            }
            if (rels.Count > 0) node["relationships"] = rels;
        }

        return node;
    }
}
