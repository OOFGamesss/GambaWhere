using System;
using System.Collections.Generic;
using System.Linq;

namespace GambaWhere.Games;

/// <summary>
/// The registry of games. Each Game has its display card, its category (in GameCategories.cs, which owns the shared
/// manual rules), and optional companion IPC (the WindowOpened prompt plus its own automatic rules). Manual rules live
/// on the category, not here.
/// </summary>
public static class GameCatalog
{
    private const string SimpleGamba = "https://simple.gamba.pro/#games";
    private const string OofGames = "https://oofgames.fyi";
    private const string Asuna = "https://puni.sh/api/repository/asuna";

    // ==========================================================================================
    // ADD A GAME HERE: one Game per offering, in a Category (see GameCategories.cs).
    //   Category, CompanionPlugin, Description, Creator, Url, IconFile : the Games-tab card (IconFile under Images/).
    //   IpcBaseName   : companion IPC base, e.g. "SimpleBingo" -> ".WindowOpened"/".GetGameInfo"/".GameJoined". Drives the
    //                   auto-session prompt; null = display-only. (LinkId auto-assigned by PartnerIpcManager.)
    //   AutomaticFields : this game's automatic rules. Each maps an IPC JSON value to a rule key (Name case-insensitive;
    //                     Source/Multiplier/SkipIfMissing/SkipIfZero/SpacesFromUnderscores/AutoType transform it). A Name
    //                     shared with a category manual field auto-captures that value. Null = no automatic rules.
    //   UsesGameJoined: subscribe to "{base}.GameJoined" to refresh the rules cache.
    //   AutomaticShapesOverride / InvalidationKeysOverride : only for odd companions (e.g. Mini Games' two info keys).
    // ==========================================================================================
    public static readonly IReadOnlyList<Game> Games = new Game[]
    {
        new("Aviator", "SimpleAviator",
            "A fast-paced aviation casino game where every second counts!",
            "Asuna & Klia", SimpleGamba, "Games/simpleaviator.png",
            IpcBaseName: "SimpleAviator"),

        new("Bingo", "SimpleBingo",
            "Interactive bingo with automated ball calling, multiple card support, and real-time winner detection.",
            "Asuna & Klia", SimpleGamba, "Games/simplebingo.png",
            AutomaticFields: new RuleField[]
            {
                new("gameType", AutoType: RuleValueType.String, SpacesFromUnderscores: true),
                new("cardCost", AutoType: RuleValueType.Long),
                new("boostedPot", AutoType: RuleValueType.Long),
                new("totalPot", AutoType: RuleValueType.Long),
                new("chaosMode", AutoType: RuleValueType.Bool),
                new("multiWinner", AutoType: RuleValueType.Bool),
                new("cardsSold", AutoType: RuleValueType.Int),
                new("playerCount", AutoType: RuleValueType.Int),
            },
            IpcBaseName: "SimpleBingo", UsesGameJoined: true),

        new("Blackjack", "SimpleBlackjack",
            "Professional blackjack with multiple hands, full game customization, and advanced betting options.",
            "Asuna & Klia", SimpleGamba, "Games/simpleblackjack.png",
            IpcBaseName: "SimpleBlackjack"),

        new("Chocobo Racing", "Chocobo Racing",
            "Fully Customisable racing game with auto detected bets, bank management and much more.",
            "OOF Games", OofGames, "Games/chocoboracinggamba.png",
            AutomaticFields: new RuleField[]
            {
                new("chocoboRunners", AutoType: RuleValueType.Int),
                new("raceTrackLength", AutoType: RuleValueType.Int),
                new("maxBetPerChocobo", AutoType: RuleValueType.Long),
                new("payoutOdds", AutoType: RuleValueType.Float),
                new("perfectRaceOdds", AutoType: RuleValueType.Float, SkipIfZero: true),
                new("currentPlayers", AutoType: RuleValueType.Int),
            },
            IpcBaseName: "ChocoboRacingGamba"),

        new("Mini Games", "Mini Games Emporium",
            "Casual mini bar-style games designed for quick rounds, simple interactions, and social-friendly gameplay.",
            "OOF Games", OofGames, "Games/minigamesemporium.png",
            IpcBaseName: "MiniGamesEmporium",
            AutomaticShapesOverride: new[]
            {
                new AutomaticRuleShape("MiniGamesEmporium.Bar777.GetInfo", new RuleField[]
                {
                    new("gameType", Source: "GameLabel", AutoType: RuleValueType.String),
                    new("boostedPot", Source: "BoostedPot", AutoType: RuleValueType.Long),
                    new("totalPot", Source: "TotalPot", AutoType: RuleValueType.Long),
                    new("costPerRoll", Source: "CostPerRoll", AutoType: RuleValueType.Long),
                    new("maxRolls", Source: "MaxRolls", AutoType: RuleValueType.Int),
                    new("playersPlayed", Source: "PlayersPlayed", AutoType: RuleValueType.Int),
                    new("queue", Source: "Queue", AutoType: RuleValueType.Int, SkipIfMissing: true),
                }),
                new AutomaticRuleShape("MiniGamesEmporium.DeathrollTournament.GetInfo", new RuleField[]
                {
                    new("gameType", Source: "GameLabel", AutoType: RuleValueType.String),
                    new("round", Source: "Round", AutoType: RuleValueType.String),
                    new("boostedPot", Source: "BoostedPot", AutoType: RuleValueType.Long),
                    new("totalPot", Source: "TotalPot", AutoType: RuleValueType.Long),
                    new("entryCost", Source: "EntryCost", AutoType: RuleValueType.Long),
                    new("playersEntered", Source: "PlayersEntered", AutoType: RuleValueType.Int),
                }),
            },
            InvalidationKeysOverride: new[] { "MiniGamesEmporium.Bar777.GetInfo", "MiniGamesEmporium.DeathrollTournament.GetInfo" }),

        new("Mini Games", "SimpleMiniGames",
            "DICE! GAMES! UNO! Cards Against Humanity",
            "Asuna & Klia", Asuna, "Games/simpleminigames.png"),

        new("Poker", "SimplePoker",
            "Professional Texas Hold'em poker with full customization and immersive table management.",
            "Asuna & Klia", SimpleGamba, "Games/simplepoker.png",
            IpcBaseName: "SimplePoker"),

        new("Roulette", "SimpleRoulette",
            "Classic European roulette with live dealer controls, real-time stats, and spectator chat.",
            "Asuna & Klia", SimpleGamba, "Games/simpleroulette.png",
            AutomaticFields: new RuleField[]
            {
                new("maxBetInner", AutoType: RuleValueType.Long, Multiplier: 1000, SkipIfMissing: true),
                new("maxBetOuter", AutoType: RuleValueType.Long, Multiplier: 1000, SkipIfMissing: true),
                new("maxBetInnerVIP", AutoType: RuleValueType.Long, Multiplier: 1000, SkipIfMissing: true),
                new("maxBetOuterVIP", AutoType: RuleValueType.Long, Multiplier: 1000, SkipIfMissing: true),
                new("playerCount", AutoType: RuleValueType.Int),
            },
            IpcBaseName: "SimpleRoulette", UsesGameJoined: true),

        new("Scratchcards", "SimpleScratch",
            "Fully customizable scratcher game with configurable prizes, adjustable odds, and custom images.",
            "Asuna & Klia", SimpleGamba, "Games/simplescratch.png",
            IpcBaseName: "SimpleScratch"),

        new("Spin the Wheel", "SimpleWheel",
            "Customizable spin to win wheels with variable segments, custom images, and exciting prize distribution options.",
            "Asuna & Klia", SimpleGamba, "Games/simplewheel.png",
            IpcBaseName: "SimpleWheel"),
    };

