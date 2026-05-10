using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class PokerRules : IRuleConfig
{
    public string GameType => "Poker";

    private string _gameType = "String";
    private int _minBuyin = 1000000;
    private int _maxBuyin = 5000000;

    private static readonly string[] RowLabels =
    {
        "Game Type",
        "Min Buyin (gil)",
        "Max Buyin (gil)"
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
        ImGuiEx.InputFancyNumeric("##MinBuyin", ref _minBuyin, 0);

        ImGui.Text(RowLabels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##MaxBuyin", ref _maxBuyin, 0);

        _minBuyin = RuleClamp.Min(_minBuyin, 0);
        _maxBuyin = RuleClamp.Min(_maxBuyin, 0);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "gameType", _gameType },
        { "minBuyin", _minBuyin },
        { "maxBuyin", _maxBuyin }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _gameType = PresetReader.String(values, "gameType", _gameType);
        _minBuyin = PresetReader.Int(values, "minBuyin", _minBuyin);
        _maxBuyin = PresetReader.Int(values, "maxBuyin", _maxBuyin);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
