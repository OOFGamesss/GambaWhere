using System;
using GambaWhere.Games;

namespace GambaWhere.Discord;

/// <summary>Colour and styling constants for Discord webhook embeds, sourced from the central game catalogue.</summary>
public static class DiscordWebhookTheme
{
    public const string IdleBannerFile = "nogambabanner.png";
    public const int IdleColour = 0x95A5A6;

    public static (int Colour, string Emoji, string BannerFile) ResolveForGame(string gameType)
    {
        return GameCategories.Find(gameType) is { } category
            ? (category.DiscordColour, category.Emoji, category.BannerFile)
            : (0x8040C0, "\uD83C\uDFB2", "minigamesbanner.png");
    }

    public static string BuildTitle(string gameName, string? venueName, string emoji)
    {
        var vn = (venueName ?? string.Empty).Trim();
        var noVenue = vn.Length == 0 || string.Equals(vn, "No Venue", StringComparison.OrdinalIgnoreCase);
        var baseTitle = noVenue ? gameName : $"{gameName} @ {vn}";
        return $"{emoji} {baseTitle} {emoji}";
    }
}
