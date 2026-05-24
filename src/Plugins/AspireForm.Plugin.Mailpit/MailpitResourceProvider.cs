using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Mailpit;

/// <summary>External Resource provider for Mailpit (local SMTP test mail server). Delegates package add to <c>aspire add mailpit</c>; owns the AppHost resource declaration in a managed region.</summary>
public sealed class MailpitResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "mailpit";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var aspireName = context.Inputs["aspireName"]?.GetValue<string>() ?? context.BlockName;
        var withDataVolume = context.Inputs["withDataVolume"]?.GetValue<bool>() ?? false;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            CliActions = [new PlannedCliAction("aspire", ["add", "mailpit"])],
            FileActions =
            [
                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderInner(aspireName, withDataVolume, context.BlockName)),
            ],
        };
    }

    private static string RenderInner(string aspireName, bool withDataVolume, string blockName)
    {
        var sb = new StringBuilder();
        sb.Append("var ").Append(blockName).Append(" = builder.AddMailPit(\"").Append(aspireName).Append("\")");
        if (withDataVolume)
        {
            sb.Append(".WithDataVolume()");
        }

        sb.Append(';');
        return sb.ToString();
    }
}
