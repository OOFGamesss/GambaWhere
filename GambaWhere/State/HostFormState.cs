using GambaWhere.Rules;

namespace GambaWhere.State;

public class HostFormState
{
    public int SelectedVenueIndex { get; set; } = 0;

    public int SelectedGameIndex { get; set; } = 0;

    public string Description { get; set; } = string.Empty;

    public IRuleConfig? RuleConfig { get; set; }

    public int SelectedPresetIndex { get; set; } = 0;

    public string PresetNameBuffer { get; set; } = string.Empty;

    public bool IsRenamingPreset { get; set; } = false;

    public string? StatusMessage { get; set; }

    public bool IsStarting { get; set; } = false;
}
