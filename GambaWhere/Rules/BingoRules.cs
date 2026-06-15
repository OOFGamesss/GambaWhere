using System.Collections.Generic;
using GambaWhere.UI.Components;
using GambaWhere.Utility;
using SimpleBingo.Data;

namespace GambaWhere.Rules;

/// <summary>Rule configuration and automatic host rule source for Bingo.</summary>
public class BingoRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Bingo";

    private string _gameType = "Full Board";
    private int _cardCost = 100000;
    private int _boostedPot = 100000;
    private int _totalPot = 1000000;
    private bool _chaosMode = false;
    private bool _multiWinner = false;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##BingoGrid"))
        {
            grid.Cell();
            HostField.Text("Game Type", "##GameType", ref _gameType, 64);
            grid.Cell();
            HostField.Money("Card Cost (gil)", "##CardCost", ref _cardCost);
            grid.Cell();
            HostField.Money("Boosted Pot (gil)", "##BoostedPot", ref _boostedPot);
            grid.Cell();
            HostField.Money("Total Pot (gil)", "##TotalPot", ref _totalPot);
            grid.Cell();
            HostField.Toggle("Chaos Mode", "##ChaosMode", ref _chaosMode);
            grid.Cell();
            HostField.Toggle("Multi Winner", "##MultiWinner", ref _multiWinner);
        }

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
}
