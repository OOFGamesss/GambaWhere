using System.Numerics;

namespace GambaWhere.Utility;

/// <summary>Colour palette keyed by game type.</summary>
public static class GameTypeColours
{
    private static readonly Vector4 YellowAccent = new(1f, 0.85f, 0f, 1f);

    public static (Vector4 Background, Vector4 Accent) ForGame(string gameType) => gameType switch
    {
        "Bingo"          => (new Vector4(0.85f, 0.25f, 0.25f, 0.18f), new Vector4(1.00f, 0.50f, 0.50f, 1f)),
        "Blackjack"      => (new Vector4(0.25f, 0.50f, 0.90f, 0.18f), new Vector4(0.50f, 0.75f, 1.00f, 1f)),
        "Chocobo Racing" => (new Vector4(0.85f, 0.80f, 0.15f, 0.18f), new Vector4(1.00f, 0.95f, 0.30f, 1f)),
        "Mini Games"     => (new Vector4(0.20f, 0.80f, 0.40f, 0.18f), new Vector4(0.40f, 1.00f, 0.55f, 1f)),
        "Poker"          => (new Vector4(0.00f, 0.80f, 0.80f, 0.18f), new Vector4(0.00f, 1.00f, 1.00f, 1f)),
        "Roulette"       => (new Vector4(0.52f, 0.38f, 0.78f, 0.18f), new Vector4(0.82f, 0.68f, 1.00f, 1f)),
        "Scratchcards"   => (new Vector4(0.85f, 0.45f, 0.00f, 0.18f), new Vector4(1.00f, 0.60f, 0.00f, 1f)),
        "Spin the Wheel" => (new Vector4(0.90f, 0.60f, 0.70f, 0.18f), new Vector4(1.00f, 0.75f, 0.85f, 1f)),
        _                => (new Vector4(0.50f, 0.50f, 0.50f, 0.12f), new Vector4(0.75f, 0.75f, 0.75f, 1f)),
    };

    public static Vector4 PillBorderForGame(string? gameType) => gameType switch
    {
        "Bingo" or "Blackjack" or "Chocobo Racing" or "Mini Games" or
        "Poker" or "Roulette" or "Scratchcards" or "Spin the Wheel"
            => ForGame(gameType).Accent,
        _ => YellowAccent,
    };
}
