using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GambaWhere.Games;

/// <summary>A game category (Bingo, Blackjack, ...): its colour/theme bucket and the manual rules every game in it shares.</summary>
public sealed record GameCategory(
    string Key,
    Vector4 Background,
    Vector4 Accent,
    int DiscordColour,
    string Emoji,
    string DiscordEmoji,
    string BannerUrl,
    IReadOnlyList<RuleField>? ManualFields = null,
    string? EmptyRulesMessage = null);

public static class GameCategories
{
    private static readonly Vector4 DefaultBackground = new(0.50f, 0.50f, 0.50f, 0.12f);
    private static readonly Vector4 DefaultAccent = new(0.75f, 0.75f, 0.75f, 1f);

    public static readonly IReadOnlyList<GameCategory> Fallbacks = new GameCategory[]
    {
        new("Aviator", new(0.72f, 0.88f, 1.00f, 0.18f), new(0.78f, 0.91f, 1.00f, 1f), 0xB8E0FF, "✈️", "✈️", "",
            ManualFields: new RuleField[]
            {
                new("minBet", RuleKind.Money, "Min Bet (gil)", 10000, Min: 0),
                new("maxBet", RuleKind.Money, "Max Bet (gil)", 1000000, Min: 0),
            }),

        new("Bingo", new(0.85f, 0.25f, 0.25f, 0.18f), new(1.00f, 0.50f, 0.50f, 1f), 0xE03030, "🎱", "<a:Bingo:1500593652358189227>", "",
            ManualFields: new RuleField[]
            {
                new("gameType", RuleKind.Text, "Game Type", "Full Board"),
                new("cardCost", RuleKind.Money, "Card Cost (gil)", 200000, Min: 0),
                new("boostedPot", RuleKind.Money, "Boosted Pot (gil)", 10000000, Min: 0),
                new("totalPot", RuleKind.Money, "Total Pot (gil)", 10000000, Min: 0),
                new("chaosMode", RuleKind.Toggle, "Chaos Mode", false),
                new("multiWinner", RuleKind.Toggle, "Multi Winner", false),
            }),

        new("Blackjack", new(0.25f, 0.50f, 0.90f, 0.18f), new(0.50f, 0.75f, 1.00f, 1f), 0x3060D0, "🃏", "<a:Blackjack:1500593592668782622>", "",
            ManualFields: new RuleField[]
            {
                new("maxBet", RuleKind.Money, "Max Bet (gil)", 1000000, Min: 0),
                new("maxPush", RuleKind.Money, "Max Push (gil)", 1000000, Min: 0),
                new("standsSoftOn", RuleKind.Int, "Stands Soft On", 17, Min: 1, Max: 21),
                new("standsHardOn", RuleKind.Int, "Stands Hard On", 17, Min: 1, Max: 21),
                new("maxSplits", RuleKind.Int, "Max Splits", 2, Min: 0),
                new("allowNonMatchingSplits", RuleKind.Toggle, "Allow Non-Matching Splits", false),
                new("5 Card Charlie", RuleKind.Toggle, "5 Card Charlie", false),
            }),

        new("Chocobo Racing", new(0.85f, 0.80f, 0.15f, 0.18f), new(1.00f, 0.95f, 0.30f, 1f), 0xF0C030, "🏁", "<a:ChocoboRacing:1500593790040412391>", "",
            ManualFields: new RuleField[]
            {
                new("chocoboRunners", RuleKind.Int, "Chocobo Runners", 5, Min: 2, Max: 20),
                new("raceTrackLength", RuleKind.Int, "Race Track Length", 5, Min: 1),
                new("maxBetPerChocobo", RuleKind.Money, "Max Bet per Chocobo (gil)", 500000, Min: 0),
                new("payoutOdds", RuleKind.Float, "Payout Odds", 5.0f, Min: 0),
                new("perfectRaceOdds", RuleKind.Float, "Perfect Race Odds", 25.0f, Min: 0),
            }),

        new("Mini Games", new(0.20f, 0.80f, 0.40f, 0.18f), new(0.40f, 1.00f, 0.55f, 1f), 0x30A050, "🎲", "<a:MiniGames:1500593760654983300>", "",
            ManualFields: new RuleField[]
            {
                new("gameType", RuleKind.Text, "Game", "Deathroll", TextMax: 64),
                new("entryCost", RuleKind.Money, "Entry Cost (gil)", 0, Min: 0),
            }),

        new("Poker", new(0.75f, 0.75f, 0.78f, 0.18f), new(0.88f, 0.88f, 0.92f, 1f), 0xC0C0C0, "♠️", "<:poker:1501971290037289092>", "",
            ManualFields: new RuleField[]
            {
                new("gameType", RuleKind.Text, "Game Type", "Sit N Go"),
                new("minBuyin", RuleKind.Money, "Min Buyin (gil)", 1000000, Min: 0),
                new("maxBuyin", RuleKind.Money, "Max Buyin (gil)", 5000000, Min: 0),
            }),

        new("Roulette", new(0.52f, 0.38f, 0.78f, 0.18f), new(0.82f, 0.68f, 1.00f, 1f), 0x8B5CF6, "🔢", "<a:roulette:1501971297175867422>", "",
            ManualFields: new RuleField[]
            {
                new("maxBetInner", RuleKind.Money, "Max Bet Inner (gil)", 100000, Min: 0),
                new("maxBetOuter", RuleKind.Money, "Max Bet Outer (gil)", 100000, Min: 0),
                new("maxBetInnerVIP", RuleKind.Money, "Max Bet Inner VIP (gil)", 200000, Min: 0),
                new("maxBetOuterVIP", RuleKind.Money, "Max Bet Outer VIP (gil)", 200000, Min: 0),
            }),

        new("Scratchcards", new(0.85f, 0.45f, 0.00f, 0.18f), new(1.00f, 0.60f, 0.00f, 1f), 0xF97316, "🎫", "<a:scratchcards:1501971293757636782>", "",
            ManualFields: new RuleField[]
            {
                new("cardCost", RuleKind.Money, "Card Cost (gil)", 200000, Min: 0),
                new("jackpot", RuleKind.Money, "Jackpot (gil)", 3000000, Min: 0),
                new("topPrize", RuleKind.ItemSearch, "Top Prize", 0u),
            }),

        new("Spin the Wheel", new(0.90f, 0.60f, 0.70f, 0.18f), new(1.00f, 0.75f, 0.85f, 1f), 0xEC4899, "🎡", "<a:spinthewheel:1501971291257704728>", "",
            EmptyRulesMessage: "Contact Felix to add rules to this section."),
    };

    private static IReadOnlyList<GameCategory> _all = Fallbacks;
    private static string[] _keys = Fallbacks.Select(c => c.Key).ToArray();

    public static event Action? Changed;

    public static IReadOnlyList<GameCategory> All => _all;

    public static string[] Keys => _keys;

    public static void ReplaceAll(IReadOnlyList<GameCategory> categories)
    {
        if (categories == null || categories.Count == 0)
            return;

        _all = categories;
        _keys = categories.Select(c => c.Key).ToArray();
        Changed?.Invoke();
    }

    public static GameCategory? Find(string? key) =>
        key == null ? null : _all.FirstOrDefault(c => c.Key == key);

    public static (Vector4 Background, Vector4 Accent) ColoursFor(string? key)
    {
        var category = Find(key);
        return category != null ? (category.Background, category.Accent) : (DefaultBackground, DefaultAccent);
    }
}
