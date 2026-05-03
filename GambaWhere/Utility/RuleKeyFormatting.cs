using System;
using System.Text;

namespace GambaWhere.Utility;

public static class RuleKeyFormatting
{
    private const string PayingTwoPointFiveCharlieId = "payingTwoPointFiveCharlie";
    private const string CharliePayoutDisplay = "Paying x2.5 for Charlie";

    public static string FormatDisplayKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (MatchesPayingTwoPointFiveCharlieKey(key.AsSpan()))
            return CharliePayoutDisplay;

        return key.AsSpan().IndexOf(' ') < 0
            ? CamelCaseToSpacedTitle(key.AsSpan())
            : key;
    }

    private static bool MatchesPayingTwoPointFiveCharlieKey(ReadOnlySpan<char> key)
    {
        Span<char> buffer = stackalloc char[key.Length];
        var w = 0;
        foreach (var c in key)
        {
            if (!char.IsWhiteSpace(c))
                buffer[w++] = c;
        }

        return buffer[..w].Equals(PayingTwoPointFiveCharlieId.AsSpan(), StringComparison.OrdinalIgnoreCase);
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
