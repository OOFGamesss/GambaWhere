using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.IPC;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class ChocoboRacingRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Chocobo Racing";

    private int _chocoboRunners = 6;
    private int _raceTrackLength = 10;
    private int _maxBetPerChocobo = 500_000;
    private float _payoutOdds = 2.0f;
    private float _perfectRaceOdds = 5.0f;

    private static readonly string[] Labels =
    {
        "Chocobo Runners", "Race Track Length", "Max Bet per Chocobo (gil)", "Payout Odds", "Perfect Race Odds"
    };

    public void Draw()
    {
        var offset = Labels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(Labels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##ChocoboRunners", ref _chocoboRunners);

        ImGui.Text(Labels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RaceTrackLength", ref _raceTrackLength);

        ImGui.Text(Labels[2]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxBetPerChocobo", ref _maxBetPerChocobo);

        ImGui.Text(Labels[3]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("##PayoutOdds", ref _payoutOdds, 0.1f, 1.0f, "%.2f");

        ImGui.Text(Labels[4]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("##PerfectRaceOdds", ref _perfectRaceOdds, 0.1f, 1.0f, "%.2f");

        _chocoboRunners = RuleClamp.Range(_chocoboRunners, 2, 20);
        _raceTrackLength = RuleClamp.Min(_raceTrackLength, 1);
        _maxBetPerChocobo = RuleClamp.Min(_maxBetPerChocobo, 0);
        _payoutOdds = RuleClamp.MinF(_payoutOdds, 0.0f);
        _perfectRaceOdds = RuleClamp.MinF(_perfectRaceOdds, 0.0f);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "chocoboRunners", _chocoboRunners },
        { "raceTrackLength", _raceTrackLength },
        { "maxBetPerChocobo", _maxBetPerChocobo },
        { "payoutOdds", _payoutOdds },
        { "perfectRaceOdds", _perfectRaceOdds }
    };

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
        if (ipcContext is not RaceGameInfoIPC info)
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
            ["perfectRaceOdds"] = info.PerfectRaceOdds,
            ["currentPlayers"] = info.CurrentPlayers
        };
        return true;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is not RaceGameInfoIPC raceInfo)
        {
            ImGui.TextDisabled("No session has been started");
            return;
        }

        ImGui.Text("Chocobo Racing session info:");
        ImGui.Text($"Chocobo runners: {raceInfo.ChocoboRunners}");
        ImGui.Text($"Race track length: {raceInfo.RaceTrackLength}");
        ImGui.Text($"Max bet per chocobo: {raceInfo.MaxBetPerChocobo:N0}");
        ImGui.Text($"Payout odds: {raceInfo.PayoutOdds:N2}x");
        if (raceInfo.PerfectRaceOdds <= 0f)
            ImGui.Text("Perfect race: off");
        else
            ImGui.Text($"Perfect race odds: {raceInfo.PerfectRaceOdds:N2}x");

        ImGui.Text($"Current players: {raceInfo.CurrentPlayers}");
    }
}
