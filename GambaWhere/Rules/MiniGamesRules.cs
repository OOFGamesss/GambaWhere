using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using GambaWhere.IPC;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public enum MiniGame
{
    DeathRoll
}

public class MiniGamesRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Mini Games";

    private MiniGame _selectedGame = MiniGame.DeathRoll;
    private int _entryCost;

    private static readonly string[] RowLabels =
    {
        "Game",
        "Entry Cost (gil)"
    };

    public void Draw()
    {
        var offset = RowLabels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text(RowLabels[0]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##MiniGameSelect", MiniGameLabel(_selectedGame)))
        {
            foreach (var game in System.Enum.GetValues<MiniGame>())
            {
                if (ImGui.Selectable(MiniGameLabel(game), _selectedGame == game))
                    _selectedGame = game;
            }
            ImGui.EndCombo();
        }

        ImGui.Text(RowLabels[1]);
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGuiEx.InputFancyNumeric("##EntryCost", ref _entryCost, 0);

        _entryCost = RuleClamp.Min(_entryCost, 0);
    }

    private static string MiniGameLabel(MiniGame game) => game switch
    {
        MiniGame.DeathRoll => "Death Roll",
        _ => game.ToString()
    };

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "GameLabel", MiniGameLabel(_selectedGame) },
        { "entryCost", _entryCost }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        var label = PresetReader.String(values, "GameLabel", MiniGameLabel(_selectedGame));
        _selectedGame = label == "Death Roll" ? MiniGame.DeathRoll : _selectedGame;
        _entryCost = PresetReader.Int(values, "entryCost", _entryCost);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();

    public string AutomaticRulesPluginName => "MiniGamesEmporium";

    public bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules)
    {
        if (ipcContext is not MiniGamesEmporiumData info)
        {
            rules = null;
            return false;
        }

        rules = new Dictionary<string, object>
        {
            ["gameType"] = info.GameLabel,
            ["boostedPot"] = info.BoostedPot,
            ["totalPot"] = info.TotalPot,
            ["costPerRoll"] = info.CostPerRoll,
            ["playersPlayed"] = info.PlayersPlayed
        };

        if (info.Queue.HasValue)
            rules["queue"] = info.Queue.Value;
        return true;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is not MiniGamesEmporiumData info)
        {
            ImGui.TextDisabled("No session has been started in MiniGamesEmporium.");
            return;
        }

        ImGui.Text($"Game: {info.GameLabel}");
        ImGui.Text($"Boosted Pot: {info.BoostedPot:N0}");
        ImGui.Text($"Total Pot: {info.TotalPot:N0}");
        ImGui.Text($"Cost Per Roll: {info.CostPerRoll:N0}");
        ImGui.Text($"Players Played: {info.PlayersPlayed:N0}");
        if (info.Queue.HasValue)
            ImGui.Text($"Queue: {info.Queue.Value:N0}");
    }
}
