using GambaWhere.Rules;

namespace GambaWhere.State;

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

    public bool AutoEndEnabled { get; set; } = false;

    public int AutoEndHours { get; set; } = 2;
    
    public int AutoEndMinutes { get; set; } = 0;
}
