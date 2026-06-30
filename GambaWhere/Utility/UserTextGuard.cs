using System;

namespace GambaWhere.Utility;

/// <summary>
/// Rejects user-entered text that contains URLs or HTML.
/// </summary>
public static class UserTextGuard
{
    private static readonly string[] DisallowedPatterns =
    {
        "http", "https://", "www.", ".com", ".gg", ".net", ".org", ".io", ".tv", "<", ">"
    };

    private static readonly string[] AllowedHosts =
    {
        "gamba.pro", "oofgames.fyi"
    };

    public static bool ContainsDisallowedContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (IsAllowedUrl(text))
            return false;

        foreach (var pattern in DisallowedPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsAllowedUrl(string text)
    {
        var s = text.Trim();
        if (!s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        var hostAndPath = s[8..];
        var slashIdx = hostAndPath.IndexOf('/');
        var host = slashIdx >= 0 ? hostAndPath[..slashIdx] : hostAndPath;

        foreach (var allowed in AllowedHosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
