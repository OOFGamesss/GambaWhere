using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GambaWhere.Utility;

public static class PresetReader
{
    public static int Int(Dictionary<string, object> dict, string key, int fallback)
    {
        if (!dict.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Number => el.GetInt32(),
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => fallback
        };
    }

    public static float Float(Dictionary<string, object> dict, string key, float fallback)
    {
        if (!dict.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Number => el.GetSingle(),
            float f => f,
            double d => (float)d,
            int i => (float)i,
            _ => fallback
        };
    }

    public static bool Bool(Dictionary<string, object> dict, string key, bool fallback)
    {
        if (!dict.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.True => true,
            JsonElement el when el.ValueKind == JsonValueKind.False => false,
            bool b => b,
            _ => fallback
        };
    }
}
