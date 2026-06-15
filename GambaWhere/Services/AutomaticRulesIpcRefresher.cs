using System.Collections.Generic;
using GambaWhere.IPC;
using GambaWhere.Rules;

namespace GambaWhere.Services;

/// <summary>Refreshes automatic host rules from connected game plugins over IPC.</summary>
public sealed class AutomaticRulesIpcRefresher
{
    private readonly SimpleBingoIpc _bingoIpc;
    private readonly SimpleRouletteIpc _rouletteIpc;
    private readonly ChocoboRacingGambaIpc _chocoboIpc;
    private readonly MiniGamesEmporiumIpc _miniGamesIpc;
    private readonly BingoRules _bingoRules = new();
    private readonly RouletteRules _rouletteRules = new();
    private readonly ChocoboRacingRules _chocoboRules = new();
    private readonly MiniGamesRules _miniGamesRules = new();

    public AutomaticRulesIpcRefresher(
        SimpleBingoIpc bingoIpc,
        SimpleRouletteIpc rouletteIpc,
        ChocoboRacingGambaIpc chocoboIpc,
        MiniGamesEmporiumIpc miniGamesIpc)
    {
        _bingoIpc = bingoIpc;
        _rouletteIpc = rouletteIpc;
        _chocoboIpc = chocoboIpc;
        _miniGamesIpc = miniGamesIpc;
    }

    public Dictionary<string, object>? TryRefresh(string gameType)
    {
        return gameType switch
        {
            "Bingo" => TryFromBingo(),
            "Roulette" => TryFromRoulette(),
            "Chocobo Racing" => TryFromChocoboRacing(),
            "Mini Games" => TryFromMiniGames(),
            _ => null
        };
    }

    private Dictionary<string, object>? TryFromBingo()
    {
        var info = _bingoIpc.GetGameInfo(true);
        return _bingoRules.TryGetAutomaticApiRules(info, out var rules) ? rules : null;
    }

    private Dictionary<string, object>? TryFromRoulette()
    {
        var info = _rouletteIpc.GetGameInfo(true);
        return _rouletteRules.TryGetAutomaticApiRules(info, out var rules) ? rules : null;
    }

    private Dictionary<string, object>? TryFromChocoboRacing()
    {
        var info = _chocoboIpc.GetGameInfo(true);
        return _chocoboRules.TryGetAutomaticApiRules(info, out var rules) ? rules : null;
    }

    private Dictionary<string, object>? TryFromMiniGames()
    {
        var info = _miniGamesIpc.GetGameInfo(true);
        return _miniGamesRules.TryGetAutomaticApiRules(info, out var rules) ? rules : null;
    }
}
