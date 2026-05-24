using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.MagicLink;

/// <summary>External Module provider for passwordless email magic-link authentication.</summary>
public sealed class MagicLinkAuthModuleProvider : IProvider
{
    /// <inheritdoc/>
    public string Type => "auth-magiclink";

    /// <inheritdoc/>
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc/>
    public ProviderPlan Plan(PlanContext context)
    {
        var fromAddress = context.Inputs["fromAddress"]?.GetValue<string>() ?? "noreply@example.com";
        var tokenLifetime = context.Inputs["tokenLifetimeMinutes"]?.GetValue<int>() ?? 15;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "MagicLinkAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, fromAddress, tokenLifetime)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("magiclink"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("magiclink", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string fromAddress, int tokenLifetime) => $$"""
        namespace {{projectName}}.AppHost;

        /// <summary>Magic-link auth scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class MagicLinkAuthSetup
        {
            /// <summary>Email "From" address for outgoing magic-link emails.</summary>
            public const string FromAddress = "{{fromAddress}}";

            /// <summary>Magic-link token lifetime in minutes.</summary>
            public const int TokenLifetimeMinutes = {{tokenLifetime}};

            // TODO: wire IEmailSender + token storage (SQL or Redis) in your service project.
        }
        """;
}
