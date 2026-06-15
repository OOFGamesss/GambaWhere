using System;
using System.Text;

namespace GambaWhere.Utility;

/// <summary>Formats rule keys for display and API payloads.</summary>
public static class RuleKeyFormatting
{
    public static string FormatDisplayKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (MatchesKeyIgnoringWhitespace(key.AsSpan(), "5CardCharlie"))
            return "5 Card Charlie";

        return key.AsSpan().IndexOf(' ') < 0
            ? CamelCaseToSpacedTitle(key.AsSpan())
            : key;
    }

    private static bool MatchesKeyIgnoringWhitespace(ReadOnlySpan<char> key, ReadOnlySpan<char> canonical)
    {
        Span<char> buffer = stackalloc char[key.Length];
        var w = 0;
        foreach (var c in key)
        {
            if (!char.IsWhiteSpace(c))
                buffer[w++] = char.ToLowerInvariant(c);
        }

        return buffer[..w].Equals(canonical, StringComparison.OrdinalIgnoreCase);
    }

    private static string CamelCaseToSpacedTitle(ReadOnlySpan<char> camelCase)
    {
        var result = new StringBuilder(camelCase.Length + 8);
        result.Append(char.ToUpperInvariant(camelCase[0]));

        for (var i = 1; i < camelCase.Length; i++)
        {
            if (char.IsUpper(camelCase[i]))
                result.Append(' ');
            result.Append(camelCase[i]);
        }

        return result.ToString();
    }
}
