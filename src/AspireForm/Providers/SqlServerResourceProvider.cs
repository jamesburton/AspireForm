namespace AspireForm.Providers;

/// <summary>Built-in Resource provider for SQL Server. Plan 2 Task 4 implements; this is a stub for Task 2.</summary>
public sealed class SqlServerResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "sqlserver";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context) =>
        throw new NotImplementedException("Implemented in Plan 2 Task 4.");
}
