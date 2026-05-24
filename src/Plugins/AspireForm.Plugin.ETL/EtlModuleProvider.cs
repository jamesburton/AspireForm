using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.ETL;

/// <summary>External Module provider for CSV/Excel file ETL import (Hangfire-driven directory watcher).</summary>
public sealed class EtlModuleProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "etl";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var watchDirectory = context.Inputs["watchDirectory"]?.GetValue<string>() ?? "./incoming";
        var parsersArr = context.Inputs["parsers"] as JsonArray;
        var parsers = parsersArr is null
            ? new[] { "csv", "excel" }
            : parsersArr.Select(p => p?.GetValue<string>() ?? string.Empty).Where(s => s.Length > 0).ToArray();

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "EtlSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, watchDirectory, parsers)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(watchDirectory, parsers, context.BlockName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string watchDirectory, string[] parsers)
    {
        var parserList = string.Join(", ", parsers.Select(p => $"\"{p}\""));
        return $$"""
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{projectName}}.AppHost;

            /// <summary>ETL setup scaffolded by AspireForm. Copy/adapt into your worker or web project.</summary>
            public static class EtlSetup
            {
                /// <summary>Directory the file watcher polls for new files.</summary>
                public const string WatchDirectory = "{{watchDirectory}}";

                /// <summary>Enabled file format parsers.</summary>
                public static readonly string[] EnabledParsers = [ {{parserList}} ];

                /// <summary>Registers ETL services. Wire CsvHelper / ExcelDataReader + SqlBulkCopy in your worker project.</summary>
                public static IServiceCollection AddEtl(this IServiceCollection services, IConfiguration configuration)
                {
                    // TODO: register IFileWatcher, IImporter, and Hangfire recurring job in your service project.
                    return services;
                }
            }
            """;
    }

    private static string RenderAppHostRegion(string watchDirectory, string[] parsers, string blockName)
    {
        var parserList = string.Join(",", parsers);
        return $"""
            // etl module ({blockName}): watch={watchDirectory}, parsers=[{parserList}].
            // Wire your worker project here (e.g. .WithReference(<db>).WithReference(<hangfire>)).
            // See EtlSetup.cs in this directory for a sample DI registration helper.
            """;
    }
}
