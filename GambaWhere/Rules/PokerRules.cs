using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace GambaWhere.Rules;

public class PokerRules : IRuleConfig
{
    public string GameType => "Poker";

    public void Draw()
    {
        ImGui.TextDisabled("Contact Felix to add rules to this section.");
    }

    public Dictionary<string, object> ToApiPayload() => new();

    public void LoadFromPreset(Dictionary<string, object> values) { }

    public Dictionary<string, object> SaveToPreset() => new();
}
