using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class BlackjackRules : IRuleConfig
{
    public const string FiveCardCharlieRuleKey = "5 Card Charlie";

    private const string LegacyCompactFiveCardCharlieKey = "5CardCharlie";

    private const string LegacyPayingTwoPointFiveCharlieKey = "payingTwoPointFiveCharlie";

    public string GameType => "Blackjack";

    private int _maxBet = 100000;
    private int _maxPush = 3;
    private int _standsSoftOn = 17;
    private int _standsHardOn = 17;
    private int _maxSplits = 2;
    private bool _allowNonMatchingSplits = false;
    private bool _fiveCardCharlie = false;

    private static readonly string[] Labels =
    {
        "Max Bet (gil)", "Max Push (gil)", "Stands Soft On", "Stands Hard On",
        "Max Splits", "Allow Non-Matching Splits", "5 Card Charlie"
    };

    public void Draw()
    {
        var offset = Labels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(Labels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##MaxBet", ref _maxBet,0);
        

        ImGui.Text(Labels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##MaxPush", ref _maxPush,0);

        ImGui.Text(Labels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##StandsSoftOn", ref _standsSoftOn,0);

        ImGui.Text(Labels[3]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##StandsHardOn", ref _standsHardOn);

        ImGui.Text(Labels[4]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxSplits", ref _maxSplits);

        ImGui.Text(Labels[5]);
        ImGui.SameLine(offset);
        ImGui.Checkbox("##AllowNonMatchingSplits", ref _allowNonMatchingSplits);

        ImGui.Text(Labels[6]);
        ImGui.SameLine(offset);
        ImGui.Checkbox("##FiveCardCharlie", ref _fiveCardCharlie);

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
        { FiveCardCharlieRuleKey, _fiveCardCharlie }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _maxBet = PresetReader.Int(values, "maxBet", _maxBet);
        _maxPush = PresetReader.Int(values, "maxPush", _maxPush);
        _standsSoftOn = PresetReader.Int(values, "standsSoftOn", _standsSoftOn);
        _standsHardOn = PresetReader.Int(values, "standsHardOn", _standsHardOn);
        _maxSplits = PresetReader.Int(values, "maxSplits", _maxSplits);
        _allowNonMatchingSplits = PresetReader.Bool(values, "allowNonMatchingSplits", _allowNonMatchingSplits);
        _fiveCardCharlie = false;
        if (values.ContainsKey(FiveCardCharlieRuleKey))
            _fiveCardCharlie = PresetReader.Bool(values, FiveCardCharlieRuleKey, false);
        else if (values.ContainsKey(LegacyCompactFiveCardCharlieKey))
            _fiveCardCharlie = PresetReader.Bool(values, LegacyCompactFiveCardCharlieKey, false);
        else if (values.ContainsKey(LegacyPayingTwoPointFiveCharlieKey))
            _fiveCardCharlie = PresetReader.Bool(values, LegacyPayingTwoPointFiveCharlieKey, false);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
