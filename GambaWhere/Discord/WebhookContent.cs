using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GambaWhere.Games;
using GambaWhere.Models;
using GambaWhere.Utility;

namespace GambaWhere.Discord;

/// <summary>Builds the content of Discord webhook posts.</summary>
internal static class WebhookProfile
{
    public const string DisplayName = "Gamba Where";

    public const string AvatarImageHttpsUrl =
        "https://raw.githubusercontent.com/OOFGamesss/GambaWhere/main/GambaWhere/Images/Icons/gambawhere.png";

    public const string ActiveEmbedFooterText = "Create by Gamba Where Plogon";
}

internal static class WebhookTheme
{
    public const string IdleBannerFile = "nogambabanner.png";
    public const int IdleColour = 0x95A5A6;

    public static (int Colour, string Emoji, string BannerFile) ResolveForGame(string gameType)
    {
        return GameCategories.Find(gameType) is { } category
            ? (category.DiscordColour, category.Emoji, category.BannerFile)
            : (0x8040C0, "🎲", "minigamesbanner.png");
    }

    public static string BuildTitle(string gameName, string? venueName, string emoji)
    {
        var vn = (venueName ?? string.Empty).Trim();
        var noVenue = vn.Length == 0 || string.Equals(vn, "No Venue", StringComparison.OrdinalIgnoreCase);
        var baseTitle = noVenue ? gameName : $"{gameName} @ {vn}";
        return $"{emoji} {baseTitle} {emoji}";
    }
}

internal static partial class EmbedTextFormatter
{
    private const int DiscordFieldValueLimit = 1024;

    public static string FormatHostCharacter(string characterName)
    {
        var trimmed = characterName.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
            var isOdds = kv.Key.ToLowerInvariant().Contains("odds");
            string display = kv.Value switch
            {
                bool b => b ? "Yes" : "No",
                int i => FormatNumeric(i, isOdds),
                long l => FormatNumeric(l, isOdds),
                float f => FormatDecimal(f, isOdds),
                double d => FormatDecimal(d, isOdds),
                _ => kv.Value.ToString() ?? string.Empty
            };

            if (isOdds && !display.EndsWith('x'))
                display += "x";

            var key = RuleKeyFormatting.FormatDisplayKey(kv.Key);
            sb.Append("- **").Append(key).Append(":** ").Append(display).Append('\n');
        }

        var result = sb.ToString().TrimEnd();
        return result.Length <= DiscordFieldValueLimit ? result : result[..(DiscordFieldValueLimit - 3)] + "...";
    }

    private static string FormatNumeric(long value, bool isOdds)
    {
        var s = value.ToString("N0", CultureInfo.InvariantCulture);
        if (isOdds)
            return s.EndsWith('x') ? s : s + "x";
        if (value > 1000)
            s += " gil";

        return s;
    }

    private static string FormatDecimal(double value, bool isOdds)
    {
        if (isOdds)
            return value.ToString("N2", CultureInfo.InvariantCulture) + "x";

        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"(?i)\bward\s+(\d+)\b", RegexOptions.None)]
    private static partial Regex WardRegex();

    [GeneratedRegex(@"(?i)\bplot\s+(\d+)\b", RegexOptions.None)]
    private static partial Regex PlotRegex();
}

internal static class WebhookPayload
{
    public static DiscordOutboundPayloadDto ForIdle(string bannerFileName, bool applyWebhookProfile)
    {
        return new DiscordOutboundPayloadDto
        {
            Username = applyWebhookProfile ? WebhookProfile.DisplayName : null,
            AvatarUrl = applyWebhookProfile ? WebhookProfile.AvatarImageHttpsUrl : null,
            Embeds =
            [
                new DiscordEmbedDto
                {
                    Description = null,
                    Color = WebhookTheme.IdleColour,
                    Image = new DiscordMediaDto($"attachment://{bannerFileName}"),
                    Footer = null,
                    Thumbnail = null,
                    Title = null,
                    Fields = null
                }
            ],
            Attachments = [new DiscordAttachmentDto { Id = 0, Filename = bannerFileName }]
        };
    }

    public static DiscordOutboundPayloadDto ForActive(
        DiscordSessionSnapshot snapshot,
        (int Colour, string Emoji, string BannerFile) theme,
        string bannerFileName,
        bool applyWebhookProfile)
    {
        var rules = snapshot.Rules != null
            ? snapshot.Rules as Dictionary<string, object> ?? new Dictionary<string, object>(snapshot.Rules!)
            : null;

        var fields = new List<DiscordEmbedFieldDto>
        {
            new() { Name = "Gamba Host", Value = EmbedTextFormatter.FormatHostCharacter(snapshot.CharacterName), Inline = true },
            new() { Name = "Current Location", Value = EmbedTextFormatter.CompactLocationDisplay(snapshot.Location), Inline = false },
            new() { Name = "Game Info", Value = EmbedTextFormatter.FormatRules(rules), Inline = false }
        };

        if (!string.IsNullOrWhiteSpace(snapshot.DiscordUrl))
            fields.Add(new DiscordEmbedFieldDto { Name = "Discord", Value = $"<{snapshot.DiscordUrl}>", Inline = false });

        return new DiscordOutboundPayloadDto
        {
            Username = applyWebhookProfile ? WebhookProfile.DisplayName : null,
            AvatarUrl = applyWebhookProfile ? WebhookProfile.AvatarImageHttpsUrl : null,
            Embeds =
            [
                new DiscordEmbedDto
                {
                    Title = WebhookTheme.BuildTitle(snapshot.GameType, snapshot.VenueName, theme.Emoji),
                    Color = theme.Colour,
                    Fields = fields,
                    Image = new DiscordMediaDto($"attachment://{bannerFileName}"),
                    Footer = new DiscordFooterDto
                    {
                        Text = WebhookProfile.ActiveEmbedFooterText,
                        IconUrl = WebhookProfile.AvatarImageHttpsUrl
                    },
                    Thumbnail = string.IsNullOrWhiteSpace(snapshot.ImageUrl)
                        ? null
                        : new DiscordMediaDto(snapshot.ImageUrl!),
                    Description = null
                }
            ],
            Attachments = [new DiscordAttachmentDto { Id = 0, Filename = bannerFileName }]
        };
    }
}
