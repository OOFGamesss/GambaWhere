using System;

namespace GambaWhere.Config;

/// <summary>Persisted configuration for a single Discord webhook target.</summary>
[Serializable]
public class DiscordWebhookEntry
{
    public string Url { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string? MessageId { get; set; }

    public bool PostFailed { get; set; }
}
