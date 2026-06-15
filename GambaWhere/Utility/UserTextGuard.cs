using System;

namespace GambaWhere.Utility;

/// <summary>
/// Rejects user-entered text that contains URLs or HTML, mirroring the API's
/// server-side rule so the client can fail fast before sending.
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

        foreach (var pattern in DisallowedPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
