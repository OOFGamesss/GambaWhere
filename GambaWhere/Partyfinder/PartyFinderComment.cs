using System;
using System.Text;

namespace GambaWhere.Partyfinder;

/// <summary>
/// Builds the Party Finder recruitment comment for a hosting session.
/// </summary>
public static class PartyFinderComment
{
    public const int MaxByteLength = 192;

    public static string Compose(string? gameType, string? venueName, string? location)
    {
        var game = Sanitise(gameType);
        var venue = Sanitise(venueName);
        var place = StripDataCentre(Sanitise(location));

        if (venue.Equals("No Venue", StringComparison.OrdinalIgnoreCase))
            venue = string.Empty;

        var prefix = string.IsNullOrEmpty(venue) ? game : $"{game} @ {venue}";

        var combined = string.IsNullOrEmpty(place) ? prefix : $"{prefix}  {place}";
        return TruncateToBytes(combined, MaxByteLength);
    }

    private static string StripDataCentre(string location)
    {
        const string separator = " • ";
        var index = location.IndexOf(separator, StringComparison.Ordinal);
        return index >= 0 ? location[(index + separator.Length)..] : location;
    }

    private static string Sanitise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsControl(ch))
                continue;

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }

    private static string TruncateToBytes(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
            return text;

        var result = text;
        while (result.Length > 0 && Encoding.UTF8.GetByteCount(result) > maxBytes)
            result = result[..^1];

        return result.TrimEnd();
    }
}