    public static readonly IReadOnlyList<Game> DisplayGames = Games.Where(IsDisplayable).ToList();

    public static readonly IReadOnlyList<Game> IpcGames = Games.Where(g => g.HasIpc).ToList();

    static GameCatalog()
    {
        foreach (var game in Games)
            System.Diagnostics.Debug.Assert(
                GameCategories.Find(game.Category) != null,
                $"Game '{game.CompanionPlugin}' uses category '{game.Category}', which has no matching GameCategory in GameCategories.cs.");
    }

    public static Game? GameForCategory(string key) =>
        Games.FirstOrDefault(g => g.Category == key && g.HasAutomaticRules)
        ?? Games.FirstOrDefault(g => g.Category == key);

    public static bool HasAutomaticRules(string? key) =>
        key != null && GameForCategory(key)?.HasAutomaticRules == true;

    public static IRuleConfig[] CreateRuleConfigs() =>
        GameCategories.All.Select(c =>
            (IRuleConfig)new DataRuleConfig(c.Key, c.ManualFields ?? Array.Empty<RuleField>(), c.EmptyRulesMessage)).ToArray();

    private static bool IsDisplayable(Game game) =>
        !string.IsNullOrWhiteSpace(game.Category)
        && !string.IsNullOrWhiteSpace(game.CompanionPlugin)
        && !string.IsNullOrWhiteSpace(game.Description)
        && !string.IsNullOrWhiteSpace(game.Creator)
        && !string.IsNullOrWhiteSpace(game.Url);
}
