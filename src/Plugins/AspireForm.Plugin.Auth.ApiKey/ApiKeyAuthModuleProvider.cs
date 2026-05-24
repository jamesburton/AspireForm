using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.ApiKey;

/// <summary>External Module provider for API-key authentication.</summary>
public sealed class ApiKeyAuthModuleProvider : IProvider
{
    /// <inheritdoc/>
    public string Type => "auth-apikey";

    /// <inheritdoc/>
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc/>
    public ProviderPlan Plan(PlanContext context)
    {
        var headerName = context.Inputs["headerName"]?.GetValue<string>() ?? "X-API-Key";
        var keysSource = context.Inputs["keysSource"]?.GetValue<string>() ?? "config";

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "ApiKeyAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, headerName, keysSource)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("apikey"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("apikey", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string headerName, string keysSource) => $$"""
        using Microsoft.AspNetCore.Authentication;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;

        namespace {{projectName}}.AppHost;

        /// <summary>API-key auth setup scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class ApiKeyAuthSetup
        {
            /// <summary>The HTTP header name carrying the API key.</summary>
            public const string HeaderName = "{{headerName}}";

            /// <summary>The configured source for valid keys (<c>config</c> or <c>db</c>).</summary>
            public const string KeysSource = "{{keysSource}}";

            /// <summary>Registers API-key auth services. Wire your own AuthenticationHandler in your service project.</summary>
            public static IServiceCollection AddApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
            {
                // TODO: wire your ApiKeyAuthenticationHandler here. KeysSource = "{{keysSource}}".
                return services;
            }
        }
        """;
}
