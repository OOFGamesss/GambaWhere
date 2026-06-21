using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.CardEffects;

/// <summary>Holographic card effect for Discord server boosters.</summary>
public static class BoosterCardEffect
{
    public static readonly Vector4 BaseColour = new(0.07f, 0.06f, 0.12f, 1f);

    public static void DrawHolographicFill(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        dl.PushClipRect(min, max, true);
        DrawFoilStripes(dl, min, max, time);
        DrawGlareStreak(dl, min, max, time);
        DrawSparkles(dl, min, max, time, seed);
        dl.PopClipRect();
    }

    public static void DrawHolographicFoil(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        dl.PushClipRect(min, max, true);
        DrawFoilStripes(dl, min, max, time);
        dl.PopClipRect();
    }

    private static void DrawFoilStripes(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;

        var step = 7f * scale;
        var thickness = step + 1.5f;
        var period = 180f * scale;
        for (var o = -h; o <= w; o += step)
        {
            var hue = CardEffectHelpers.Frac(o / period - t * 0.08f);
            var col = ImGui.GetColorU32(CardEffectHelpers.Hsv(hue, 0.42f, 1f, 0.11f));
            dl.AddLine(new Vector2(min.X + o, min.Y), new Vector2(min.X + o + h, min.Y + h), col, thickness);
        }
    }

    private static void DrawGlareStreak(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;

        var sweep = CardEffectHelpers.Frac(t * 0.10f);
        var centre = -h + sweep * (w + 2f * h);
        for (var g = -3; g <= 3; g++)
        {
            var o = centre + g * 4f * scale;
            var a = (1f - MathF.Abs(g) / 4f) * 0.16f;
            dl.AddLine(new Vector2(min.X + o, min.Y), new Vector2(min.X + o + h, min.Y + h),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, a)), 4f * scale);
        }
    }

    private static void DrawSparkles(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;

        var rng = seed == 0 ? 1u : seed;
        const int count = 10;
        for (var i = 0; i < count; i++)
        {
            var fx = CardEffectHelpers.NextFloat(ref rng);
            var fy = CardEffectHelpers.NextFloat(ref rng);
            var phase = CardEffectHelpers.NextFloat(ref rng) * MathF.PI * 2f;

            var twinkle = MathF.Max(0f, MathF.Sin(t * 1.6f + phase));
            var alpha = twinkle * twinkle * 0.9f;
            if (alpha < 0.02f)
                continue;

            var c = new Vector2(min.X + fx * w, min.Y + fy * h);
            var radius = (1.5f + twinkle * 2.5f) * scale;
            DrawSparkle(dl, c, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        }
    }

    public static void DrawHolographicBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, double time)
    {
        var thickness = 2.5f * ImGuiHelpers.GlobalScale;
        var pts = CardEffectHelpers.BuildRoundedRectPerimeter(min, max, rounding, 6);
        var n = pts.Count;
        if (n < 2)
            return;

        var t = (float)time;

        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var hue = CardEffectHelpers.Frac(i / (float)n - t * 0.15f);
            dl.AddLine(a, b, ImGui.GetColorU32(CardEffectHelpers.Hsv(hue, 0.65f, 1f, 0.95f)), thickness);
        }

        var tail = Math.Max(2, n / 6);
        var head = (int)(CardEffectHelpers.Frac(t * 0.12f) * n);
        for (var k = 0; k < tail; k++)
        {
            var idx = (head + k) % n;
            var fade = 1f - k / (float)tail;
            dl.AddLine(pts[idx], pts[(idx + 1) % n],
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f * fade)), thickness);
        }
    }

    private static void DrawSparkle(ImDrawListPtr dl, Vector2 c, float r, uint col)
    {
        var th = MathF.Max(1f, r * 0.35f);
        dl.AddLine(new Vector2(c.X - r, c.Y), new Vector2(c.X + r, c.Y), col, th);
        dl.AddLine(new Vector2(c.X, c.Y - r), new Vector2(c.X, c.Y + r), col, th);
    }
}
