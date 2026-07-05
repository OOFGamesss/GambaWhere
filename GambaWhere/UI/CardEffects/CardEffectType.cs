using System.Numerics;

namespace GambaWhere.UI.CardEffects;

/// <summary>Identifies which animated card effect is active for a given profile or post.</summary>
public enum CardEffectType { None, Booster, Beta, Hearts, HighRoller }

public static class CardEffectResolver
{
    public static CardEffectType Resolve(string? style, bool booster) => style switch
    {
        "booster" => CardEffectType.Booster,
        "gwbeta" => CardEffectType.Beta,
        "hearts" => CardEffectType.Hearts,
        "highroller" => CardEffectType.HighRoller,
        null when booster => CardEffectType.Booster,
        _ => CardEffectType.None
    };

    public static Vector4? BaseColour(CardEffectType effect) => effect switch
    {
        CardEffectType.Booster => BoosterCardEffect.BaseColour,
        CardEffectType.Beta => null,
        CardEffectType.Hearts => null,
        CardEffectType.HighRoller => null,
        _ => null
    };
}
