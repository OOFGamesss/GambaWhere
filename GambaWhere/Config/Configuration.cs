using Dalamud.Configuration;
using GambaWhere.Alerting;
using GambaWhere.Games;
using GambaWhere.Models;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GambaWhere.Config;

/// <summary>Saved settings and session state.</summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<ActiveSessionSnapshot> ActiveSessions { get; set; } = new();

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

    public List<ScheduledSessionSnapshot> ScheduledSessions { get; set; } = new();

    public List<KnownRuleSource> KnownRuleSources { get; set; } = new();

    public bool ScheduledOverlayEnabled { get; set; } = true;
    public int ScheduledLeadMinutes { get; set; } = 15;
    public float ScheduledOverlayX { get; set; } = -1f;
    public float ScheduledOverlayY { get; set; } = -1f;

    public bool MinimapHostIconsEnabled { get; set; } = true;

    public Dictionary<string, bool> MinimapHostGameTypeEnabled { get; set; } = new();

    public bool IsMinimapGameTypeEnabled(string gameType) =>
        !MinimapHostGameTypeEnabled.TryGetValue(gameType, out var enabled) || enabled;

    public List<string> FavouriteVenues { get; set; } = new();

    public List<GambaProfile> Profiles { get; set; } = new();
    public string? SelectedProfileId { get; set; }

    public List<RecruitmentPostToken> RecruitmentPosts { get; set; } = new();

    public List<GameStoreGame> CachedStoreGames { get; set; } = new();
    public DateTime? StoreGamesUpdatedAt { get; set; }

    public List<GameTypeDto> CachedGameTypes { get; set; } = new();
    public DateTime? CategoriesUpdatedAt { get; set; }

    public bool ShowNsfwRecruitment { get; set; }

    public Vector4 PrimaryColour { get; set; } = DefaultPrimaryColour;
    public Vector4 SecondaryColour { get; set; } = DefaultSecondaryColour;

    public string? CustomIdleBannerFileName { get; set; }
    public string? CustomActiveBannerFileName { get; set; }

    public static Vector4 DefaultPrimaryColour => new(0.60f, 0.0f, 1.0f, 1f);
    public static Vector4 DefaultSecondaryColour => new(0.0f, 0.88f, 0.85f, 1f);

    public void Save() => global::GambaWhere.GambaWhere.PluginInterface.SavePluginConfig(this);

    private readonly object _knownRuleSourceGate = new();

    private static readonly TimeSpan KnownRuleSourceRefreshInterval = TimeSpan.FromHours(1);

    public void RememberRuleSource(string name, string category)
    {
        var now = DateTime.UtcNow;
        bool save;

        lock (_knownRuleSourceGate)
        {
            var known = KnownRuleSources.Find(
                s => string.Equals(s.Name, name, StringComparison.Ordinal)
                     && string.Equals(s.Category, category, StringComparison.Ordinal));

            if (known == null)
            {
                KnownRuleSources.Add(new KnownRuleSource
                {
                    Name = name,
                    Category = category,
                    LastSeenUtc = now
                });
                save = true;
            }
            else
            {
                save = now - known.LastSeenUtc >= KnownRuleSourceRefreshInterval;
                if (save)
                    known.LastSeenUtc = now;
            }
        }

        if (save)
            Save();
    }

    public bool IsKnownRuleSource(string name, string category)
    {
        lock (_knownRuleSourceGate)
            return KnownRuleSources.Exists(
                s => string.Equals(s.Name, name, StringComparison.Ordinal)
                     && string.Equals(s.Category, category, StringComparison.Ordinal));
    }

    public List<string> KnownRuleSourceNames(string category)
    {
        lock (_knownRuleSourceGate)
        {
            var names = new List<string>();
            foreach (var known in KnownRuleSources)
                if (string.Equals(known.Category, category, StringComparison.Ordinal))
                    names.Add(known.Name);

            return names;
        }
    }

    public void MigrateLegacyActiveSession()
    {
        if (string.IsNullOrEmpty(ActiveSessionToken)
            || string.IsNullOrEmpty(ActiveCharacterName)
            || ActiveSessions.Count > 0)
            return;

        ActiveSessions.Add(new ActiveSessionSnapshot
        {
            EventId = string.Empty,
            SessionToken = ActiveSessionToken,
            CharacterName = ActiveCharacterName,
            GameType = ActiveGameType,
            VenueName = ActiveVenueName,
            RulesJson = ActiveRulesJson,
            Description = ActiveDescription,
            Location = ActiveLocation,
            StartedAt = ActiveStartedAt,
            AutoEndAt = ActiveAutoEndAt,
            IsPaused = ActiveIsPaused,
            PausedAt = ActivePausedAt,
            TotalPausedDurationTicks = ActiveTotalPausedDurationTicks,
            DiscordUrl = ActiveDiscordUrl,
            ImageUrl = ActiveImageUrl,
        });

        ActiveSessionToken = null;
        ActiveCharacterName = null;
        ActiveGameType = null;
        ActiveVenueName = null;
        ActiveRulesJson = null;
        ActiveDescription = null;
        ActiveLocation = null;
        ActiveStartedAt = null;
        ActiveAutoEndAt = null;
        ActiveIsPaused = false;
        ActivePausedAt = null;
        ActiveTotalPausedDurationTicks = 0;
        ActiveDiscordUrl = null;
        ActiveImageUrl = null;

        Save();
    }

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
