using System.Text.Json;
using GambaWhere.Config;

namespace GambaWhere.Discord;

internal static class DiscordWebhookEntryMutations
{
    internal static void AssignMessageIdIfPresent(DiscordWebhookEntry entry, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("id", out var idEl))
                return;

            var idStr = idEl.GetString();
            if (!string.IsNullOrWhiteSpace(idStr))
                entry.MessageId = idStr.Trim();
        }
        catch (JsonException)
        {
            // Ignore malformed webhook execute JSON.
        }
    }

    internal static void MarkFailure(DiscordWebhookEntry entry)
    {
        entry.PostFailed = true;
    }

    internal static void ClearFailure(DiscordWebhookEntry entry)
    {
        entry.PostFailed = false;
    }
}
