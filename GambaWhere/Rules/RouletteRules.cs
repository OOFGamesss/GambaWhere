using System.Collections.Generic;
using GambaWhere.UI.Components;
using GambaWhere.Utility;
using SimpleRoulette.Data;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for Roulette sessions, supporting manual entry and automatic IPC data from SimpleRoulette.</summary>
public class RouletteRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Roulette";

    private const int IpcMaxBetGilMultiplier = 1000;

    public string AutomaticRulesPluginName => "SimpleRoulette";

    private int _maxBetInner;
    private int _maxBetOuter;
    private int _maxBetInnerVip;
    private int _maxBetOuterVip;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##RouletteGrid"))
        {
            grid.Cell();
            HostField.Money("Max Bet Inner (gil)", "##RouletteMaxInner", ref _maxBetInner);
            grid.Cell();
            HostField.Money("Max Bet Outer (gil)", "##RouletteMaxOuter", ref _maxBetOuter);
            grid.Cell();
            HostField.Money("Max Bet Inner VIP (gil)", "##RouletteMaxInnerVip", ref _maxBetInnerVip);
            grid.Cell();
            HostField.Money("Max Bet Outer VIP (gil)", "##RouletteMaxOuterVip", ref _maxBetOuterVip);
        }

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

        rules = new Dictionary<string, object>();

        if (info.MaxBetInner is int maxInner)
            rules["maxBetInner"] = maxInner * IpcMaxBetGilMultiplier;
        if (info.MaxBetOuter is int maxOuter)
            rules["maxBetOuter"] = maxOuter * IpcMaxBetGilMultiplier;
        if (info.MaxBetInnerVIP is int maxInnerVip)
            rules["maxBetInnerVIP"] = maxInnerVip * IpcMaxBetGilMultiplier;
        if (info.MaxBetOuterVIP is int maxOuterVip)
            rules["maxBetOuterVIP"] = maxOuterVip * IpcMaxBetGilMultiplier;
        rules["playerCount"] = info.PlayerCount;
        return true;
    }
}
