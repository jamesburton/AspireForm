using System.ComponentModel;
using System.Text;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>state list</c> command: prints a one-line summary of every tracked block.</summary>
public sealed class StateListCommand : Command<StateListCommand.Settings>
{
    /// <summary>Options for <c>state list</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var state = new StateStore().Load(Path.GetFullPath(settings.ProjectDir));
            if (state.Blocks.Count == 0)
            {
                Console.Out.WriteLine("No tracked blocks.");
                return 0;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Block        Kind      Type          Files");
            sb.AppendLine("-----        ----      ----          -----");
            foreach (var (name, block) in state.Blocks.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"{Pad(name, 12)} {Pad(block.Kind, 9)} {Pad(block.Type, 13)} {block.Files.Count}");
            }

            Console.Out.Write(sb.ToString());
            return 0;
        }
        catch (StateException ex)
        {
            Console.Error.WriteLine($"State error: {ex.Message}");
            return 1;
        }

        static string Pad(string s, int width) => s.PadRight(width)[..Math.Max(width, s.Length)];
    }
}
