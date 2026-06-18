using System;
using System.Collections.Generic;
using GambaWhere.Utility;
using Newtonsoft.Json.Linq;

namespace GambaWhere.IPC;

/// <summary>Validates and sanitises a partner's raw IPC v2 rules payload into a safe label/value map, rejecting anything malformed, oversized, wrongly typed or containing disallowed content.</summary>
internal static class AutomaticRulesValidator
{
    private const int MaxRules = 10;
    private const int MaxLabelLength = 48;
    private const int MaxStringValueLength = 64;

    public static Dictionary<string, object>? Validate(object? rawPayload)
    {
        var array = AutomaticRuleReader.ToJObject(rawPayload)?.GetValue("Rules", StringComparison.OrdinalIgnoreCase) as JArray;
        if (array == null || array.Count == 0 || array.Count > MaxRules)
            return null;

        var result = new Dictionary<string, object>();
        foreach (var token in array)
        {
            if (token is not JObject entry)
                continue;

            var label = CleanLabel(entry.GetValue("Label", StringComparison.OrdinalIgnoreCase));
            if (label == null || result.ContainsKey(label))
                continue;

            if (TryReadValue(entry.GetValue("Value", StringComparison.OrdinalIgnoreCase), out var value))
                result[label] = value;
        }

        return result.Count > 0 ? result : null;
    }

    private static string? CleanLabel(JToken? token)
    {
        if (token is not { Type: JTokenType.String })
            return null;

        var label = (token.Value<string>() ?? string.Empty).Trim();
        if (label.Length == 0 || label.Length > MaxLabelLength || UserTextGuard.ContainsDisallowedContent(label))
            return null;

        return label;
    }

    private static bool TryReadValue(JToken? token, out object value)
    {
        value = string.Empty;
        if (token == null)
            return false;

        try
        {
            switch (token.Type)
            {
                case JTokenType.String:
                    var s = token.Value<string>() ?? string.Empty;
                    if (s.Length > MaxStringValueLength || UserTextGuard.ContainsDisallowedContent(s))
                        return false;
                    value = s;
                    return true;
                case JTokenType.Boolean:
                    value = token.Value<bool>();
                    return true;
                case JTokenType.Integer:
                    value = token.Value<long>();
                    return true;
                case JTokenType.Float:
                    value = token.Value<double>();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}
