using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.CardEffects;

/// <summary>Booster Hearts Card Effect showing floating and beating hearts.</summary>
public static class HeartsCardEffect
{
    private static readonly Vector4 Crimson = new(0.70f, 0.09f, 0.14f, 1f);
    private static readonly Vector4 Rose = new(0.95f, 0.36f, 0.52f, 1f);
    private static readonly Vector4 DeepGlow = new(0.48f, 0.03f, 0.07f, 1f);

    public static void DrawHeartsFill(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        dl.PushClipRect(min, max, true);
        DrawRedVignette(dl, min, max);
        DrawSpeckle(dl, min, max, time, seed ^ 0x51EDu, 46);
        DrawFloatingHearts(dl, min, max, time, seed == 0 ? 1u : seed, 12);
        dl.PopClipRect();
    }

    public static void DrawHeartsFoil(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        dl.PushClipRect(min, max, true);
        DrawRedVignette(dl, min, max);
        DrawFloatingHearts(dl, min, max, time, 0x9E37u, 6);
        dl.PopClipRect();
    }

    public static void DrawHeartsBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, double time)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        DrawRing(dl, min, max, rounding, t, scale);
    }

    private static void DrawFloatingHearts(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var rng = seed;

        for (var i = 0; i < count; i++)
        {
            var baseX = CardEffectHelpers.NextFloat(ref rng);
            var startY = CardEffectHelpers.NextFloat(ref rng);
            var size = (7f + CardEffectHelpers.NextFloat(ref rng) * 11f) * scale;
            var speed = 0.045f + CardEffectHelpers.NextFloat(ref rng) * 0.075f;
            var swayAmp = 0.02f + CardEffectHelpers.NextFloat(ref rng) * 0.05f;
            var swayFreq = 0.8f + CardEffectHelpers.NextFloat(ref rng) * 1.6f;
            var phase = CardEffectHelpers.NextFloat(ref rng) * 6.283f;
            var period = 0.85f + CardEffectHelpers.NextFloat(ref rng) * 0.7f;
            var pink = CardEffectHelpers.NextFloat(ref rng) < 0.28f;
            var tilt = (CardEffectHelpers.NextFloat(ref rng) - 0.5f) * 0.5f;

            var yPos = CardEffectHelpers.Frac(startY - t * speed);
            var env = MathF.Min(1f, yPos / 0.16f) * MathF.Min(1f, (1f - yPos) / 0.16f);
            if (env <= 0f)
                continue;

            var x = min.X + (baseX + swayAmp * MathF.Sin(t * swayFreq + phase)) * w;
            var y = min.Y + yPos * h;
            var beat = 1f + 0.24f * Heartbeat(t / period + phase);
            var colour = pink ? Rose : Crimson;
            DrawGlowingHeart(dl, new Vector2(x, y), size * beat, tilt + 0.12f * MathF.Sin(t * swayFreq + phase), colour, 0.85f * env);
        }
    }

    private static void DrawGlowingHeart(ImDrawListPtr dl, Vector2 c, float size, float angle, Vector4 colour, float alpha)
    {
        DrawHeart(dl, c, size * 1.7f, angle, DeepGlow with { W = 0.14f * alpha });
        DrawHeart(dl, c, size * 1.28f, angle, colour with { W = 0.28f * alpha });
        DrawHeart(dl, c, size, angle, colour with { W = 0.92f * alpha });
        DrawHeart(dl, c, size * 0.42f, angle, new Vector4(1f, 0.82f, 0.86f, 0.55f * alpha));
    }

    private static void DrawHeart(ImDrawListPtr dl, Vector2 c, float size, float angle, Vector4 colour)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        Vector2 T(float x, float y) => c + new Vector2(x * size * cos - y * size * sin, x * size * sin + y * size * cos);

        var col = ImGui.GetColorU32(colour);
        dl.AddCircleFilled(T(-0.5f, -0.25f), size * 0.5f, col, 16);
        dl.AddCircleFilled(T(0.5f, -0.25f), size * 0.5f, col, 16);
        dl.AddTriangleFilled(T(-1f, -0.22f), T(1f, -0.22f), T(0f, 1.05f), col);
    }

    private static float Heartbeat(float phase)
    {
        phase = CardEffectHelpers.Frac(phase);
        var lub = MathF.Exp(-MathF.Pow(phase / 0.055f, 2f));
        var dub = 0.55f * MathF.Exp(-MathF.Pow((phase - 0.17f) / 0.05f, 2f));
        return lub + dub;
    }

    private static void DrawRedVignette(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var band = (max.Y - min.Y) * 0.28f;
        var edge = ImGui.GetColorU32(new Vector4(0.40f, 0.03f, 0.06f, 0.20f));
        var clear = ImGui.GetColorU32(new Vector4(0.40f, 0.03f, 0.06f, 0f));
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + band), edge, edge, clear, clear);
        dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - band), max, clear, clear, edge, edge);
    }

    private static void DrawSpeckle(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var rng = seed;

        for (var i = 0; i < count; i++)
        {
            var bx = CardEffectHelpers.NextFloat(ref rng);
            var by = CardEffectHelpers.NextFloat(ref rng);
            var speed = 0.02f + CardEffectHelpers.NextFloat(ref rng) * 0.05f;
            var twk = CardEffectHelpers.NextFloat(ref rng) * 6.283f;
            var rad = (0.6f + CardEffectHelpers.NextFloat(ref rng) * 1.3f) * scale;

            var y = min.Y + CardEffectHelpers.Frac(by - t * speed) * h;
            var alpha = (0.20f + 0.35f * MathF.Abs(MathF.Sin(t * 2.4f + twk))) * 0.6f;
            dl.AddCircleFilled(new Vector2(min.X + bx * w, y), rad,
                ImGui.GetColorU32(new Vector4(0.72f, 0.12f, 0.18f, alpha)), 6);
        }
    }

    private static void DrawRing(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float t, float scale)
    {
        var pulse = 0.82f + 0.18f * MathF.Sin(t * 2.2f);
        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.45f, 0.03f, 0.07f, 0.24f * pulse)), rounding, ImDrawFlags.RoundCornersAll, 8.5f * scale);
        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.70f, 0.10f, 0.15f, 0.58f * pulse)), rounding, ImDrawFlags.RoundCornersAll, 3.5f * scale);
        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.96f, 0.40f, 0.52f, 0.85f)), rounding, ImDrawFlags.RoundCornersAll, 1.4f * scale);
    }
}
