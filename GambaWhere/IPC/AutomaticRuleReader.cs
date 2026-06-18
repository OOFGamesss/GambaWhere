using System;
using System.Collections.Generic;
using System.Text.Json;
using GambaWhere.Games;
using Newtonsoft.Json.Linq;

namespace GambaWhere.IPC;

/// <summary>Parses a partner plugin's raw IPC return into a rule payload using a declarative field schema.</summary>
internal static class AutomaticRuleReader
{
    public static JObject? ToJObject(object? raw)
    {
        try
        {
            switch (raw)
            {
                case null:
                    return null;
                case JObject jo:
                    return jo;
                case string s when !string.IsNullOrWhiteSpace(s):
                    return JObject.Parse(s);
                case JsonElement je:
                    return JObject.Parse(je.GetRawText());
                case JToken token:
                    return token as JObject;
                default:
                    return JObject.FromObject(raw);
            }
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, object>? Map(JObject? data, IReadOnlyList<RuleField> fields)
    {
        if (data == null)
            return null;

        var result = new Dictionary<string, object>();
        foreach (var field in fields)
        {
            var token = data.GetValue(field.SourceKey, StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type == JTokenType.Null)
                continue;

            var value = Coerce(token, field);
            if (field.SkipIfZero && IsZero(value))
                continue;

            result[field.Name] = value;
        }

        return result.Count > 0 ? result : null;
    }

    private static object Coerce(JToken token, RuleField field)
    {
        switch (field.ResolvedAutoType)
        {
            case RuleValueType.String:
                var s = token.Type == JTokenType.String ? token.Value<string>() ?? string.Empty : token.ToString();
                return field.SpacesFromUnderscores ? s.Replace('_', ' ') : s;
            case RuleValueType.Bool:
                return token.Type == JTokenType.Boolean
                    ? token.Value<bool>()
                    : bool.TryParse(token.ToString(), out var b) && b;
            default:
                var d = ToDouble(token) * field.Multiplier;
                return field.ResolvedAutoType switch
                {
                    RuleValueType.Int => ClampInt(d),
                    RuleValueType.Long => ClampLong(d),
                    _ => (float)d
                };
        }
    }

    private static double ToDouble(JToken token) => token.Type switch
    {
        JTokenType.Integer or JTokenType.Float => token.Value<double>(),
        _ => double.TryParse(token.ToString(), out var d) ? d : 0d
    };

    private static int ClampInt(double d) =>
        d >= int.MaxValue ? int.MaxValue : d <= int.MinValue ? int.MinValue : (int)d;

    private static long ClampLong(double d) =>
        d >= long.MaxValue ? long.MaxValue : d <= long.MinValue ? long.MinValue : (long)d;

    private static bool IsZero(object value) => value switch
    {
        int i => i == 0,
        long l => l == 0,
        float f => f == 0f,
        double dd => dd == 0d,
        _ => false
    };
}
