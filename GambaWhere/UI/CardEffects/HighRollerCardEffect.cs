using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.CardEffects;

/// <summary>High Roller Card Effect showing a roulette wheel.</summary>
public static class HighRollerCardEffect
{
    private static readonly Vector4[] NeonPalette =
    [
        new(0.96f, 0.80f, 0.28f, 1f),
        new(0.97f, 0.30f, 0.34f, 1f),
        new(0.34f, 0.92f, 0.48f, 1f),
        new(0.34f, 0.58f, 0.99f, 1f),
        new(0.74f, 0.44f, 0.97f, 1f),
        new(0.32f, 0.92f, 0.93f, 1f),
    ];

    private static readonly int[] WheelSequence =
    [
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23,
        10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
    ];

    private static readonly bool[] IsRed = BuildRedTable();

    private static readonly Vector4 Silver = new(0.80f, 0.82f, 0.88f, 1f);
    private static readonly Vector4 Red = new(0.82f, 0.16f, 0.18f, 1f);
    private static readonly Vector4 Black = new(0.05f, 0.05f, 0.07f, 1f);
    private static readonly Vector4 Green = new(0.12f, 0.62f, 0.30f, 1f);

    private const float PocketAlpha = 0.055f;
    private const float FretAlpha = 0.12f;
    private const float RimAlpha = 0.14f;
    private const float NumberAlpha = 0.13f;

    public static void DrawHighRollerFill(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        dl.PushClipRect(min, max, true);
        DrawWheel(dl, min, max, time);
        dl.PopClipRect();
    }

    public static void DrawHighRollerFoil(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        dl.PushClipRect(min, max, true);
        DrawWheel(dl, min, max, time);
        dl.PopClipRect();
    }

    private static void DrawWheel(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        var t = (float)time;
        var c = (min + max) * 0.5f;
        var radius = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.46f;

        DrawGlowRing(dl, c, radius, t);
        DrawPockets(dl, c, radius);
        DrawRims(dl, c, radius);
        DrawHub(dl, c, radius);
    }

    private static void DrawGlowRing(ImDrawListPtr dl, Vector2 c, float radius, float t)
    {
        var col = CycleColour(t);
        dl.AddCircle(c, radius * 1.05f, ImGui.GetColorU32(col with { W = 0.055f }), 72, 6f);
        dl.AddCircle(c, radius * 1.05f, ImGui.GetColorU32(col with { W = 0.03f }), 72, 13f);
    }

    private static void DrawPockets(ImDrawListPtr dl, Vector2 c, float radius)
    {
        var n = WheelSequence.Length;
        var step = MathF.Tau / n;
        var innerR = radius * 0.70f;
        var outerR = radius * 0.99f;
        var numR = (innerR + outerR) * 0.5f;
        var font = ImGui.GetFont();
        var fontSize = radius * 0.088f;
        var fret = ImGui.GetColorU32(Silver with { W = FretAlpha });

        for (var i = 0; i < n; i++)
        {
            var a0 = i * step;
            var a1 = a0 + step;
            var d0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0));
            var d1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1));

            var num = WheelSequence[i];
            var fill = num == 0 ? Green : IsRed[num] ? Red : Black;
            dl.AddQuadFilled(c + d0 * innerR, c + d0 * outerR, c + d1 * outerR, c + d1 * innerR,
                ImGui.GetColorU32(fill with { W = PocketAlpha }));
            dl.AddLine(c + d0 * innerR, c + d0 * outerR, fret, 1.3f);

            var mid = (a0 + a1) * 0.5f;
            var np = c + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * numR;
            DrawCentredNumber(dl, font, fontSize, np, num);
        }
    }

    private static void DrawCentredNumber(ImDrawListPtr dl, ImFontPtr font, float fontSize, Vector2 centre, int num)
    {
        var text = num.ToString();
        var half = ImGui.CalcTextSize(text) * (fontSize / ImGui.GetFontSize()) * 0.5f;
        dl.AddText(font, fontSize, centre - half, ImGui.GetColorU32(Silver with { W = NumberAlpha }), text);
    }

    private static void DrawRims(ImDrawListPtr dl, Vector2 c, float radius)
    {
        var silver = ImGui.GetColorU32(Silver with { W = RimAlpha });
        dl.AddCircle(c, radius * 0.99f, silver, 72, 2f);
        dl.AddCircle(c, radius * 1.05f, silver, 72, 2.5f);
        dl.AddCircle(c, radius * 0.70f, silver, 56, 1.5f);
        dl.AddCircle(c, radius * 0.62f, ImGui.GetColorU32(Silver with { W = RimAlpha * 0.7f }), 56, 1f);
    }

    private static void DrawHub(ImDrawListPtr dl, Vector2 c, float radius)
    {
        var hubR = radius * 0.30f;
        var innerR = radius * 0.62f;
        var silver = ImGui.GetColorU32(Silver with { W = RimAlpha });
        var spoke = ImGui.GetColorU32(Silver with { W = RimAlpha * 0.85f });

        for (var k = 0; k < 8; k++)
        {
            var a = k * (MathF.Tau / 8f);
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            dl.AddLine(c + dir * hubR, c + dir * innerR, spoke, 2f);
        }

        dl.AddCircleFilled(c, hubR, ImGui.GetColorU32(Silver with { W = 0.10f }), 40);
        dl.AddCircle(c, hubR, silver, 40, 2f);
        dl.AddCircle(c, hubR * 0.6f, silver, 28, 1.5f);
        dl.AddCircleFilled(c, hubR * 0.18f, silver, 16);
    }

    private static Vector4 CycleColour(float t)
    {
        var len = NeonPalette.Length;
        var pos = CardEffectHelpers.Frac(t / (len * 1.3f)) * len;
        var i0 = (int)pos;
        return Vector4.Lerp(NeonPalette[i0], NeonPalette[(i0 + 1) % len], pos - i0);
    }

    private static bool[] BuildRedTable()
    {
        var red = new bool[37];
        foreach (var num in new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 })
            red[num] = true;
        return red;
    }
}
