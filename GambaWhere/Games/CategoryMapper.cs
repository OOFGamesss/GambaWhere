using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using GambaWhere.Models;
using Newtonsoft.Json.Linq;

namespace GambaWhere.Games;

public static class CategoryMapper
{
    public static GameCategory ToCategory(GameTypeDto dto)
    {
        return new GameCategory(
            dto.Name,
            ParseColour(dto.Background, new Vector4(0.50f, 0.50f, 0.50f, 0.12f)),
            ParseColour(dto.Accent, new Vector4(0.75f, 0.75f, 0.75f, 1f)),
            dto.DiscordColour,
            string.IsNullOrWhiteSpace(dto.Emoji) ? "🎲" : dto.Emoji,
            string.IsNullOrWhiteSpace(dto.DiscordEmoji) ? dto.Emoji : dto.DiscordEmoji,
            dto.BannerUrl ?? string.Empty,
            MapFields(dto.ManualFields),
            dto.EmptyRulesMessage);
    }

    public static IReadOnlyList<GameCategory> ToCategories(IEnumerable<GameTypeDto> dtos) =>
        dtos.Select(ToCategory).ToList();

    public static void NormalizeDefaults(IEnumerable<GameTypeDto> dtos)
    {
        foreach (var dto in dtos)
        {
            if (dto.ManualFields == null)
                continue;

            foreach (var field in dto.ManualFields)
            {
                var kind = Enum.TryParse<RuleKind>(field.Kind, ignoreCase: true, out var parsed)
                    ? parsed
                    : RuleKind.Text;
                field.Default = CoerceDefault(field.Default, kind);
            }
        }
    }

    private static IReadOnlyList<RuleField>? MapFields(List<ManualRuleFieldDto>? fields)
    {
        if (fields == null || fields.Count == 0)
            return null;

        return fields.Select(MapField).ToArray();
    }

    private static RuleField MapField(ManualRuleFieldDto dto)
    {
        var kind = Enum.TryParse<RuleKind>(dto.Kind, ignoreCase: true, out var parsed)
            ? parsed
            : RuleKind.Text;

        return new RuleField(
            dto.Name,
            kind,
            string.IsNullOrWhiteSpace(dto.Label) ? dto.Name : dto.Label,
            CoerceDefault(dto.Default, kind),
            dto.Min,
            dto.Max,
            dto.Options,
            dto.TextMax ?? 64);
    }

    private static object CoerceDefault(object? value, RuleKind kind)
    {
        if (value is null)
            return KindFallback(kind);

        if (value is JsonElement element)
            return CoerceJsonElement(element, kind);

        if (value is JToken token)
            return CoerceJToken(token, kind);

        return kind switch
        {
            RuleKind.Toggle => value switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var parsed) && parsed,
                byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value) != 0,
                float or double or decimal => Convert.ToDouble(value) != 0d,
                _ => false
            },
            RuleKind.Money or RuleKind.Int or RuleKind.ItemSearch => value switch
            {
                byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value),
                float or double or decimal => Convert.ToInt64(value),
                string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
                _ => 0L
            },
            RuleKind.Float => value switch
            {
                float f => f,
                double d => (float)d,
                decimal m => (float)m,
                byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToSingle(value),
                string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) => f,
                _ => 0f
            },
            _ => value switch
            {
                string s => s,
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => string.Empty
            }
        };
    }

    private static object CoerceJToken(JToken token, RuleKind kind)
    {
        if (token.Type is JTokenType.Null or JTokenType.Undefined)
            return KindFallback(kind);

        if (token is JValue value)
            return CoerceDefault(value.Value, kind);

        return KindFallback(kind);
    }

    private static object KindFallback(RuleKind kind) => kind switch
    {
        RuleKind.Toggle => false,
        RuleKind.Money or RuleKind.Int or RuleKind.ItemSearch => 0L,
        RuleKind.Float => 0f,
        _ => string.Empty
    };

    private static object CoerceJsonElement(JsonElement element, RuleKind kind)
    {
        return kind switch
        {
            RuleKind.Toggle => element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(element.GetString(), out var b) && b,
                JsonValueKind.Number => element.TryGetInt64(out var n) && n != 0,
                _ => false
            },
            RuleKind.Money or RuleKind.Int or RuleKind.ItemSearch => element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetInt64(out var n) ? n : 0L,
                JsonValueKind.String => long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0L,
                _ => 0L
            },
            RuleKind.Float => element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetSingle(out var f) ? f : 0f,
                JsonValueKind.String => float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
                _ => 0f
            },
            _ => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty
            }
        };
    }

    private static Vector4 ParseColour(string? raw, Vector4 fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return fallback;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            return fallback;

        return new Vector4(r, g, b, a);
    }
}
