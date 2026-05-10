using Dalamud.Configuration;
using GambaWhere.Alerting;
using GambaWhere.Rules;
using System;
using System.Collections.Generic;

namespace GambaWhere.Config;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string? ActiveSessionToken { get; set; }
    public string? ActiveCharacterName { get; set; }

    public bool AutoSessionDetection { get; set; } = true;

    public Dictionary<string, List<GamePreset>> Presets { get; set; } = new();

    public List<DiscordWebhookEntry> DiscordWebhooks { get; set; } = new();
    public List<AlertRule> Alerts { get; set; } = new();
    public bool AlertToastEnabled { get; set; } = false;
    public bool AlertSoundEnabled { get; set; } = false;
    public int AlertSoundEffectId { get; set; } = 1;

    public void Save() => global::GambaWhere.GambaWhere.PluginInterface.SavePluginConfig(this);

    public void EnsureDefaultPresets()
    {
        EnsureBingoDefaults();
        EnsureBlackjackDefaults();
        EnsureChocoboRacingDefaults();
        EnsureMiniGamesDefaults();
        EnsurePokerDefaults();
        EnsureRouletteDefaults();
        EnsureScratchcardsDefaults();
        EnsureSpinTheWheelDefaults();
    }

    private void EnsureBingoDefaults()
    {
        if (Presets.ContainsKey("Bingo") && Presets["Bingo"].Count > 0)
            return;

        Presets["Bingo"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    { "gameType", "Full Board" },
                    { "cardCost", 200000 },
                    { "boostedPot", 10000000 },
                    { "totalPot", 10000000 },
                    { "chaosMode", false },
                    { "multiWinner", false }
                }
            }
        };
    }

    private void EnsureBlackjackDefaults()
    {
        if (Presets.ContainsKey("Blackjack") && Presets["Blackjack"].Count > 0)
            return;

        Presets["Blackjack"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    { "maxBet", 1000000 },
                    { "maxPush", 1000000 },
                    { "standsSoftOn", 17 },
                    { "standsHardOn", 17 },
                    { "maxSplits", 2 },
                    { "allowNonMatchingSplits", false },
                    { BlackjackRules.FiveCardCharlieRuleKey, false }
                }
            }
        };
    }

    private void EnsureChocoboRacingDefaults()
    {
        if (Presets.ContainsKey("Chocobo Racing") && Presets["Chocobo Racing"].Count > 0)
            return;

        Presets["Chocobo Racing"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    { "chocoboRunners", 5 },
                    { "raceTrackLength", 5 },
                    { "maxBetPerChocobo", 500000 },
                    { "payoutOdds", 5.0f },
                    { "perfectRaceOdds", 25.0f }
                }
            }
        };
    }

    private void EnsureMiniGamesDefaults()
    {
        if (Presets.ContainsKey("Mini Games") && Presets["Mini Games"].Count > 0)
            return;

        Presets["Mini Games"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>()
            }
        };
    }

    private void EnsurePokerDefaults()
    {
        if (Presets.ContainsKey("Poker") && Presets["Poker"].Count > 0)
            return;

        Presets["Poker"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    { "gameType", "Sit N Go" },
                    { "minBuyin", 1000000 },
                    { "maxBuyin", 5000000 }
                }
            }
        };
    }

    private void EnsureRouletteDefaults()
    {
        if (Presets.ContainsKey("Roulette") && Presets["Roulette"].Count > 0)
            return;

        Presets["Roulette"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    ["maxBetInner"] = 100000,
                    ["maxBetOuter"] = 100000,
                    ["maxBetInnerVIP"] = 200000,
                    ["maxBetOuterVIP"] = 200000
                }
            }
        };
    }

    private void EnsureScratchcardsDefaults()
    {
        if (Presets.ContainsKey("Scratchcards") && Presets["Scratchcards"].Count > 0)
            return;

        Presets["Scratchcards"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>
                {
                    { "cardCost", 200000 },
                    { "jackpot", 3000000 }
                }
            }
        };
    }

    private void EnsureSpinTheWheelDefaults()
    {
        if (Presets.ContainsKey("Spin the Wheel") && Presets["Spin the Wheel"].Count > 0)
            return;

        Presets["Spin the Wheel"] = new List<GamePreset>
        {
            new()
            {
                Name = "Default",
                RuleValues = new Dictionary<string, object>()
            }
        };
    }
}

[Serializable]
public class GamePreset
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, object> RuleValues { get; set; } = new();

    public string Description { get; set; } = string.Empty;
}
