using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace GambaWhere.Utility;

public static class PresetCodec
{
    public static string Encode(Dictionary<string, object> ruleValues, string description)
    {
        var payload = JsonSerializer.Serialize(new { r = ruleValues, d = description });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string key, out Dictionary<string, object> ruleValues, out string description)
    {
        ruleValues = new();
        description = string.Empty;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(key.Trim()));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            description = root.TryGetProperty("d", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            if (root.TryGetProperty("r", out var r) && r.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in r.EnumerateObject())
                    ruleValues[prop.Name] = prop.Value.Clone();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
