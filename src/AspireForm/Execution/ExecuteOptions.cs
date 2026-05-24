namespace AspireForm.Execution;

/// <summary>Flags that modify an <see cref="Executor.ApplyAsync"/> invocation.</summary>
public sealed class ExecuteOptions
{
    /// <summary>When true, skip the interactive approval prompt (equivalent to <c>--yes</c>).</summary>
    public bool AutoApprove { get; init; }

    /// <summary>When true, proceed even if <see cref="Planning.FileActionPlan.DriftDetected"/> is set on any file (equivalent to <c>--force-drift</c>).</summary>
    public bool ForceDrift { get; init; }
}
