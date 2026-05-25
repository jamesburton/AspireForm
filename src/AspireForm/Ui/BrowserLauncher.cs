using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AspireForm.Ui;

/// <summary>Opens a URL in the user's default browser.</summary>
internal static class BrowserLauncher
{
    /// <summary>Best-effort launch — failures are swallowed (the URL is still printed to stdout by the host).</summary>
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch { /* best effort */ }
    }
}
