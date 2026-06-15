using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GambaWhere.Discord;

/// <summary>Tracks and honours Discord webhook rate-limit responses.</summary>
internal static class DiscordWebhookRateLimit
{
    internal static async Task<TimeSpan> ResolveDelayAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fromHeader = TryReadRetryAfter(response.Headers);
        if (fromHeader.HasValue)
            return ClampDelay(fromHeader.Value);

        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(json);

            foreach (var name in new[] { "retry_after", "retryAfter" })
            {
                if (!doc.RootElement.TryGetProperty(name, out var el))
                    continue;

                if (el.ValueKind == JsonValueKind.Number)
                    return ClampDelay(TimeSpan.FromSeconds(Math.Max(0d, el.GetDouble())));

                if (double.TryParse(el.ToString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                    return ClampDelay(TimeSpan.FromSeconds(parsed));
            }
        }
        catch
        {
        }

        return TimeSpan.FromSeconds(3);
    }

    internal static TimeSpan ClampDelay(TimeSpan raw)
    {
        if (raw <= TimeSpan.Zero)
            raw = TimeSpan.FromSeconds(1);

        var max = TimeSpan.FromMinutes(10);

        return raw > max ? max : raw;
    }

    internal static async Task DrainResponseSafelyAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content == null)
            return;

        try
        {
            await response.Content.CopyToAsync(Stream.Null, cancellationToken);
        }
        catch
        {
        }
    }

    private static TimeSpan? TryReadRetryAfter(HttpHeaders headers)
    {
        if (!headers.TryGetValues("retry-after", out var values))
            return null;

        foreach (var v in values)
        {
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                return TimeSpan.FromSeconds(seconds);

            if (DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var until))
                return until - DateTimeOffset.UtcNow;
        }

        return null;
    }
}
