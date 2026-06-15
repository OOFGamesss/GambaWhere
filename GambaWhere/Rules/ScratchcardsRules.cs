using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for Scratchcards.</summary>
public class ScratchcardsRules : IRuleConfig
{
    public string GameType => "Scratchcards";

    private int _cardCost = 200000;
    private int _jackpot = 3000000;
    private uint _topPrizeItemId = 0;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##ScratchcardsGrid"))
        {
            grid.Cell();
            HostField.Money("Card Cost (gil)", "##CardCost", ref _cardCost);
            grid.Cell();
            HostField.Money("Jackpot (gil)", "##Jackpot", ref _jackpot);
            grid.Cell();
            HostField.Label("Top Prize");
            ImGui.SetNextItemWidth(-1);
            ItemSearchCombo.Draw("##TopPrize", ref _topPrizeItemId);
        }

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
