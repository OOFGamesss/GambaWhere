using System;
using System.Collections.Generic;
using System.Numerics;

namespace GambaWhere.UI.CardEffects;

/// <summary>Shared math and geometry helpers used by card effect renderers.</summary>
internal static class CardEffectHelpers
{
    public static uint Seed(string key)
    {
        uint hash = 2166136261u;
        foreach (var ch in key)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return hash;
    }

    public static List<Vector2> BuildRoundedRectPerimeter(Vector2 min, Vector2 max, float r, int cornerSegments)
    {
        r = MathF.Max(0f, MathF.Min(r, MathF.Min((max.X - min.X) * 0.5f, (max.Y - min.Y) * 0.5f)));

        var pts = new List<Vector2>((cornerSegments + 1) * 4);
        var tl = new Vector2(min.X + r, min.Y + r);
        var tr = new Vector2(max.X - r, min.Y + r);
        var br = new Vector2(max.X - r, max.Y - r);
        var bl = new Vector2(min.X + r, max.Y - r);

        void Arc(Vector2 c, float a0, float a1)
        {
            for (var s = 0; s <= cornerSegments; s++)
            {
                var a = a0 + (a1 - a0) * (s / (float)cornerSegments);
                pts.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
            }
        }

        Arc(tl, MathF.PI, MathF.PI * 1.5f);
        Arc(tr, MathF.PI * 1.5f, MathF.PI * 2f);
        Arc(br, 0f, MathF.PI * 0.5f);
        Arc(bl, MathF.PI * 0.5f, MathF.PI);
        return pts;
    }

    public static float NextFloat(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / (float)0x1000000;
    }

    public static float Frac(float x) => x - MathF.Floor(x);

    public static Vector4 Hsv(float h, float s, float v, float a)
    {
        h = Frac(h) * 6f;
        var i = (int)h;
        var f = h - i;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var w = v * (1f - (1f - f) * s);

        return (i % 6) switch
        {
            0 => new Vector4(v, w, p, a),
            1 => new Vector4(q, v, p, a),
            2 => new Vector4(p, v, w, a),
            3 => new Vector4(p, q, v, a),
            4 => new Vector4(w, p, v, a),
            _ => new Vector4(v, p, q, a),
        };
    }
}
