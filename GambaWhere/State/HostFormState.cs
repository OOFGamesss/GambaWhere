using System;
using System.Collections.Generic;
using GambaWhere.Games;

namespace GambaWhere.State;

/// <summary>The host tab's in-progress form: chosen game, rules, presets, and auto-end settings.</summary>
public class HostFormState
{
    public string? SelectedVenueName { get; set; }
    public int SelectedGameIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public IRuleConfig? RuleConfig { get; set; }
    public int SelectedPresetIndex { get; set; }
    public string PresetNameBuffer { get; set; } = string.Empty;
    public bool IsRenamingPreset { get; set; }
    public string? StatusMessage { get; set; }
    public bool IsStarting { get; set; }
    public bool UseManualHostRules { get; set; }
    public int SelectedRuleSourceIndex { get; set; }
    public bool AutoEndEnabled { get; set; }
    public int AutoEndHours { get; set; } = 2;
    public int AutoEndMinutes { get; set; }
}

public sealed record HostRuleSource(string Name, Func<Dictionary<string, object>?> GetRules);
