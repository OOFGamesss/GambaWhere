using System;
using System.Collections.Generic;
using System.Linq;
using GambaWhere.Config;

namespace GambaWhere.Games;

/// <summary>Builds an API rules payload from a saved preset, so a scheduled session can resolve its rules at the moment it starts rather than carrying a stale copy.</summary>
internal static class PresetRules
{
    public static Dictionary<string, object> Build(Configuration config, string gameType, string? presetName)
    {
        var ruleConfig = CreateRuleConfig(gameType);
        if (ruleConfig == null)
            return new Dictionary<string, object>();

        var preset = FindPreset(config, gameType, presetName);
        if (preset != null)
            ruleConfig.LoadFromPreset(preset.RuleValues);

        return Sendable(ruleConfig.ToApiPayload());
    }

    public static Dictionary<string, object> Sendable(Dictionary<string, object> rules)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            if (IsSendable(rule.Value))
                result[rule.Key] = rule.Value;
        }

        return result;
    }

    private static bool IsSendable(object? value)
    {
        if (value is null)
            return false;

        return value switch
        {
            string s => !string.IsNullOrWhiteSpace(s),
            bool b => b,
            byte n => n != 0,
            sbyte n => n != 0,
            short n => n != 0,
            ushort n => n != 0,
            int n => n != 0,
            uint n => n != 0,
            long n => n != 0,
            ulong n => n != 0,
            float f => f != 0f,
            double d => d != 0d,
            decimal m => m != 0m,
            _ => true
        };
    }

    private static IRuleConfig? CreateRuleConfig(string gameType)
    {
        var index = Array.IndexOf(GameCategories.Keys, gameType);
        if (index < 0)
            return null;

        var configs = GameCatalog.CreateRuleConfigs();
        return index < configs.Length ? configs[index] : null;
    }

    private static GamePreset? FindPreset(Configuration config, string gameType, string? presetName)
    {
        if (!config.Presets.TryGetValue(gameType, out var presets) || presets.Count == 0)
            return null;

        if (string.IsNullOrEmpty(presetName))
            return presets[0];

        return presets.FirstOrDefault(p => string.Equals(p.Name, presetName, StringComparison.Ordinal))
               ?? presets[0];
    }
}
