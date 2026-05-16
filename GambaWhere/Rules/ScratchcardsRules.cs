using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class ScratchcardsRules : IRuleConfig
{
    public string GameType => "Scratchcards";

    private int _cardCost = 200000;
    private int _jackpot = 3000000;
    private uint _topPrizeItemId = 0;

    private static readonly string[] RowLabels =
    {
        "Card Cost (gil)",
        "Jackpot (gil)",
        "Top Prize"
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

        ImGui.Text(RowLabels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        ItemSearchCombo.Draw("##TopPrize", ref _topPrizeItemId);

        _cardCost = RuleClamp.Min(_cardCost, 0);
        _jackpot = RuleClamp.Min(_jackpot, 0);
    }

    public Dictionary<string, object> ToApiPayload()
    {
        var payload = new Dictionary<string, object>
        {
            { "cardCost", _cardCost },
            { "jackpot", _jackpot }
        };

        var topPrizeName = ItemSearchCombo.GetItemName(_topPrizeItemId);
        if (topPrizeName != null)
            payload["topPrize"] = topPrizeName;

        return payload;
    }

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _cardCost = PresetReader.Int(values, "cardCost", _cardCost);
        _jackpot = PresetReader.Int(values, "jackpot", _jackpot);
        _topPrizeItemId = (uint)PresetReader.Int(values, "topPrizeItemId", 0);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
