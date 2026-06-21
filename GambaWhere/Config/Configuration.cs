using Dalamud.Configuration;
using GambaWhere.Alerting;
using GambaWhere.Games;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GambaWhere.Config;

/// <summary>Plugin configuration persisted between sessions, including active session state and user preferences.</summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string? ActiveSessionToken { get; set; }
    public string? ActiveCharacterName { get; set; }
    public string? ActiveGameType { get; set; }
    public string? ActiveVenueName { get; set; }
    public string? ActiveRulesJson { get; set; }
    public string? ActiveDescription { get; set; }
    public string? ActiveLocation { get; set; }
    public DateTime? ActiveStartedAt { get; set; }
    public DateTime? ActiveAutoEndAt { get; set; }
    public bool ActiveIsPaused { get; set; }
    public DateTime? ActivePausedAt { get; set; }
    public long ActiveTotalPausedDurationTicks { get; set; }
    public bool ActiveUsesAutomaticHostRules { get; set; }
    public string? ActiveDiscordUrl { get; set; }
    public string? ActiveImageUrl { get; set; }

    public bool AutoSessionDetection { get; set; } = true;

    public Dictionary<string, List<GamePreset>> Presets { get; set; } = new();

    public List<DiscordWebhookEntry> DiscordWebhooks { get; set; } = new();

    public string? BoosterKey { get; set; }

    public List<AlertRule> Alerts { get; set; } = new();
    public bool AlertToastEnabled { get; set; } = false;
    public bool AlertSoundEnabled { get; set; } = false;
    public int AlertSoundEffectId { get; set; } = 1;
    public bool AlertPauseInDuty { get; set; } = false;

    public bool PillOverlayEnabled { get; set; } = true;
    public float PillPositionX { get; set; } = 50f;
    public float PillPositionY { get; set; } = 50f;

    public bool MinimapHostIconsEnabled { get; set; } = true;

    public Dictionary<string, bool> MinimapHostGameTypeEnabled { get; set; } = new();

    public bool IsMinimapGameTypeEnabled(string gameType) =>
        !MinimapHostGameTypeEnabled.TryGetValue(gameType, out var enabled) || enabled;

    public List<string> FavouriteVenues { get; set; } = new();

    public List<GambaProfile> Profiles { get; set; } = new();
    public string? SelectedProfileId { get; set; }

    public List<RecruitmentPostToken> RecruitmentPosts { get; set; } = new();

    public bool ShowNsfwRecruitment { get; set; }

    public Vector4 PrimaryColour { get; set; } = DefaultPrimaryColour;
    public Vector4 SecondaryColour { get; set; } = DefaultSecondaryColour;

    public string? CustomIdleBannerFileName { get; set; }
    public string? CustomActiveBannerFileName { get; set; }

    public static Vector4 DefaultPrimaryColour => new(0.60f, 0.0f, 1.0f, 1f);
    public static Vector4 DefaultSecondaryColour => new(0.0f, 0.88f, 0.85f, 1f);

    public void Save() => global::GambaWhere.GambaWhere.PluginInterface.SavePluginConfig(this);

    public void EnsureDefaultPresets()
    {
        var configs = GameCatalog.CreateRuleConfigs();
        var keys = GameCategories.Keys;

        for (var i = 0; i < keys.Length; i++)
        {
            if (Presets.TryGetValue(keys[i], out var list) && list.Count > 0)
                continue;

            Presets[keys[i]] = new List<GamePreset>
            {
                new() { Name = "Default", RuleValues = configs[i].SaveToPreset() }
            };
        }
    }
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
