using System.Collections.Generic;

namespace GambaWhere.Discord;

/// <summary>Builds Discord webhook payloads from a session snapshot.</summary>
internal static class DiscordWebhookPayloadFactory
{
    internal static DiscordOutboundPayloadDto ForIdleBanner(
        string bannerFileName,
        bool applyWebhookProfile)
    {
        var attachments = new List<DiscordAttachmentDto>
        {
            new() { Id = 0, Filename = bannerFileName }
        };

        string? username = null;
        string? avatarUrl = null;

        if (applyWebhookProfile)
        {
            username = DiscordWebhookProfile.DisplayName;
            avatarUrl = DiscordWebhookProfile.AvatarImageHttpsUrl;
        }

        return new DiscordOutboundPayloadDto
        {
            Username = username,
            AvatarUrl = avatarUrl,
            Embeds =
            [
                new DiscordEmbedDto
                {
                    Description = null,
                    Color = DiscordWebhookTheme.IdleColour,
                    Image = new DiscordMediaDto($"attachment://{bannerFileName}"),
                    Footer = null,
                    Thumbnail = null,
                    Title = null,
                    Fields = null
                }
            ],
            Attachments = attachments
        };
    }

    internal static DiscordOutboundPayloadDto ForActive(
        DiscordSessionSnapshot snapshot,
        (int Colour, string Emoji, string BannerFile) theme,
        string bannerFileName,
        bool applyWebhookProfile)
    {
        var title = DiscordWebhookTheme.BuildTitle(snapshot.GameType, snapshot.VenueName, theme.Emoji);
        var rulesDict = snapshot.Rules != null
            ? snapshot.Rules as Dictionary<string, object> ?? new Dictionary<string, object>(snapshot.Rules!)
            : null;

        var fields = new List<DiscordEmbedFieldDto>
        {
            new()
            {
                Name = "Gamba Host",
                Value = DiscordEmbedTextFormatter.FormatHostCharacter(snapshot.CharacterName),
                Inline = true
            },
            new()
            {
                Name = "Current Location",
                Value = DiscordEmbedTextFormatter.CompactLocationDisplay(snapshot.Location),
                Inline = false
            },
            new()
            {
                Name = "Game Info",
                Value = DiscordEmbedTextFormatter.FormatRules(rulesDict),
                Inline = false
            }
        };

        if (!string.IsNullOrWhiteSpace(snapshot.DiscordUrl))
        {
            fields.Add(new DiscordEmbedFieldDto
            {
                Name = "Discord",
                Value = $"<{snapshot.DiscordUrl}>",
                Inline = false
            });
        }

        var attachments = new List<DiscordAttachmentDto>
        {
            new() { Id = 0, Filename = bannerFileName }
        };

        string? username = null;
        string? avatarUrl = null;

        if (applyWebhookProfile)
        {
            username = DiscordWebhookProfile.DisplayName;
            avatarUrl = DiscordWebhookProfile.AvatarImageHttpsUrl;
        }

        return new DiscordOutboundPayloadDto
        {
            Username = username,
            AvatarUrl = avatarUrl,
            Embeds =
            [
                new DiscordEmbedDto
                {
                    Title = title,
                    Color = theme.Colour,
                    Fields = fields,
                    Image = new DiscordMediaDto($"attachment://{bannerFileName}"),
                    Footer =
                        new DiscordFooterDto
                        {
                            Text = DiscordWebhookProfile.ActiveEmbedFooterText,
                            IconUrl = DiscordWebhookProfile.AvatarImageHttpsUrl
                        },
                    Thumbnail = string.IsNullOrWhiteSpace(snapshot.ImageUrl)
                        ? null
                        : new DiscordMediaDto(snapshot.ImageUrl!),
                    Description = null
                }
            ],
            Attachments = attachments
        };
    }
}
