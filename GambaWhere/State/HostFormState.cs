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

    /// <summary>When false and automatic host rules are available, API rules come from IPC instead of manual fields.</summary>
    public bool UseManualHostRules { get; set; } = false;
}
