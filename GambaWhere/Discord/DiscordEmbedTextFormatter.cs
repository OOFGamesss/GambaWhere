using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GambaWhere.Utility;

namespace GambaWhere.Discord;

/// <summary>Plain-text helpers for Discord embed field values, aligned with embeds/builder.py.</summary>
public static partial class DiscordEmbedTextFormatter
{
    private const int DiscordFieldValueLimit = 1024;

    public static string FormatHostCharacter(string characterName)
    {
        var trimmed = characterName.Trim();
        var parts = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{string.Join(' ', parts[..^1])}@{parts[^1]}";

        return trimmed;
    }

    public static string CompactLocationDisplay(string? location)
    {
        var text = string.IsNullOrWhiteSpace(location) ? "Unknown" : location.Trim();
        text = WardRegex().Replace(text, "W$1");
        text = PlotRegex().Replace(text, "P$1");
        return text;
    }

    public static string FormatRules(Dictionary<string, object>? rules)
    {
        if (rules == null || rules.Count == 0)
            return "No rules set.";

        var sb = new StringBuilder();
        foreach (var kv in rules)
        {
            var rawKey = kv.Key;
            var value = kv.Value;
            var key = RuleKeyFormatting.FormatDisplayKey(rawKey);
            var display = FormatRulePrimitive(value, rawKey);
            sb.Append("- **").Append(key).Append(":** ").Append(display).Append('\n');
        }

        var result = sb.ToString().TrimEnd();
        return TruncateField(result);
    }

    private static string FormatRulePrimitive(object value, string rawKey)
    {
        var lowerKey = rawKey.ToLowerInvariant();
        var isOdds = lowerKey.Contains("odds");

        string display = value switch
        {
            bool b => b ? "Yes" : "No",
            int i => FormatNumeric((long)i, isOdds, lowerKey),
            long l => FormatNumeric(l, isOdds, lowerKey),
            float f => FormatFloat(f, isOdds, lowerKey),
            double d => FormatDouble(d, isOdds, lowerKey),
            _ => value.ToString() ?? string.Empty
        };

        if (isOdds && !display.EndsWith('x'))
            display += "x";

        return display;
    }

    private static string FormatNumeric(long value, bool isOdds, string lowerKey)
    {
        var s = value.ToString("N0", CultureInfo.InvariantCulture);
        if (isOdds)
            return !s.EndsWith('x') ? s + "x" : s;
        if (value > 1000)
            s += " gil";

        return s;
    }

    private static string FormatFloat(float value, bool isOdds, string lowerKey)
    {
        if (isOdds)
            return value.ToString("N2", CultureInfo.InvariantCulture) + "x";

        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double value, bool isOdds, string lowerKey)
    {
        if (isOdds)
            return value.ToString("N2", CultureInfo.InvariantCulture) + "x";

        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string TruncateField(string text)
    {
        if (text.Length <= DiscordFieldValueLimit)
            return text;

        return text[..(DiscordFieldValueLimit - 3)] + "...";
    }

    [GeneratedRegex(@"(?i)\bward\s+(\d+)\b", RegexOptions.None)]
    private static partial Regex WardRegex();

    [GeneratedRegex(@"(?i)\bplot\s+(\d+)\b", RegexOptions.None)]
    private static partial Regex PlotRegex();
}
