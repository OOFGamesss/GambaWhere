using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public const string IdleBannerFile = "nogamba.png";
    public const int IdleColour = 0x95A5A6;

    public static (int Colour, string Emoji, string BannerUrl) ResolveForGame(string gameType)
    {
        return GameCategories.Find(gameType) is { } category
            ? (category.DiscordColour, category.Emoji, category.BannerUrl)
            : (0x8040C0, "🎲", string.Empty);
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
    public static DiscordOutboundPayloadDto ForIdle(string? bannerFileName, string? bannerUrl, bool applyWebhookProfile)
    {
        var imageUrl = !string.IsNullOrWhiteSpace(bannerFileName)
            ? $"attachment://{bannerFileName}"
            : bannerUrl;

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
                    Image = string.IsNullOrWhiteSpace(imageUrl) ? null : new DiscordMediaDto(imageUrl!),
                    Footer = null,
                    Thumbnail = null,
                    Title = null,
                    Fields = null
                }
            ],
            Attachments = string.IsNullOrWhiteSpace(bannerFileName)
                ? []
                : [new DiscordAttachmentDto { Id = 0, Filename = bannerFileName! }]
        };
    }

    public static DiscordOutboundPayloadDto ForActive(
        IReadOnlyList<DiscordSessionSnapshot> snapshots,
        (int Colour, string Emoji, string BannerUrl) theme,
        string? bannerFileName,
        bool applyWebhookProfile)
    {
        var lead = snapshots[0];

        var fields = new List<DiscordEmbedFieldDto>
        {
            new() { Name = "Gamba Host", Value = EmbedTextFormatter.FormatHostCharacter(lead.CharacterName), Inline = true },
            new() { Name = "Current Location", Value = EmbedTextFormatter.CompactLocationDisplay(lead.Location), Inline = false }
        };

        foreach (var snapshot in snapshots)
        {
            var rules = snapshot.Rules != null
                ? snapshot.Rules as Dictionary<string, object> ?? new Dictionary<string, object>(snapshot.Rules!)
                : null;

            var heading = snapshots.Count == 1
                ? "Game Info"
                : WebhookTheme.BuildTitle(snapshot.GameType, snapshot.VenueName, WebhookTheme.ResolveForGame(snapshot.GameType).Emoji);

            fields.Add(new DiscordEmbedFieldDto
            {
                Name = heading,
                Value = EmbedTextFormatter.FormatRules(rules),
                Inline = false
            });
        }

        foreach (var discordUrl in snapshots
                     .Select(s => s.DiscordUrl)
                     .Where(url => !string.IsNullOrWhiteSpace(url))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            fields.Add(new DiscordEmbedFieldDto { Name = "Discord", Value = $"<{discordUrl}>", Inline = false });
        }

        var imageUrl = !string.IsNullOrWhiteSpace(bannerFileName)
            ? $"attachment://{bannerFileName}"
            : (!string.IsNullOrWhiteSpace(theme.BannerUrl) ? theme.BannerUrl : null);

        return new DiscordOutboundPayloadDto
        {
            Username = applyWebhookProfile ? WebhookProfile.DisplayName : null,
            AvatarUrl = applyWebhookProfile ? WebhookProfile.AvatarImageHttpsUrl : null,
            Embeds =
            [
                new DiscordEmbedDto
                {
                    Title = BuildActiveTitle(snapshots, theme.Emoji),
                    Color = theme.Colour,
                    Fields = fields,
                    Image = string.IsNullOrWhiteSpace(imageUrl) ? null : new DiscordMediaDto(imageUrl!),
                    Footer = new DiscordFooterDto
                    {
                        Text = WebhookProfile.ActiveEmbedFooterText,
                        IconUrl = WebhookProfile.AvatarImageHttpsUrl
                    },
                    Thumbnail = string.IsNullOrWhiteSpace(lead.ImageUrl)
                        ? null
                        : new DiscordMediaDto(lead.ImageUrl!),
                    Description = null
                }
            ],
            Attachments = string.IsNullOrWhiteSpace(bannerFileName)
                ? []
                : [new DiscordAttachmentDto { Id = 0, Filename = bannerFileName! }]
        };
    }

    private static string BuildActiveTitle(IReadOnlyList<DiscordSessionSnapshot> snapshots, string emoji)
    {
        var lead = snapshots[0];
        if (snapshots.Count == 1)
            return WebhookTheme.BuildTitle(lead.GameType, lead.VenueName, emoji);

        return $"{emoji} Hosting {snapshots.Count} Games {emoji}";
    }
}
