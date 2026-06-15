using System;
using System.Collections.Generic;

namespace GambaWhere.Utility;

/// <summary>Strips empty or default rule values before sending to the API.</summary>
public static class ManualRulesApiOmitter
{
    public static Dictionary<string, object> OmitEmptyOrDefault(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kv in source)
        {
            if (ShouldSend(kv.Value))
                result[kv.Key] = kv.Value;
        }

        return result;
    }

    private static bool ShouldSend(object? value)
    {
        if (value is null)
            return false;

        return value switch
        {
            string s => !string.IsNullOrWhiteSpace(s),
            bool b => b,
            byte n => n != 0,
            sbyte n => n != 0,
            short n => n != 0,
            ushort n => n != 0,
            int n => n != 0,
            uint n => n != 0,
            long n => n != 0,
            ulong n => n != 0,
            float f => f != 0f,
            double d => d != 0d,
            decimal m => m != 0m,
            _ => true
        };
    }
}
