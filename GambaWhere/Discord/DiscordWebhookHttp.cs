using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.Config;

namespace GambaWhere.Discord;

/// <summary>Low-level HTTP send helpers for delivering Discord webhook requests.</summary>
internal static class DiscordWebhookHttp
{
    internal static async Task<HttpResponseMessage> SendMultipartWithRetriesAsync(
        HttpClient http,
        IPluginLog log,
        DiscordWebhookEntry entry,
        byte[] payloadJson,
        IReadOnlyList<DiscordMultipartFilePart> fileParts,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        if (!DiscordWebhookUrlParser.TryParseDiscordWebhook(entry.Url, out var webhookId, out var token))
            throw new HttpRequestException("Webhook URL parsing failed unexpectedly.");

        var createsNewMessage = string.IsNullOrWhiteSpace(entry.MessageId);

        var uri = createsNewMessage
            ? $"https://discord.com/api/webhooks/{webhookId}/{token}?wait=true"
            : $"https://discord.com/api/webhooks/{webhookId}/" +
              $"{token}/messages/{Uri.EscapeDataString(entry.MessageId!.Trim())}";

        HttpResponseMessage? pending = null;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            pending?.Dispose();

            using var multipart = DiscordWebhookMultipartBuilder.BuildContent(payloadJson, fileParts);
            using var request = new HttpRequestMessage(
                    createsNewMessage ? HttpMethod.Post : HttpMethod.Patch,
                    uri)
                { Content = multipart };

            pending = await http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var status = pending.StatusCode;
            var canRetryLater = attempt + 1 < maxRetries;

            if (status == HttpStatusCode.TooManyRequests && canRetryLater)
            {
                var delay =
                    await DiscordWebhookRateLimit.ResolveDelayAsync(pending, cancellationToken);
                await DiscordWebhookRateLimit.DrainResponseSafelyAsync(pending, cancellationToken);
                pending.Dispose();
                pending = null;
                await Task.Delay(delay, cancellationToken);

                continue;
            }

            if (!createsNewMessage && status == HttpStatusCode.NotFound)
            {
                await DiscordWebhookRateLimit.DrainResponseSafelyAsync(pending, cancellationToken);
                var missingMessageResponse = pending;
                pending = null;

                return missingMessageResponse;
            }

            if (!pending.IsSuccessStatusCode && pending.StatusCode != HttpStatusCode.NoContent
                                              && status >= HttpStatusCode.InternalServerError &&
                                              status != HttpStatusCode.TooManyRequests && canRetryLater)
            {
                await DiscordWebhookRateLimit.DrainResponseSafelyAsync(pending, cancellationToken);
                pending.Dispose();
                pending = null;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                continue;
            }

            if (!pending.IsSuccessStatusCode && pending.StatusCode != HttpStatusCode.NoContent)
            {
                var preview = "(no body)";
                try
                {
                    var txt = await pending.Content.ReadAsStringAsync(cancellationToken);
                    preview = ClampOneLine(txt, 560);
                }
                catch (Exception bx)
                {
                    preview = $"(could not read body: {bx.Message})";
                }

                log.Warning(
                    "Discord webhook HTTP {Verb} rejected: {Status} ({Code}). Snippet={Snippet}",
                    createsNewMessage ? "POST" : "PATCH",
                    status,
                    (int)status,
                    preview);

                var bad = pending;
                pending = null;

                return bad;
            }

            var retained = pending;
            pending = null;

            return retained!;
        }

        pending?.Dispose();

        log.Warning(
            "Discord webhook gave up after {MaxRetries} attempts ({Verb}).",
            maxRetries,
            createsNewMessage ? "POST" : "PATCH");

        throw new HttpRequestException("Discord webhook retries exhausted.");
    }

    private static string ClampOneLine(string raw, int maxLen)
    {
        if (string.IsNullOrEmpty(raw))
            return "(empty)";

        var collapsed = raw.Replace("\r", " ").Replace("\n", " ");
        return collapsed.Length <= maxLen ? collapsed : collapsed[..maxLen] + "…";
    }
}
