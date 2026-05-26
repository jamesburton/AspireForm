using System.ComponentModel;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AspireForm.Mcp.Tools.Endpoint;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Mcp.Tools.Macros;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>mcp</c> command: starts an MCP server exposing AspireForm's verbs as tools. Defaults to stdio; pass <c>--http --port N</c> for HTTP.</summary>
public sealed class McpCommand : AsyncCommand<McpCommand.Settings>
{
    /// <summary>Options for <c>mcp</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Default project directory used by tool handlers when their args omit <c>projectDir</c>.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("Default project directory for tool calls that omit 'projectDir'.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Use HTTP transport instead of stdio.</summary>
        [CommandOption("--http")]
        [Description("Use HTTP transport (localhost only) instead of stdio.")]
        public bool Http { get; init; }

        /// <summary>Port for the HTTP transport. Ignored unless <c>--http</c> is supplied.</summary>
        [CommandOption("--port <PORT>")]
        [Description("Port for the HTTP transport (default 5050).")]
        public int Port { get; init; } = 5050;
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var registry = BuildRegistry(projectDir);
        var server = new McpServer(registry);
        ITransport transport = settings.Http
            ? new HttpTransport(settings.Port)
            : new StdioTransport();

        try
        {
            await transport.RunAsync(server, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    /// <summary>Builds the registry of all 14 low-level tools, 12 entity tools, 10 endpoint tools, 3 macros, and 3 theme tools (42 total), all bound to <paramref name="projectDir"/> as their default.</summary>
    public static ToolRegistry BuildRegistry(string projectDir)
    {
        var r = new ToolRegistry();

        // Low-level (14).
        r.Register(new ConfigTool(projectDir));
        r.Register(new PlanTool(projectDir));
        r.Register(new ApplyTool(projectDir));
        r.Register(new NewTool(projectDir));
        r.Register(new AddTool(projectDir));
        r.Register(new DestroyTool(projectDir));
        r.Register(new ImportTool(projectDir));
        r.Register(new StateListTool(projectDir));
        r.Register(new StateShowTool(projectDir));
        r.Register(new DoctorTool());
        r.Register(new PluginListTool(projectDir));
        r.Register(new PluginInstallTool(projectDir));
        r.Register(new PluginUpdateTool(projectDir));
        r.Register(new PluginRemoveTool(projectDir));

        // EF model-builder tools (#4a) — 12 fine-grained verbs over EntityCatalog.
        r.Register(new EntityListTool(projectDir));
        r.Register(new EntityShowTool(projectDir));
        r.Register(new DbContextListTool(projectDir));
        r.Register(new EntityCreateTool(projectDir));
        r.Register(new EntityDeleteTool(projectDir));
        r.Register(new PropertyAddTool(projectDir));
        r.Register(new PropertyRemoveTool(projectDir));
        r.Register(new PropertyRenameTool(projectDir));
        r.Register(new AttributeSetTool(projectDir));
        r.Register(new AttributeClearTool(projectDir));
        r.Register(new RelationshipAddTool(projectDir));
        r.Register(new RelationshipRemoveTool(projectDir));

        // API endpoint builder tools (#4b) — 10 fine-grained verbs over ApiCatalog.
        r.Register(new EndpointListTool(projectDir));
        r.Register(new EndpointShowTool(projectDir));
        r.Register(new EndpointCreateTool(projectDir));
        r.Register(new EndpointDeleteTool(projectDir));
        r.Register(new EndpointParameterAddTool(projectDir));
        r.Register(new EndpointParameterRemoveTool(projectDir));
        r.Register(new EndpointAuthSetTool(projectDir));
        r.Register(new EndpointAttributeSetTool(projectDir));
        r.Register(new EndpointAttributeClearTool(projectDir));
        r.Register(new EndpointEmitTool(projectDir));

        // Macros (3).
        r.Register(new ScaffoldAspireAppWithDataTool(projectDir));
        r.Register(new AddCacheLayerTool(projectDir));
        r.Register(new AddAuthenticationTool(projectDir));

        // Theme tools (#5.1, #6.0) — 3 tools: show, list, activate.
        r.Register(new ThemeShowTool(projectDir));
        r.Register(new ThemeListTool(projectDir));
        r.Register(new ThemeActivateTool(projectDir));

        return r;
    }
}
