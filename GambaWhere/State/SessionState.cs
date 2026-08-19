using System;
using System.Collections.Generic;
using System.Threading;

namespace GambaWhere.State;

/// <summary>Runtime state for a single hosting session; a character may run several at once.</summary>
public class SessionState
{
    public bool IsActive { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string? VenueName { get; set; }
    public Dictionary<string, object>? ActiveRules { get; set; }
    public string? DiscordUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? AutomaticRuleSourceName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? AutoEndAt { get; set; }
    public CancellationTokenSource? AutoEndCts { get; set; }
    public bool IsPaused { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? PausedAt { get; set; }
    public TimeSpan TotalPausedDuration { get; set; }

    public TimeSpan Elapsed()
    {
        if (!StartedAt.HasValue)
            return TimeSpan.Zero;

        var until = IsPaused && PausedAt.HasValue ? PausedAt.Value : DateTime.UtcNow;
        var elapsed = until - StartedAt.Value - TotalPausedDuration;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }
}
