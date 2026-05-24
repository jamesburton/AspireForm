using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Hangfire;

/// <summary>External Module provider for Hangfire background jobs.</summary>
public sealed class HangfireModuleProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "hangfire";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var storage = context.Inputs["storage"]?.GetValue<string>() ?? "sql";
        var dashboardPath = context.Inputs["dashboardPath"]?.GetValue<string>() ?? "/hangfire";

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var scaffoldFile = Path.Combine(context.AppHostDirectory, "HangfireSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: scaffoldFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetupFile(context.ProjectName, storage)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(storage, dashboardPath, context.BlockName)),
            ],
        };
    }

    private static string RenderSetupFile(string projectName, string storage)
    {
        var storageBlock = storage switch
        {
            "redis" => """
                services.AddHangfire(cfg => cfg.UseRedisStorage(configuration.GetConnectionString(connectionName)));
                """,
            _ => """
                services.AddHangfire(cfg => cfg.UseSqlServerStorage(configuration.GetConnectionString(connectionName)));
                """,
        };

        return $$"""
            using Hangfire;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{projectName}}.AppHost;

            /// <summary>Hangfire setup scaffolded by AspireForm. Copy/adapt into your worker or web project.</summary>
            public static class HangfireSetup
            {
                /// <summary>Registers Hangfire with the configured storage. Call <c>services.AddHangfireServer()</c> in your worker project.</summary>
                public static IServiceCollection AddHangfireWithStorage(
                    this IServiceCollection services, IConfiguration configuration, string connectionName)
                {
                    {{storageBlock}}
                    return services;
                }
            }
            """;
    }

    private static string RenderAppHostRegion(string storage, string dashboardPath, string blockName)
    {
        return $"""
            // hangfire module ({blockName}): storage={storage}, dashboard={dashboardPath}.
            // Wire your worker / web project here (e.g. .WithReference({storage})).
            // See HangfireSetup.cs in this directory for a sample DI registration helper.
            """;
    }
}
