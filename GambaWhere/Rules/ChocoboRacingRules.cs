using System.Collections.Generic;
using GambaWhere.IPC;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

/// <summary>Rule configuration and automatic host rule source for Chocobo Racing.</summary>
public class ChocoboRacingRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Chocobo Racing";

    private int _chocoboRunners = 6;
    private int _raceTrackLength = 10;
    private int _maxBetPerChocobo = 500_000;
    private float _payoutOdds = 2.0f;
    private float _perfectRaceOdds = 5.0f;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##ChocoboGrid"))
        {
            grid.Cell();
            HostField.Int("Chocobo Runners", "##ChocoboRunners", ref _chocoboRunners);
            grid.Cell();
            HostField.Int("Race Track Length", "##RaceTrackLength", ref _raceTrackLength);
            grid.Cell();
            HostField.Money("Max Bet per Chocobo (gil)", "##MaxBetPerChocobo", ref _maxBetPerChocobo);
            grid.Cell();
            HostField.Float("Payout Odds", "##PayoutOdds", ref _payoutOdds);
            grid.Cell();
            HostField.Float("Perfect Race Odds", "##PerfectRaceOdds", ref _perfectRaceOdds);
        }

        _chocoboRunners = RuleClamp.Range(_chocoboRunners, 2, 20);
        _raceTrackLength = RuleClamp.Min(_raceTrackLength, 1);
        _maxBetPerChocobo = RuleClamp.Min(_maxBetPerChocobo, 0);
        _payoutOdds = RuleClamp.MinF(_payoutOdds, 0.0f);
        _perfectRaceOdds = RuleClamp.MinF(_perfectRaceOdds, 0.0f);
    }

    public Dictionary<string, object> ToApiPayload()
    {
        var payload = new Dictionary<string, object>
        {
            { "chocoboRunners", _chocoboRunners },
            { "raceTrackLength", _raceTrackLength },
            { "maxBetPerChocobo", _maxBetPerChocobo },
            { "payoutOdds", _payoutOdds }
        };
        if (_perfectRaceOdds > 0f)
            payload["perfectRaceOdds"] = _perfectRaceOdds;
        return payload;
    }

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _chocoboRunners = PresetReader.Int(values, "chocoboRunners", _chocoboRunners);
        _raceTrackLength = PresetReader.Int(values, "raceTrackLength", _raceTrackLength);
        _maxBetPerChocobo = PresetReader.Int(values, "maxBetPerChocobo", _maxBetPerChocobo);
        _payoutOdds = PresetReader.Float(values, "payoutOdds", _payoutOdds);
        _perfectRaceOdds = PresetReader.Float(values, "perfectRaceOdds", _perfectRaceOdds);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();

    public string AutomaticRulesPluginName => "ChocoboRacingGamba";

    public bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules)
    {
        if (ipcContext is not ChocoboRacingGambaData info)
        {
            rules = null;
            return false;
        }

        rules = new Dictionary<string, object>
        {
            ["chocoboRunners"] = info.ChocoboRunners,
            ["raceTrackLength"] = info.RaceTrackLength,
            ["maxBetPerChocobo"] = (int)System.Math.Min(info.MaxBetPerChocobo, int.MaxValue),
            ["payoutOdds"] = info.PayoutOdds,
        };
        if (info.PerfectRaceOdds > 0f)
            rules["perfectRaceOdds"] = info.PerfectRaceOdds;
        rules["currentPlayers"] = info.CurrentPlayers;
        return true;
    }
}
