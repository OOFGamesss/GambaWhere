using System;
using System.Collections.Generic;
using System.Threading;

namespace GambaWhere.State;

/// <summary>Runtime state for the active hosting session.</summary>
public class SessionState
{
    public bool IsActive { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string? VenueName { get; set; }
    public Dictionary<string, object>? ActiveRules { get; set; }
    public string? DiscordUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool UsesAutomaticHostRules { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? AutoEndAt { get; set; }
    public CancellationTokenSource? LoopCts { get; set; }
    public bool IsPaused { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? PausedAt { get; set; }
    public TimeSpan TotalPausedDuration { get; set; }

    public void Clear()
    {
        IsActive = false;
        SessionToken = string.Empty;
        CharacterName = string.Empty;
        Location = string.Empty;
        GameType = string.Empty;
        VenueName = null;
        ActiveRules = null;
        DiscordUrl = null;
        ImageUrl = null;
        UsesAutomaticHostRules = false;
        StartedAt = null;
        AutoEndAt = null;
        LoopCts = null;
        IsPaused = false;
        Description = string.Empty;
        PausedAt = null;
        TotalPausedDuration = TimeSpan.Zero;
    }
}
