using System;
using System.Collections.Generic;

namespace GambaWhere.Config;

/// <summary>Small data types stored in the plugin config.</summary>
[Serializable]
public class DiscordWebhookEntry
{
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? MessageId { get; set; }
    public bool PostFailed { get; set; }
}

[Serializable]
public class GambaProfile
{
    public const int BioMaxLength = 255;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? ImageFileName { get; set; }
    public string? OriginalImageFileName { get; set; }
    public float ImageCropZoom { get; set; } = 1f;
    public float ImageCropCenterX { get; set; } = 0.5f;
    public float ImageCropCenterY { get; set; } = 0.5f;
    public string Bio { get; set; } = string.Empty;
    public List<string> PreferredGames { get; set; } = new();
    public bool Booster { get; set; }
    public string BorderStyle { get; set; } = "none";
    public string CardEffectStyle { get; set; } = "none";
    public string? UploadedImageUrl { get; set; }
    public string? UploadedImageHash { get; set; }
}

[Serializable]
public class RecruitmentPostToken
{
    public string Id { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string PosterCharacter { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
}

[Serializable]
public class GamePreset
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> RuleValues { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

[Serializable]
public class ActiveSessionSnapshot
{
    public string EventId { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string? GameType { get; set; }
    public string? VenueName { get; set; }
    public string? RulesJson { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? AutoEndAt { get; set; }
    public bool IsPaused { get; set; }
    public DateTime? PausedAt { get; set; }
    public long TotalPausedDurationTicks { get; set; }
    public bool UsesAutomaticHostRules { get; set; }
    public string? DiscordUrl { get; set; }
    public string? ImageUrl { get; set; }
}
