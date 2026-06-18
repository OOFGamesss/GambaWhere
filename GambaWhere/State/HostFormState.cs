using System;
using System.Collections.Generic;
using GambaWhere.Rules;

namespace GambaWhere.State;

/// <summary>
/// A named provider of live automatic rules for the currently selected game. <see cref="GetRules"/>
/// returns the live rule payload, or null when that source has no active session and should not be offered.
/// </summary>
public sealed record HostRuleSource(string Name, Func<Dictionary<string, object>?> GetRules);

public class HostFormState
{
    public string? SelectedVenueName { get; set; } = null;

    public int SelectedGameIndex { get; set; } = 0;

    public string Description { get; set; } = string.Empty;

    public IRuleConfig? RuleConfig { get; set; }

    public int SelectedPresetIndex { get; set; } = 0;

    public string PresetNameBuffer { get; set; } = string.Empty;

    public bool IsRenamingPreset { get; set; } = false;

    public string? StatusMessage { get; set; }

    public bool IsStarting { get; set; } = false;

    public bool UseManualHostRules { get; set; } = false;

    public int SelectedRuleSourceIndex { get; set; } = 0;

    public bool AutoEndEnabled { get; set; } = false;

    public int AutoEndHours { get; set; } = 2;

    public int AutoEndMinutes { get; set; } = 0;
}
