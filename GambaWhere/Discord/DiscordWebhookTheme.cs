using System;

namespace GambaWhere.Discord;

public static class DiscordWebhookTheme
{
    public const string IdleBannerFile = "nogambabanner.png";
    public const int IdleColour = 0x95A5A6;

    public static (int Colour, string Emoji, string BannerFile) ResolveForGame(string gameType)
    {
        return gameType switch
        {
            "Bingo" => (0xE03030, "\uD83C\uDFB1", "bingobanner.png"),
            "Blackjack" => (0x3060D0, "\uD83C\uDCCF", "blackjackbanner.png"),
            "Chocobo Racing" => (0xF0C030, "\uD83C\uDFC1", "chocoboracingbanner.png"),
            "Mini Games" => (0x30A050, "\uD83C\uDFB2", "minigamesbanner.png"),
            "Poker" => (0x06B6D4, "\u2660\uFE0F", "pokerbanner.png"),
            "Roulette" => (0x8B5CF6, "\uD83D\uDD22", "roulettebanner.png"),
            "Scratchcards" => (0xF97316, "\uD83C\uDFAB", "scratchcardbanner.png"),
            "Spin the Wheel" => (0xEC4899, "\uD83C\uDFA1", "spinthewheelbanner.png"),
            _ => (0x8040C0, "\uD83C\uDFB2", "minigamesbanner.png")
        };
    }

    public static string BuildTitle(string gameName, string? venueName, string emoji)
    {
        var vn = (venueName ?? string.Empty).Trim();
        var noVenue = vn.Length == 0 || string.Equals(vn, "No Venue", StringComparison.OrdinalIgnoreCase);
        var baseTitle = noVenue ? gameName : $"{gameName} @ {vn}";
        return $"{emoji} {baseTitle} {emoji}";
    }
}
