using System.Collections.Generic;
using GambaWhere.IPC;
using GambaWhere.Rules;

namespace GambaWhere.Services;

public sealed class AutomaticRulesIpcRefresher
{
    private readonly SimpleBingoIpc _bingoIpc;
    private readonly SimpleRouletteIpc _rouletteIpc;
    private readonly BingoRules _bingoRules = new();
    private readonly RouletteRules _rouletteRules = new();

    public AutomaticRulesIpcRefresher(SimpleBingoIpc bingoIpc, SimpleRouletteIpc rouletteIpc)
    {
        _bingoIpc = bingoIpc;
        _rouletteIpc = rouletteIpc;
    }

    public Dictionary<string, object>? TryRefresh(string gameType)
    {
        return gameType switch
        {
            "Bingo" => TryFromBingo(),
            "Roulette" => TryFromRoulette(),
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
}
