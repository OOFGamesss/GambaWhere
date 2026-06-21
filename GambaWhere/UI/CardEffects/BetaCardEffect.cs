using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.CardEffects;

/// <summary>Green lightning card effect for GW Beta users.</summary>
public static class BetaCardEffect
{
    public static readonly Vector4 BaseColour = new(0.04f, 0.08f, 0.05f, 1f);

    public static void DrawLightningFill(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        dl.PushClipRect(min, max, true);
        DrawElectricHaze(dl, min, max, time);
        DrawBetaWatermark(dl, min, max);
        DrawLightningStrikes(dl, min, max, time, seed);
        dl.PopClipRect();
    }

    public static void DrawLightningFoil(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        dl.PushClipRect(min, max, true);
        DrawElectricHaze(dl, min, max, time);
        DrawBetaWatermark(dl, min, max);
        dl.PopClipRect();
    }

    public static void DrawLightningBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, double time)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pts = BuildDensePerimeter(min, max, rounding, 6, 8f * scale);
        var n = pts.Count;
        if (n < 2)
            return;

        var t = (float)time;
        DrawBorderGlow(dl, pts, n, t, scale);
        DrawBorderSparks(dl, pts, n, t, scale);
    }

    // Builds a perimeter with arc points at corners and interpolated points along straight edges,
    // so tendrils and spark distribution is uniform around the entire card rather than corner-heavy.
    private static List<Vector2> BuildDensePerimeter(Vector2 min, Vector2 max, float r, int cornerSegs, float spacing)
    {
        r = MathF.Max(0f, MathF.Min(r, MathF.Min((max.X - min.X) * 0.5f, (max.Y - min.Y) * 0.5f)));
        var pts = new List<Vector2>();
        var tl = new Vector2(min.X + r, min.Y + r);
        var tr = new Vector2(max.X - r, min.Y + r);
        var br = new Vector2(max.X - r, max.Y - r);
        var bl = new Vector2(min.X + r, max.Y - r);

        void AddArc(Vector2 c, float a0, float a1)
        {
            for (var s = 0; s <= cornerSegs; s++)
            {
                var a = a0 + (a1 - a0) * (s / (float)cornerSegs);
                pts.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
            }
        }

        void AddEdge(Vector2 from, Vector2 to)
        {
            var steps = Math.Max(1, (int)((to - from).Length() / spacing));
            for (var s = 1; s < steps; s++)
                pts.Add(from + (to - from) * (s / (float)steps));
        }

        AddArc(tl, MathF.PI, MathF.PI * 1.5f);
        AddEdge(new Vector2(tl.X, min.Y), new Vector2(tr.X, min.Y));
        AddArc(tr, MathF.PI * 1.5f, MathF.PI * 2f);
        AddEdge(new Vector2(max.X, tr.Y), new Vector2(max.X, br.Y));
        AddArc(br, 0f, MathF.PI * 0.5f);
        AddEdge(new Vector2(br.X, max.Y), new Vector2(bl.X, max.Y));
        AddArc(bl, MathF.PI * 0.5f, MathF.PI);
        AddEdge(new Vector2(min.X, bl.Y), new Vector2(min.X, tl.Y));
        return pts;
    }

    private static void DrawElectricHaze(ImDrawListPtr dl, Vector2 min, Vector2 max, double time)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var h = max.Y - min.Y;
        var w = max.X - min.X;
        var step = 14f * scale;
        var period = 220f * scale;

        for (var o = -h; o <= w; o += step)
        {
            var wave = CardEffectHelpers.Frac(o / period - t * 0.045f);
            var green = 0.40f + wave * 0.18f;
            var alpha = 0.028f + wave * 0.036f;
            dl.AddLine(new Vector2(min.X + o, min.Y), new Vector2(min.X + o + h, min.Y + h),
                ImGui.GetColorU32(new Vector4(0f, green, 0.10f, alpha)), step + 1.5f);
        }
    }

    private static void DrawBetaWatermark(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var fontSize = ImGui.GetFontSize() * 0.68f;
        var pos = new Vector2(min.X + 5f * scale, max.Y - fontSize - 5f * scale);
        var glowCol = ImGui.GetColorU32(new Vector4(0f, 0.52f, 0.18f, 0.11f));
        var font = ImGui.GetFont();
        for (var ox = -1; ox <= 1; ox++)
        for (var oy = -1; oy <= 1; oy++)
        {
            if (ox == 0 && oy == 0) continue;
            dl.AddText(font, fontSize, pos + new Vector2(ox, oy) * scale, glowCol, "BETA");
        }
        dl.AddText(font, fontSize, pos, ImGui.GetColorU32(new Vector4(0.22f, 1f, 0.48f, 0.28f)), "BETA");
    }

    private static void DrawLightningStrikes(ImDrawListPtr dl, Vector2 min, Vector2 max, double time, uint seed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var t = (float)time;
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var rng = seed == 0 ? 1u : seed;
        const int boltCount = 5;

        for (var i = 0; i < boltCount; i++)
        {
            var startX = CardEffectHelpers.NextFloat(ref rng);
            var endX = CardEffectHelpers.NextFloat(ref rng);
            var endY = 0.35f + CardEffectHelpers.NextFloat(ref rng) * 0.55f;
            var period = 2.8f + CardEffectHelpers.NextFloat(ref rng) * 2.2f;
            var phaseOff = CardEffectHelpers.NextFloat(ref rng);
            var boltSeed = rng ^ ((uint)(i + 1) * 2654435761u);

            var cyclePos = CardEffectHelpers.Frac(t / period + phaseOff);
            const float flashWindow = 0.060f;
            if (cyclePos >= flashWindow)
                continue;

            var alpha = (1f - cyclePos / flashWindow) * 0.95f;
            var start = new Vector2(min.X + startX * w, min.Y + 1f * scale);
            var end = new Vector2(min.X + endX * w, min.Y + endY * h);
            DrawBranchingBolt(dl, start, end, alpha, scale, boltSeed);
        }
    }

    private static void DrawBranchingBolt(ImDrawListPtr dl, Vector2 start, Vector2 end, float alpha, float scale, uint seed)
    {
        var mainPts = GenerateBoltPath(start, end, 9, 0.27f, seed);
        DrawBoltGlowLayers(dl, mainPts, alpha, scale);

        var brSeed = seed ^ 0xAB7C4F1Du;
        var brLocal = brSeed;
        var branchFrac = 0.36f + CardEffectHelpers.NextFloat(ref brLocal) * 0.24f;
        var branchIdx = Math.Clamp((int)((mainPts.Length - 1) * branchFrac), 1, mainPts.Length - 2);

        var mainDir = Vector2.Normalize(end - start);
        var angle = (CardEffectHelpers.NextFloat(ref brLocal) - 0.5f) * 1.3f;
        var cs = MathF.Cos(angle);
        var sn = MathF.Sin(angle);
        var branchDir = new Vector2(mainDir.X * cs - mainDir.Y * sn, mainDir.X * sn + mainDir.Y * cs);
        var branchLen = (end - start).Length() * (0.32f + CardEffectHelpers.NextFloat(ref brLocal) * 0.28f);
        var branchPts = GenerateBoltPath(mainPts[branchIdx], mainPts[branchIdx] + branchDir * branchLen, 5, 0.38f, brSeed ^ 0x9E3779B9u);
        DrawBoltGlowLayers(dl, branchPts, alpha * 0.52f, scale);
    }

    private static void DrawBoltGlowLayers(ImDrawListPtr dl, Vector2[] pts, float alpha, float scale)
    {
        DrawBoltPath(dl, pts, new Vector4(0f, 0.28f, 0.08f, alpha * 0.13f), 8.5f * scale);
        DrawBoltPath(dl, pts, new Vector4(0f, 0.55f, 0.20f, alpha * 0.30f), 4.5f * scale);
        DrawBoltPath(dl, pts, new Vector4(0.18f, 0.90f, 0.42f, alpha * 0.60f), 2.2f * scale);
        DrawBoltPath(dl, pts, new Vector4(0.88f, 1f, 0.93f, alpha), 1.1f * scale);
    }

    private static void DrawBoltPath(ImDrawListPtr dl, Vector2[] pts, Vector4 colour, float thickness)
    {
        var col = ImGui.GetColorU32(colour);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i], pts[i + 1], col, thickness);
    }

    private static Vector2[] GenerateBoltPath(Vector2 start, Vector2 end, int segs, float spread, uint seed)
    {
        var pts = new Vector2[segs + 1];
        pts[0] = start;
        pts[segs] = end;
        var dir = end - start;
        var len = dir.Length();
        if (len < 0.5f)
            return pts;
        var perp = new Vector2(-dir.Y / len, dir.X / len);
        var rng = seed;
        for (var s = 1; s < segs; s++)
        {
            var frac = s / (float)segs;
            var offset = (CardEffectHelpers.NextFloat(ref rng) - 0.5f) * len * spread;
            pts[s] = start + dir * frac + perp * offset;
        }
        return pts;
    }

    private static void DrawBorderGlow(ImDrawListPtr dl, List<Vector2> pts, int n, float t, float scale)
    {
        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var flicker = 0.70f + 0.30f * MathF.Abs(MathF.Sin(t * 8.4f + i * 0.28f));
            dl.AddLine(a, b, ImGui.GetColorU32(new Vector4(0f, 0.28f * flicker, 0.08f, 0.28f)), 8.5f * scale);
            dl.AddLine(a, b, ImGui.GetColorU32(new Vector4(0f, 0.60f * flicker, 0.20f, 0.58f)), 3.5f * scale);
            dl.AddLine(a, b, ImGui.GetColorU32(new Vector4(0.10f * flicker, 0.85f * flicker, 0.36f, 0.92f)), 1.5f * scale);
        }
    }

    // Three sparks travelling the perimeter at different speeds and starting positions.
    private static void DrawBorderSparks(ImDrawListPtr dl, List<Vector2> pts, int n, float t, float scale)
    {
        var tail = Math.Max(4, n / 8);
        for (var s = 0; s < 3; s++)
        {
            var head = (int)(CardEffectHelpers.Frac(t * (0.26f + s * 0.09f) + s / 3f) * n);
            for (var k = 0; k < tail; k++)
            {
                var idx = (head + k) % n;
                var fade = 1f - k / (float)tail;
                dl.AddLine(pts[idx], pts[(idx + 1) % n],
                    ImGui.GetColorU32(new Vector4(0.62f, 1f, 0.76f, fade * fade * 0.90f)), 3.5f * scale);
            }
        }
    }

}
