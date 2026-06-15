using System;
using System.Numerics;

namespace GambaWhere.Utility;

/// <summary>Resolves theme colours from the user's configured primary and secondary colours.</summary>
public static class ThemeColours
{
    public static Vector4 TintedWindowBg(Vector4 p) => BlendWithDark(p, 0.09f, 0.96f);
    public static Vector4 TintedPopupBg(Vector4 p) => BlendWithDark(p, 0.11f, 0.98f);

    public static Vector4 TabNormal(Vector4 p) => new(p.X, p.Y, p.Z, 0.14f);
    public static Vector4 TabHovered(Vector4 p) => new(p.X, p.Y, p.Z, 0.28f);
    public static Vector4 TabSelected(Vector4 p) => new(p.X, p.Y, p.Z, 0.42f);
    public static Vector4 TabUnfocused(Vector4 p) => new(p.X, p.Y, p.Z, 0.06f);

    public static Vector4 ButtonNormal(Vector4 p) => new(p.X, p.Y, p.Z, 0.18f);
    public static Vector4 ButtonHovered(Vector4 p) => new(p.X, p.Y, p.Z, 0.30f);
    public static Vector4 ButtonPressed(Vector4 p) => new(p.X, p.Y, p.Z, 0.45f);

    public static Vector4 TitleBg(Vector4 p) => new(p.X, p.Y, p.Z, 0.12f);
    public static Vector4 TitleBgActive(Vector4 p) => new(p.X, p.Y, p.Z, 0.22f);

    public static Vector4 ScrollbarGrab(Vector4 p) => new(p.X, p.Y, p.Z, 0.30f);
    public static Vector4 ScrollbarGrabHovered(Vector4 p) => new(p.X, p.Y, p.Z, 0.50f);
    public static Vector4 ScrollbarGrabActive(Vector4 p) => new(p.X, p.Y, p.Z, 0.70f);

    public static Vector4 CardBackground(Vector4 p) => new(p.X, p.Y, p.Z, 0.12f);
    public static Vector4 SectionActiveBg(Vector4 p) => new(p.X, p.Y, p.Z, 0.18f);
    public static Vector4 SectionInactiveBg(Vector4 p) => new(p.X, p.Y, p.Z, 0.07f);
    public static Vector4 ActiveBorder(Vector4 p) => new(p.X, p.Y, p.Z, 0.85f);
    public static Vector4 InactiveBorder(Vector4 p) => new(p.X, p.Y, p.Z, 0.35f);
    public static Vector4 ActiveFrameBg(Vector4 p) => new(p.X, p.Y, p.Z, 0.08f);
    public static Vector4 ActiveFrameBgHovered(Vector4 p) => new(p.X, p.Y, p.Z, 0.14f);
    public static Vector4 ActiveFrameBgActive(Vector4 p) => new(p.X, p.Y, p.Z, 0.20f);
    public static Vector4 FaqHeaderNormal(Vector4 p) => new(p.X, p.Y, p.Z, 0.18f);
    public static Vector4 FaqHeaderHovered(Vector4 p) => new(p.X, p.Y, p.Z, 0.26f);
    public static Vector4 FaqHeaderActive(Vector4 p) => new(p.X, p.Y, p.Z, 0.34f);
    public static Vector4 SectionSeparator(Vector4 p) => new(p.X, p.Y, p.Z, 0.50f);

    public static Vector4 AccentText(Vector4 s) => s;
    public static Vector4 AccentTextMuted(Vector4 s) => new(s.X, s.Y, s.Z, 0.75f);
    public static Vector4 ActiveCheckMark(Vector4 s) => s;
    public static Vector4 InactiveCheckMark(Vector4 s) => new(s.X, s.Y, s.Z, 0.65f);

    private static Vector4 BlendWithDark(Vector4 p, float strength, float alpha)
    {
        const float Dark = 0.07f;
        return new(
            Math.Min(1f, Dark + p.X * strength),
            Math.Min(1f, Dark + p.Y * strength),
            Math.Min(1f, Dark + p.Z * strength),
            alpha);
    }
}
