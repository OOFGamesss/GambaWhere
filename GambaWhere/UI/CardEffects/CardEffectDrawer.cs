using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.CardEffects;

/// <summary>Central dispatcher for all card effect draw calls.</summary>
public static class CardEffectDrawer
{
    public static void DrawFill(ImDrawListPtr dl, CardEffectType effect, Vector2 min, Vector2 max, double time, uint seed)
    {
        switch (effect)
        {
            case CardEffectType.Booster:
                BoosterCardEffect.DrawHolographicFill(dl, min, max, time, seed);
                break;
            case CardEffectType.Beta:
                BetaCardEffect.DrawLightningFill(dl, min, max, time, seed);
                break;
        }
    }

    public static void DrawFoil(ImDrawListPtr dl, CardEffectType effect, Vector2 min, Vector2 max, double time)
    {
        switch (effect)
        {
            case CardEffectType.Booster:
                BoosterCardEffect.DrawHolographicFoil(dl, min, max, time);
                break;
            case CardEffectType.Beta:
                BetaCardEffect.DrawLightningFoil(dl, min, max, time);
                break;
        }
    }

    public static void DrawBorder(ImDrawListPtr dl, CardEffectType effect, Vector2 min, Vector2 max, float rounding, double time)
    {
        switch (effect)
        {
            case CardEffectType.Booster:
                BoosterCardEffect.DrawHolographicBorder(dl, min, max, rounding, time);
                break;
            case CardEffectType.Beta:
                BetaCardEffect.DrawLightningBorder(dl, min, max, rounding, time);
                break;
        }
    }

    public static void DrawBorderAfterChildWindow(CardEffectType effect, Vector2 min, Vector2 max, float rounding, double time)
    {
        if (effect == CardEffectType.None) return;
        var dl = ImGui.GetWindowDrawList();
        var bleed = 10f * ImGuiHelpers.GlobalScale;
        var wMin = ImGui.GetWindowPos();
        var wMax = wMin + ImGui.GetWindowSize();
        dl.PushClipRect(
            Vector2.Max(min - new Vector2(bleed, bleed), wMin),
            Vector2.Min(max + new Vector2(bleed, bleed), wMax),
            false);
        DrawBorder(dl, effect, min, max, rounding, time);
        dl.PopClipRect();
    }
}
