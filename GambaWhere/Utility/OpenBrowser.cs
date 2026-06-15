using System.Diagnostics;

namespace GambaWhere.Utility;

/// <summary>Opens a URL in the system default browser.</summary>
internal static class OpenBrowser
{
    public static void TryOpen(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url.Trim()) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
