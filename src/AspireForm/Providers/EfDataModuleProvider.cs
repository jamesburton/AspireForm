namespace AspireForm.Providers;

/// <summary>Built-in Module provider for EF Core data access. Plan 2 Task 5 implements; this is a stub for Task 2.</summary>
public sealed class EfDataModuleProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "ef-data";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context) =>
        throw new NotImplementedException("Implemented in Plan 2 Task 5.");
}
