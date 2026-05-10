using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class ScratchcardsRules : IRuleConfig
{
    public string GameType => "Scratchcards";

    private int _cardCost = 200000;
    private int _jackpot = 3000000;

    private static readonly string[] RowLabels =
    {
        "Card Cost (gil)",
        "Jackpot (gil)"
    };

    public void Draw()
    {
        var offset = RowLabels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(RowLabels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##CardCost", ref _cardCost, 0);

        ImGui.Text(RowLabels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##Jackpot", ref _jackpot, 0);

        _cardCost = RuleClamp.Min(_cardCost, 0);
        _jackpot = RuleClamp.Min(_jackpot, 0);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "cardCost", _cardCost },
        { "jackpot", _jackpot }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _cardCost = PresetReader.Int(values, "cardCost", _cardCost);
        _jackpot = PresetReader.Int(values, "jackpot", _jackpot);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
