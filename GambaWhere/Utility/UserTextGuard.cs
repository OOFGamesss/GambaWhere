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

    public static bool ContainsDisallowedContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (IsAllowedGambaProUrl(text))
            return false;

        foreach (var pattern in DisallowedPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsAllowedGambaProUrl(string text)
    {
        var s = text.Trim();
        string hostAndPath;
        if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            hostAndPath = s[8..];
        else
            return false;

        var slashIdx = hostAndPath.IndexOf('/');
        var host = slashIdx >= 0 ? hostAndPath[..slashIdx] : hostAndPath;

        return host.Equals("gamba.pro", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".gamba.pro", StringComparison.OrdinalIgnoreCase);
    }
}
