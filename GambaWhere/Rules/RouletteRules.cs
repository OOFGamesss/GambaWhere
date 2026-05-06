using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.Utility;
using SimpleRoulette.Data;

namespace GambaWhere.Rules;

public class RouletteRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Roulette";

    public string AutomaticRulesPluginName => "SimpleRoulette";

    private int _maxBetInner;
    private int _maxBetOuter;
    private int _maxBetInnerVip;
    private int _maxBetOuterVip;

    private static readonly string[] RowLabels =
    {
        "Max Bet Inner",
        "Max Bet Outer",
        "Max Bet Inner VIP",
        "Max Bet Outer VIP"
    };

    public void Draw()
    {
        var offset = RowLabels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(RowLabels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RouletteMaxInner", ref _maxBetInner);

        ImGui.Text(RowLabels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RouletteMaxOuter", ref _maxBetOuter);

        ImGui.Text(RowLabels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RouletteMaxInnerVip", ref _maxBetInnerVip);

        ImGui.Text(RowLabels[3]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RouletteMaxOuterVip", ref _maxBetOuterVip);

        _maxBetInner = RuleClamp.Min(_maxBetInner, 0);
        _maxBetOuter = RuleClamp.Min(_maxBetOuter, 0);
        _maxBetInnerVip = RuleClamp.Min(_maxBetInnerVip, 0);
        _maxBetOuterVip = RuleClamp.Min(_maxBetOuterVip, 0);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "maxBetInner", _maxBetInner },
        { "maxBetOuter", _maxBetOuter },
        { "maxBetInnerVIP", _maxBetInnerVip },
        { "maxBetOuterVIP", _maxBetOuterVip }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _maxBetInner = PresetReader.Int(values, "maxBetInner", _maxBetInner);
        _maxBetOuter = PresetReader.Int(values, "maxBetOuter", _maxBetOuter);
        _maxBetInnerVip = PresetReader.Int(values, "maxBetInnerVIP", _maxBetInnerVip);
        _maxBetOuterVip = PresetReader.Int(values, "maxBetOuterVIP", _maxBetOuterVip);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();

    public bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules)
    {
        if (ipcContext is not GameInfoIPC info)
        {
            rules = null;
            return false;
        }

        rules = new Dictionary<string, object>
        {
            ["playerCount"] = info.PlayerCount
        };

        if (info.MaxBetInner is int maxInner)
            rules["maxBetInner"] = maxInner;
        if (info.MaxBetOuter is int maxOuter)
            rules["maxBetOuter"] = maxOuter;
        if (info.MaxBetInnerVIP is int maxInnerVip)
            rules["maxBetInnerVIP"] = maxInnerVip;
        if (info.MaxBetOuterVIP is int maxOuterVip)
            rules["maxBetOuterVIP"] = maxOuterVip;
        return true;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is not GameInfoIPC rouletteInfo)
        {
            ImGui.TextDisabled("No Session has been started");
            return;
        }

        ImGui.Text("Roulette session info:");
        ImGui.Text($"Player count: {rouletteInfo.PlayerCount:N0}");
        ImGui.Text($"Max bet (inner): {FormatOptionalGil(rouletteInfo.MaxBetInner)}");
        ImGui.Text($"Max bet (outer): {FormatOptionalGil(rouletteInfo.MaxBetOuter)}");
        ImGui.Text($"Max bet inner (VIP): {FormatOptionalGil(rouletteInfo.MaxBetInnerVIP)}");
        ImGui.Text($"Max bet outer (VIP): {FormatOptionalGil(rouletteInfo.MaxBetOuterVIP)}");
    }

    private static string FormatOptionalGil(int? gil) => gil is int v ? v.ToString("N0") : "Not set";
}
