using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;
using SimpleBingo.Data;

namespace GambaWhere.Rules;

public class BingoRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Bingo";

    private string _gameType = "Full Board";
    private int _cardCost = 100000;
    private int _boostedPot = 100000;
    private int _totalPot = 1000000;
    private bool _chaosMode = false;
    private bool _multiWinner = false;

    private static readonly string[] RowLabels =
    {
        "Game Type",
        "Card Cost (gil)",
        "Boosted Pot (gil)",
        "Total Pot (gil)",
        "Chaos Mode",
        "Multi Winner"
    };

    public void Draw()
    {
        var offset = RowLabels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(RowLabels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##GameType", ref _gameType, 64);

        ImGui.Text(RowLabels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##CardCost", ref _cardCost,0);

        ImGui.Text(RowLabels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##BoostedPot", ref _boostedPot,0);

        ImGui.Text(RowLabels[3]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##TotalPot", ref _totalPot,0);

        ImGui.Text(RowLabels[4]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.Checkbox("##ChaosMode", ref _chaosMode);

        ImGui.Text(RowLabels[5]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.Checkbox("##MultiWinner", ref _multiWinner);

        _boostedPot = RuleClamp.Min(_boostedPot, 0);
        _totalPot = RuleClamp.Min(_totalPot, 0);
        _cardCost = RuleClamp.Min(_cardCost, 0);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "gameType", _gameType },
        { "cardCost", _cardCost },
        { "boostedPot", _boostedPot },
        { "totalPot", _totalPot },
        { "chaosMode", _chaosMode },
        { "multiWinner", _multiWinner }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _gameType = PresetReader.String(values, "gameType", _gameType);
        _cardCost = PresetReader.Int(values, "cardCost", _cardCost);
        _boostedPot = PresetReader.Int(values, "boostedPot", _boostedPot);
        _totalPot = PresetReader.Int(values, "totalPot", _totalPot);
        _chaosMode = PresetReader.Bool(values, "chaosMode", _chaosMode);
        _multiWinner = PresetReader.Bool(values, "multiWinner", _multiWinner);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();

    public string AutomaticRulesPluginName => "SimpleBingo";

    public bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules)
    {
        if (ipcContext is not GameInfoIPC info)
        {
            rules = null;
            return false;
        }

        rules = new Dictionary<string, object>
        {
            ["gameType"] = info.GameType.ToString().Replace("_", " "),
            ["boostedPot"] = info.BoostedPot,
            ["totalPot"] = info.TotalPot,
            ["chaosMode"] = info.ChaosMode,
            ["multiWinner"] = info.MultiWinner,
            ["cardCost"] = info.CardCost,
            ["cardsSold"] = info.CardsSold,
            ["playerCount"] = info.PlayerCount
        };
        return true;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is not GameInfoIPC bingoInfo)
        {
            ImGui.TextDisabled("No Session has been started");
            return;
        }

        ImGui.Text("Bingo Session Info:");
        ImGui.Text($"Game Type: {bingoInfo.GameType.ToString().Replace("_", " ")}");
        ImGui.Text($"Boosted Pot: {bingoInfo.BoostedPot:N0}");
        ImGui.Text($"Total Pot: {bingoInfo.TotalPot:N0}");
        ImGui.Text($"Chaos Mode: {(bingoInfo.ChaosMode ? "Yes" : "No")}");
        ImGui.Text($"Multi Winner: {(bingoInfo.MultiWinner ? "Yes" : "No")}");
        ImGui.Text($"Card Cost: {bingoInfo.CardCost:N0}");
        ImGui.Text($"Cards Sold: {bingoInfo.CardsSold:N0}");
        ImGui.Text($"Player Count: {bingoInfo.PlayerCount:N0}");
    }
}
