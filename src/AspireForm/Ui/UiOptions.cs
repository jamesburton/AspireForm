namespace AspireForm.Ui;

/// <summary>Runtime settings for the <c>aspireform ui</c> verb. Injected into Blazor's DI container as a singleton.</summary>
public sealed class UiOptions
{
    /// <summary>The default AspireForm project directory (where <c>aspireform.yaml</c> lives).</summary>
    public required string ProjectDir { get; init; }

    /// <summary>TCP port the Kestrel host binds to.</summary>
    public required int Port { get; init; }

    /// <summary>When true, the host opens the default browser on startup.</summary>
    public bool LaunchBrowser { get; init; } = true;
}
