using System.Collections.Generic;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for Poker.</summary>
public class PokerRules : IRuleConfig
{
    public string GameType => "Poker";

    private string _gameType = "String";
    private int _minBuyin = 1000000;
    private int _maxBuyin = 5000000;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##PokerGrid"))
        {
            grid.Cell();
            HostField.Text("Game Type", "##GameType", ref _gameType, 64);
            grid.Cell();
            HostField.Money("Min Buyin (gil)", "##MinBuyin", ref _minBuyin);
            grid.Cell();
            HostField.Money("Max Buyin (gil)", "##MaxBuyin", ref _maxBuyin);
        }

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
