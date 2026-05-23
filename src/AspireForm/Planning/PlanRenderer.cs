using System.Text;

namespace AspireForm.Planning;

/// <summary>Pretty-prints a <see cref="Plan"/> for the <c>aspireform plan</c> output.</summary>
public static class PlanRenderer
{
    /// <summary>Renders <paramref name="plan"/> as a human-readable, line-oriented string.</summary>
    public static string Render(Plan plan)
    {
        if (!plan.HasChanges && plan.Blocks.Count == 0)
        {
            return "No changes — desired state matches actual state.\n";
        }

        var sb = new StringBuilder();
        var changed = 0;

        foreach (var block in plan.Blocks)
        {
            sb.AppendLine(RenderBlockHeader(block));

            foreach (var cli in block.CliActions)
            {
                sb.Append("    will run: ").Append(cli.Tool).Append(' ').AppendLine(string.Join(' ', cli.Args));
            }

            foreach (var file in block.FileActions)
            {
                sb.AppendLine(RenderFileLine(file));
                if (file.Kind is FileActionKind.Create or FileActionKind.Modify or FileActionKind.Remove
                    && (file.AfterContent is not null || file.BeforeContent is not null))
                {
                    AppendUnifiedDiff(sb, file.BeforeContent ?? string.Empty, file.AfterContent ?? string.Empty);
                }
            }

            if (block.Kind != BlockActionKind.Noop)
            {
                changed++;
            }

            sb.AppendLine();
        }

        sb.Append("Summary: ").Append(changed).AppendLine(" block(s) would change.");
        return sb.ToString();
    }

    private static string RenderBlockHeader(BlockAction block) => block.Kind switch
    {
        BlockActionKind.Create => $"+ {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — CREATE",
        BlockActionKind.Update => $"~ {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — UPDATE",
        BlockActionKind.Delete => $"- {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — DELETE",
        BlockActionKind.Noop => $"  {block.BlockName} ({block.BlockKind.ToString().ToLowerInvariant()}) — no change",
        _ => block.BlockName,
    };

    private static string RenderFileLine(FileActionPlan file)
    {
        var prefix = file.Kind switch
        {
            FileActionKind.Create => "+",
            FileActionKind.Modify => "~",
            FileActionKind.Remove => "-",
            FileActionKind.Skip => " ",
            FileActionKind.DriftBlocked => "!",
            _ => "?",
        };

        var drift = file.DriftDetected ? "  [DRIFT]" : string.Empty;
        return $"    {prefix} {file.Path}  [{file.OwnershipMode.ToString().ToLowerInvariant()}, {file.Kind.ToString().ToLowerInvariant()}]{drift}";
    }

    private static void AppendUnifiedDiff(StringBuilder sb, string before, string after)
    {
        // Minimal line-by-line diff: print removed lines as '-' and added lines as '+'.
        // For Plan 2 a precise unified-diff is overkill; this conveys intent and keeps the
        // renderer self-contained (no external diff library).
        var beforeLines = before.Split('\n');
        var afterLines = after.Split('\n');

        var common = LongestCommonPrefixCount(beforeLines, afterLines);
        var suffix = LongestCommonSuffixCount(beforeLines, afterLines, common);

        for (var i = common; i < beforeLines.Length - suffix; i++)
        {
            sb.Append("        - ").AppendLine(beforeLines[i]);
        }

        for (var i = common; i < afterLines.Length - suffix; i++)
        {
            sb.Append("        + ").AppendLine(afterLines[i]);
        }
    }

    private static int LongestCommonPrefixCount(string[] a, string[] b)
    {
        var max = Math.Min(a.Length, b.Length);
        for (var i = 0; i < max; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return i;
            }
        }

        return max;
    }

    private static int LongestCommonSuffixCount(string[] a, string[] b, int alreadyMatchedPrefix)
    {
        var max = Math.Min(a.Length, b.Length) - alreadyMatchedPrefix;
        var count = 0;
        while (count < max
               && string.Equals(a[^(count + 1)], b[^(count + 1)], StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
