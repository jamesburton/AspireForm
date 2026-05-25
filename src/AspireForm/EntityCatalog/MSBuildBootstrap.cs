using Microsoft.Build.Locator;

namespace AspireForm.EntityCatalog;

/// <summary>Idempotent <see cref="MSBuildLocator"/> registration. Must be called before opening any <c>MSBuildWorkspace</c>.</summary>
internal static class MSBuildBootstrap
{
    private static readonly Lock LockObj = new();
    private static bool _registered;

    /// <summary>Registers the highest installed MSBuild SDK with the current process, exactly once.</summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (LockObj)
        {
            if (_registered) return;
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
            _registered = true;
        }
    }
}
