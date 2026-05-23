using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Built-in Resource provider for SQL Server. Delegates package add to <c>aspire add sqlserver</c>; owns the AppHost resource declaration in a managed region.</summary>
public sealed class SqlServerResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "sqlserver";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var aspireName = context.Inputs["aspireName"]?.GetValue<string>() ?? context.BlockName;
        var databases = (context.Inputs["databases"] as JsonArray)?
            .Select(n => n?.GetValue<string>() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToArray() ?? [];

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            CliActions =
            [
                new PlannedCliAction("aspire", ["add", "sqlserver"]),
            ],
            FileActions =
            [
                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderInner(aspireName, databases, context.BlockName)),
            ],
        };
    }

    private static string RenderInner(string aspireName, IReadOnlyList<string> databases, string blockName)
    {
        var sb = new StringBuilder();
        sb.Append("var ").Append(blockName).Append(" = builder.AddSqlServer(\"").Append(aspireName).Append("\");");

        foreach (var db in databases)
        {
            sb.AppendLine();
            sb.Append("var ").Append(blockName).Append('_').Append(db)
              .Append(" = ").Append(blockName).Append(".AddDatabase(\"").Append(db).Append("\");");
        }

        return sb.ToString();
    }
}
