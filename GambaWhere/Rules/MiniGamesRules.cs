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
        if (ipcContext is BAR777Data bar777)
        {
            rules = new Dictionary<string, object>
            {
                ["gameType"] = bar777.GameLabel,
                ["boostedPot"] = bar777.BoostedPot,
                ["totalPot"] = bar777.TotalPot,
                ["costPerRoll"] = bar777.CostPerRoll,
                ["maxRolls"] = bar777.MaxRolls,
                ["playersPlayed"] = bar777.PlayersPlayed
            };
            if (bar777.Queue.HasValue)
                rules["queue"] = bar777.Queue.Value;
            return true;
        }

        if (ipcContext is DeathrollTournamentData deathroll)
        {
            rules = new Dictionary<string, object>
            {
                ["gameType"] = deathroll.GameLabel,
                ["round"] = deathroll.Round,
                ["boostedPot"] = deathroll.BoostedPot,
                ["totalPot"] = deathroll.TotalPot,
                ["entryCost"] = deathroll.EntryCost,
                ["playersEntered"] = deathroll.PlayersEntered
            };
            return true;
        }

        rules = null;
        return false;
    }

    public void DrawAutomaticRulesSummary(object? ipcContext)
    {
        if (ipcContext is BAR777Data bar777)
        {
            ImGui.Text($"Game: {bar777.GameLabel}");
            ImGui.Text($"Boosted Pot: {bar777.BoostedPot:N0}");
            ImGui.Text($"Total Pot: {bar777.TotalPot:N0}");
            ImGui.Text($"Cost Per Roll: {bar777.CostPerRoll:N0}");
            ImGui.Text($"Max Rolls: {bar777.MaxRolls:N0}");
            ImGui.Text($"Players Played: {bar777.PlayersPlayed:N0}");
            if (bar777.Queue.HasValue)
                ImGui.Text($"Queue: {bar777.Queue.Value:N0}");
            return;
        }

        if (ipcContext is DeathrollTournamentData deathroll)
        {
            ImGui.Text($"Game: {deathroll.GameLabel}");
            ImGui.Text($"Round: {deathroll.Round}");
            ImGui.Text($"Boosted Pot: {deathroll.BoostedPot:N0}");
            ImGui.Text($"Total Pot: {deathroll.TotalPot:N0}");
            ImGui.Text($"Entry Cost: {deathroll.EntryCost:N0}");
            ImGui.Text($"Players Entered: {deathroll.PlayersEntered:N0}");
            return;
        }

        ImGui.TextDisabled("No session has been started in MiniGamesEmporium.");
    }
}
