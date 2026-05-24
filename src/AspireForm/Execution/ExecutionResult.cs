using AspireForm.State;

namespace AspireForm.Execution;

/// <summary>The aggregate outcome of an <see cref="Executor"/> run.</summary>
public sealed class ExecutionResult
{
    /// <summary>True when every applicable block was applied without error.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable error description; null when <see cref="Success"/> is true.</summary>
    public string? FailureMessage { get; init; }

    /// <summary>Number of blocks the executor processed successfully.</summary>
    public int BlocksApplied { get; init; }

    /// <summary>Number of blocks the executor encountered a failure on (0 on a clean run).</summary>
    public int BlocksFailed { get; init; }

    /// <summary>The state the executor persisted (matches what is on disk after the run).</summary>
    public required AspireFormState NewState { get; init; }
}
