using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for Spin the Wheel.</summary>
public class SpinTheWheelRules : IRuleConfig
{
    public string GameType => "Spin the Wheel";

    public void Draw()
    {
        ImGui.TextDisabled("Contact Felix to add rules to this section.");
    }

    public Dictionary<string, object> ToApiPayload() => new();

    public void LoadFromPreset(Dictionary<string, object> values) { }

    public Dictionary<string, object> SaveToPreset() => new();
}
