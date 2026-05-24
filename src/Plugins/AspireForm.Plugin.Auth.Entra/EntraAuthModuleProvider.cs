using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.Entra;

/// <summary>External Module provider for Microsoft Entra External ID OIDC authentication.</summary>
public sealed class EntraAuthModuleProvider : IProvider
{
    /// <inheritdoc/>
    public string Type => "auth-entra";

    /// <inheritdoc/>
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc/>
    public ProviderPlan Plan(PlanContext context)
    {
        var tenantId = context.Inputs["tenantId"]?.GetValue<string>() ?? "<tenant-id>";
        var clientId = context.Inputs["clientId"]?.GetValue<string>() ?? "<client-id>";
        var audience = context.Inputs["audience"]?.GetValue<string>() ?? clientId;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "EntraAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, tenantId, clientId, audience)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("entra"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("entra", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string tenantId, string clientId, string audience) => $$"""
        namespace {{projectName}}.AppHost;

        /// <summary>Entra External ID auth scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class EntraAuthSetup
        {
            /// <summary>Entra tenant id.</summary>
            public const string TenantId = "{{tenantId}}";

            /// <summary>Entra app registration client id.</summary>
            public const string ClientId = "{{clientId}}";

            /// <summary>JWT audience.</summary>
            public const string Audience = "{{audience}}";

            // TODO: wire Microsoft.Identity.Web in your service project.
        }
        """;
}
