using System;

namespace GambaWhere.Utility;

/// <summary>Clamps rule values to their valid ranges.</summary>
public static class RuleClamp
{
    public static int Min(int value, int min) => Math.Max(value, min);
    public static int Range(int value, int min, int max) => Math.Clamp(value, min, max);
    public static float MinF(float value, float min) => MathF.Max(value, min);
}
