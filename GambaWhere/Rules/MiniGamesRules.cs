using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using GambaWhere.IPC;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

/// <summary>Rule configuration for the Mini Games Emporium game types.</summary>
public enum MiniGame
{
    DeathRoll
}

public class MiniGamesRules : IRuleConfig, IAutomaticHostRuleSource
{
    public string GameType => "Mini Games";

    private MiniGame _selectedGame = MiniGame.DeathRoll;
    private int _entryCost;

    public void Draw()
    {
        using (var grid = RuleGrid.Begin("##MiniGamesGrid"))
        {
            grid.Cell();
            HostField.Combo("Game", "##MiniGameSelect", MiniGameLabel(_selectedGame), () =>
            {
                foreach (var game in System.Enum.GetValues<MiniGame>())
                {
                    if (ImGui.Selectable(MiniGameLabel(game), _selectedGame == game))
                        _selectedGame = game;
                }
            });
            grid.Cell();
            HostField.Money("Entry Cost (gil)", "##EntryCost", ref _entryCost);
        }

        _entryCost = RuleClamp.Min(_entryCost, 0);
    }

    private static string MiniGameLabel(MiniGame game) => game switch
    {
        MiniGame.DeathRoll => "Deathroll",
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
        _selectedGame = label == "Deathroll" ? MiniGame.DeathRoll : _selectedGame;
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
}
