using System.ComponentModel;
using System.Text.Json;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>state show</c> command: dumps a single block's record as indented JSON.</summary>
public sealed class StateShowCommand : Command<StateShowCommand.Settings>
{
    /// <summary>Options for <c>state show</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The block name to show.</summary>
        [CommandArgument(0, "<BLOCK>")]
        [Description("The block name to show.")]
        public required string BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var state = new StateStore().Load(Path.GetFullPath(settings.ProjectDir));
            if (!state.Blocks.TryGetValue(settings.BlockName, out var block))
            {
                Console.Error.WriteLine($"Block '{settings.BlockName}' is not tracked in state.");
                return 1;
            }

            Console.Out.WriteLine(JsonSerializer.Serialize(block, PrettyOptions));
            return 0;
        }
        catch (StateException ex)
        {
            Console.Error.WriteLine($"State error: {ex.Message}");
            return 1;
        }
    }
}
