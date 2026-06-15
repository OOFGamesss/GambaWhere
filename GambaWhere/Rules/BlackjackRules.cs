using System.Collections.Generic;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for Blackjack.</summary>
public class BlackjackRules : IRuleConfig
{
    public const string FiveCardCharlieRuleKey = "5 Card Charlie";

    private const string LegacyCompactFiveCardCharlieKey = "5CardCharlie";

    private const string LegacyPayingTwoPointFiveCharlieKey = "payingTwoPointFiveCharlie";

    public string GameType => "Blackjack";

    private int _maxBet = 1000000;
    private int _maxPush = 1000000;
    private int _standsSoftOn = 17;
    private int _standsHardOn = 17;
    private int _maxSplits = 2;
    private bool _allowNonMatchingSplits = false;
    private bool _fiveCardCharlie = false;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##BlackjackGrid"))
        {
            grid.Cell();
            HostField.Money("Max Bet (gil)", "##MaxBet", ref _maxBet);
            grid.Cell();
            HostField.Money("Max Push (gil)", "##MaxPush", ref _maxPush);
            grid.Cell();
            HostField.Int("Stands Soft On", "##StandsSoftOn", ref _standsSoftOn);
            grid.Cell();
            HostField.Int("Stands Hard On", "##StandsHardOn", ref _standsHardOn);
            grid.Cell();
            HostField.Int("Max Splits", "##MaxSplits", ref _maxSplits);
            grid.Cell();
            HostField.Toggle("Allow Non-Matching Splits", "##AllowNonMatchingSplits", ref _allowNonMatchingSplits);
            grid.Cell();
            HostField.Toggle("5 Card Charlie", "##FiveCardCharlie", ref _fiveCardCharlie);
        }

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
