namespace AspireForm.Aspire;

/// <summary>The single seam through which AspireForm interacts with the official <c>aspire</c> CLI.</summary>
public interface IAspireCli
{
    /// <summary>Returns true when the <c>aspire</c> CLI can be invoked.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the installed <c>aspire</c> CLI version string, or null when it is unavailable.</summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
}
