using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class BlackjackRules : IRuleConfig
{
    public string GameType => "Blackjack";

    private int _maxBet = 100000;
    private int _maxPush = 3;
    private int _standsSoftOn = 17;
    private int _standsHardOn = 17;
    private int _maxSplits = 2;
    private bool _allowNonMatchingSplits = false;
    private bool _payingTwoPointFiveCharlie = false;

    private static readonly string[] Labels =
    {
        "Max Bet (gil)", "Max Push", "Stands Soft On", "Stands Hard On",
        "Max Splits", "Allow Non-Matching Splits", "Paying x2.5 for Charlie"
    };

    public void Draw()
    {
        var offset = Labels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text("Max Bet (gil)");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxBet", ref _maxBet);

        ImGui.Text("Max Push");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxPush", ref _maxPush);

        ImGui.Text("Stands Soft On");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##StandsSoftOn", ref _standsSoftOn);

        ImGui.Text("Stands Hard On");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##StandsHardOn", ref _standsHardOn);

        ImGui.Text("Max Splits");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxSplits", ref _maxSplits);

        ImGui.Text("Allow Non-Matching Splits");
        ImGui.SameLine(offset);
        ImGui.Checkbox("##AllowNonMatchingSplits", ref _allowNonMatchingSplits);

        ImGui.Text("Paying x2.5 for Charlie");
        ImGui.SameLine(offset);
        ImGui.Checkbox("##PayingTwoPointFiveCharlie", ref _payingTwoPointFiveCharlie);

        _maxBet = RuleClamp.Min(_maxBet, 0);
        _maxPush = RuleClamp.Min(_maxPush, 0);
        _standsSoftOn = RuleClamp.Range(_standsSoftOn, 1, 21);
        _standsHardOn = RuleClamp.Range(_standsHardOn, 1, 21);
        _maxSplits = RuleClamp.Min(_maxSplits, 0);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "maxBet", _maxBet },
        { "maxPush", _maxPush },
        { "standsSoftOn", _standsSoftOn },
        { "standsHardOn", _standsHardOn },
        { "maxSplits", _maxSplits },
        { "allowNonMatchingSplits", _allowNonMatchingSplits },
        { "payingTwoPointFiveCharlie", _payingTwoPointFiveCharlie }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _maxBet = PresetReader.Int(values, "maxBet", _maxBet);
        _maxPush = PresetReader.Int(values, "maxPush", _maxPush);
        _standsSoftOn = PresetReader.Int(values, "standsSoftOn", _standsSoftOn);
        _standsHardOn = PresetReader.Int(values, "standsHardOn", _standsHardOn);
        _maxSplits = PresetReader.Int(values, "maxSplits", _maxSplits);
        _allowNonMatchingSplits = PresetReader.Bool(values, "allowNonMatchingSplits", _allowNonMatchingSplits);
        _payingTwoPointFiveCharlie = PresetReader.Bool(values, "payingTwoPointFiveCharlie", _payingTwoPointFiveCharlie);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
