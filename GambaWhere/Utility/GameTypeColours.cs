using System.Numerics;
using GambaWhere.Games;

namespace GambaWhere.Utility;

/// <summary>Colour palette keyed by game type, sourced from the central game catalogue.</summary>
public static class GameTypeColours
{
    private static readonly Vector4 YellowAccent = new(1f, 0.85f, 0f, 1f);

    public static (Vector4 Background, Vector4 Accent) ForGame(string gameType) =>
        GameCategories.ColoursFor(gameType);

    public static Vector4 PillBorderForGame(string? gameType) =>
        GameCategories.Find(gameType) is { } category ? category.Accent : YellowAccent;
}
