using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;
using SimpleRoulette.Data;

namespace GambaWhere.Rules;

public class RouletteRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Roulette";

    private const int IpcMaxBetGilMultiplier = 1000;

    public string AutomaticRulesPluginName => "SimpleRoulette";

    private int _maxBetInner;
    private int _maxBetOuter;
    private int _maxBetInnerVip;
    private int _maxBetOuterVip;

    private static readonly string[] RowLabels =
    {
        "Max Bet Inner (gil)",
        "Max Bet Outer (gil)",
        "Max Bet Inner VIP (gil)",
        "Max Bet Outer VIP (gil)"
    };

    public void Draw()
    {
        var offset = RowLabels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(RowLabels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##RouletteMaxInner", ref _maxBetInner,0);

        ImGui.Text(RowLabels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##RouletteMaxOuter", ref _maxBetOuter,0);

        ImGui.Text(RowLabels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##RouletteMaxInnerVip", ref _maxBetInnerVip,0);

        ImGui.Text(RowLabels[3]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##RouletteMaxOuterVip", ref _maxBetOuterVip,0);

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
            rules["maxBetInner"] = maxInner * IpcMaxBetGilMultiplier;
        if (info.MaxBetOuter is int maxOuter)
            rules["maxBetOuter"] = maxOuter * IpcMaxBetGilMultiplier;
        if (info.MaxBetInnerVIP is int maxInnerVip)
            rules["maxBetInnerVIP"] = maxInnerVip * IpcMaxBetGilMultiplier;
        if (info.MaxBetOuterVIP is int maxOuterVip)
            rules["maxBetOuterVIP"] = maxOuterVip * IpcMaxBetGilMultiplier;
        return true;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is not GameInfoIPC rouletteInfo)
        {
            ImGui.TextDisabled("No Session has been started");
            return;
        }

        ImGui.Text($"Player count: {rouletteInfo.PlayerCount:N0}");
        ImGui.Text($"Max bet (inner): {FormatIpcMaxBetAsGil(rouletteInfo.MaxBetInner, IpcMaxBetGilMultiplier)}");
        ImGui.Text($"Max bet (outer): {FormatIpcMaxBetAsGil(rouletteInfo.MaxBetOuter, IpcMaxBetGilMultiplier)}");
        ImGui.Text($"Max bet inner (VIP): {FormatIpcMaxBetAsGil(rouletteInfo.MaxBetInnerVIP, IpcMaxBetGilMultiplier)}");
        ImGui.Text($"Max bet outer (VIP): {FormatIpcMaxBetAsGil(rouletteInfo.MaxBetOuterVIP, IpcMaxBetGilMultiplier)}");
    }

    private static string FormatIpcMaxBetAsGil(int? ipcUnits, int gilMultiplier) =>
        ipcUnits is int v ? (v * (long)gilMultiplier).ToString("N0") : "Not set";
}
