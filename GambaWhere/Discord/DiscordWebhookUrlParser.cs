using System;

namespace GambaWhere.Discord;

/// <summary>Parses and validates Discord webhook URLs.</summary>
public static class DiscordWebhookUrlParser
{
    public static bool TryParseDiscordWebhook(string raw, out string webhookId, out string token)
    {
        webhookId = string.Empty;
        token = string.Empty;
        var t = raw?.Trim();
        if (string.IsNullOrWhiteSpace(t))
            return false;

        if (!Uri.TryCreate(t, UriKind.Absolute, out var uri))
            return false;

        if (!IsDiscordWebhookHost(uri.Host) || string.IsNullOrEmpty(uri.AbsolutePath))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var idx = Array.IndexOf(segments, "webhooks");
        if (idx < 0 || idx + 2 >= segments.Length)
            return false;

        webhookId = segments[idx + 1];
        var rawToken = segments[idx + 2];
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var tokenPiece = Uri.UnescapeDataString(rawToken.Split('?')[0]);
        token = Uri.UnescapeDataString(tokenPiece.Split('/')[0]).Trim();

        return webhookId.Length > 0 && token.Length > 0;
    }

    private static bool IsDiscordWebhookHost(string host)
    {
        if (string.IsNullOrEmpty(host))
            return false;

        return host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("canary.discord.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("ptb.discord.com", StringComparison.OrdinalIgnoreCase);
    }
}
