using AspireForm.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("aspireform");

    config.AddCommand<ConfigCommand>("config")
        .WithAlias("show")
        .WithDescription("Print the fully merged and interpolated desired-state configuration.");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that AspireForm's prerequisites are installed.");

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show the reconciliation diff between desired and current state.");

    config.AddCommand<NewCommand>("new")
        .WithDescription("Scaffold a new Aspire solution and starter aspireform.yaml.");

    config.AddCommand<AddCommand>("add")
        .WithDescription("Append a Resource (default) or Module block to the AspireForm config file. Comments and original formatting are not preserved.");

    config.AddCommand<ApplyCommand>("apply")
        .WithDescription("Execute the plan after an approval gate.");

    config.AddCommand<DestroyCommand>("destroy")
        .WithDescription("Destroy one block (or all blocks when no argument is supplied).");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Adopt an existing block into AspireForm state (records the block without executing).");

    config.AddCommand<McpCommand>("mcp")
        .WithDescription("Start an MCP server exposing AspireForm's verbs (stdio by default; --http for localhost HTTP).");

    config.AddBranch("state", state =>
    {
        state.SetDescription("Inspect AspireForm's tracked state.");
        state.AddCommand<StateListCommand>("list")
            .WithDescription("List all tracked blocks.");
        state.AddCommand<StateShowCommand>("show")
            .WithDescription("Show one block's tracked state as JSON.");
    });

    config.AddBranch("plugin", plugin =>
    {
        plugin.SetDescription("Manage AspireForm plugins (NuGet plugin packages).");
        plugin.AddCommand<PluginListCommand>("list")
            .WithDescription("List installed plugins.");
        plugin.AddCommand<PluginInstallCommand>("install")
            .WithDescription("Install a plugin by name or package id.");
        plugin.AddCommand<PluginUpdateCommand>("update")
            .WithDescription("Update an installed plugin to the latest version.");
        plugin.AddCommand<PluginRemoveCommand>("remove")
            .WithDescription("Remove a plugin from the lockfile.");
    });
});

return await app.RunAsync(args);
