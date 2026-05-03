using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class ChocoboRacingRules : IRuleConfig
{
    public string GameType => "Chocobo Racing";

    private int _chocoboRunners = 6;
    private int _raceTrackLength = 10;
    private float _payoutOdds = 2.0f;
    private float _perfectRaceOdds = 5.0f;

    private static readonly string[] Labels =
    {
        "Chocobo Runners", "Race Track Length", "Payout Odds", "Perfect Race Odds"
    };

    public void Draw()
    {
        var offset = Labels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text("Chocobo Runners");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##ChocoboRunners", ref _chocoboRunners);

        ImGui.Text("Race Track Length");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##RaceTrackLength", ref _raceTrackLength);

        ImGui.Text("Payout Odds");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("##PayoutOdds", ref _payoutOdds, 0.1f, 1.0f, "%.2f");

        ImGui.Text("Perfect Race Odds");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("##PerfectRaceOdds", ref _perfectRaceOdds, 0.1f, 1.0f, "%.2f");

        _chocoboRunners = RuleClamp.Range(_chocoboRunners, 2, 20);
        _raceTrackLength = RuleClamp.Min(_raceTrackLength, 1);
        _payoutOdds = RuleClamp.MinF(_payoutOdds, 0.0f);
        _perfectRaceOdds = RuleClamp.MinF(_perfectRaceOdds, 0.0f);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "chocoboRunners", _chocoboRunners },
        { "raceTrackLength", _raceTrackLength },
        { "payoutOdds", _payoutOdds },
        { "perfectRaceOdds", _perfectRaceOdds }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _chocoboRunners = PresetReader.Int(values, "chocoboRunners", _chocoboRunners);
        _raceTrackLength = PresetReader.Int(values, "raceTrackLength", _raceTrackLength);
        _payoutOdds = PresetReader.Float(values, "payoutOdds", _payoutOdds);
        _perfectRaceOdds = PresetReader.Float(values, "perfectRaceOdds", _perfectRaceOdds);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
