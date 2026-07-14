using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using GambaWhere.UI.Components;

namespace GambaWhere.Games;

/// <summary>A game's editable rule configuration.</summary>
public interface IRuleConfig
{
    string GameType { get; }

    void Draw();

    Dictionary<string, object> ToApiPayload();

    void LoadFromPreset(Dictionary<string, object> values);

    Dictionary<string, object> SaveToPreset();
}

public sealed class DataRuleConfig : IRuleConfig
{
    private readonly string _categoryKey;
    private readonly IReadOnlyList<RuleField> _fields;
    private readonly string? _emptyMessage;
    private readonly Dictionary<string, object> _values = new();

    public DataRuleConfig(string categoryKey, IReadOnlyList<RuleField> manualFields, string? emptyMessage)
    {
        _categoryKey = categoryKey;
        _fields = manualFields;
        _emptyMessage = emptyMessage;

        foreach (var field in _fields)
            _values[field.Name] = InitialValue(field);
    }

    public string GameType => _categoryKey;

    public void Draw()
    {
        if (_fields.Count == 0)
        {
            ImGui.TextDisabled(_emptyMessage ?? "This game has no configurable rules.");
            return;
        }

        using var grid = RuleGrid.Begin($"##{_categoryKey}_rules");
        foreach (var field in _fields)
        {
            grid.Cell();
            DrawField(field);
        }
    }

    private void DrawField(RuleField field)
    {
        var id = $"##{field.Name}";
        switch (field.Kind)
        {
            case RuleKind.Money:
            {
                var v = (int)_values[field.Name];
                HostField.Money(field.Label, id, ref v);
                _values[field.Name] = ClampInt(field, v);
                break;
            }
            case RuleKind.Int:
            {
                var v = (int)_values[field.Name];
                HostField.Int(field.Label, id, ref v);
                _values[field.Name] = ClampInt(field, v);
                break;
            }
            case RuleKind.Float:
            {
                var v = (float)_values[field.Name];
                HostField.Float(field.Label, id, ref v);
                _values[field.Name] = field.Min.HasValue ? ClampToMinF(v, (float)field.Min.Value) : v;
                break;
            }
            case RuleKind.Toggle:
            {
                var v = (bool)_values[field.Name];
                HostField.Toggle(field.Label, id, ref v);
                _values[field.Name] = v;
                break;
            }
            case RuleKind.Text:
            {
                var v = (string)_values[field.Name];
                HostField.Text(field.Label, id, ref v, field.TextMax);
                _values[field.Name] = v;
                break;
            }
            case RuleKind.Combo:
            {
                var current = (string)_values[field.Name];
                HostField.Combo(field.Label, id, current, () =>
                {
                    if (field.Options == null)
                        return;
                    foreach (var option in field.Options)
                        if (ImGui.Selectable(option, current == option))
                            _values[field.Name] = option;
                });
                break;
            }
            case RuleKind.ItemSearch:
            {
                HostField.Label(field.Label);
                ImGui.SetNextItemWidth(-1);
                var v = (uint)_values[field.Name];
                ItemSearchCombo.Draw(id, ref v);
                _values[field.Name] = v;
                break;
            }
        }
    }

    public Dictionary<string, object> ToApiPayload()
    {
        var payload = new Dictionary<string, object>();
        foreach (var field in _fields)
        {
            if (field.Kind == RuleKind.ItemSearch)
            {
                var name = ItemSearchCombo.GetItemName((uint)_values[field.Name]);
                if (name != null)
                    payload[field.Name] = name;
                continue;
            }

            if (field.Kind == RuleKind.Text)
            {
                var raw = (string)_values[field.Name];
                payload[field.Name] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
                continue;
            }

            payload[field.Name] = _values[field.Name];
        }

        return payload;
    }

    public Dictionary<string, object> SaveToPreset()
    {
        var preset = new Dictionary<string, object>();
        foreach (var field in _fields)
        {
            if (field.Kind == RuleKind.ItemSearch)
                preset[$"{field.Name}ItemId"] = (int)(uint)_values[field.Name];
            else
                preset[field.Name] = _values[field.Name];
        }

        return preset;
    }

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        foreach (var field in _fields)
        {
            switch (field.Kind)
            {
                case RuleKind.Money:
                case RuleKind.Int:
                    _values[field.Name] = ClampInt(field, PresetInt(values, field.Name, (int)_values[field.Name]));
                    break;
                case RuleKind.Float:
                    var f = PresetFloat(values, field.Name, (float)_values[field.Name]);
                    _values[field.Name] = field.Min.HasValue ? ClampToMinF(f, (float)field.Min.Value) : f;
                    break;
                case RuleKind.Toggle:
                    _values[field.Name] = PresetBool(values, field.Name, (bool)_values[field.Name]);
                    break;
                case RuleKind.Text:
                case RuleKind.Combo:
                    _values[field.Name] = PresetString(values, field.Name, (string)_values[field.Name]);
                    break;
                case RuleKind.ItemSearch:
                    _values[field.Name] = (uint)PresetInt(values, $"{field.Name}ItemId", (int)(uint)_values[field.Name]);
                    break;
            }
        }
    }

    private static object InitialValue(RuleField field) => field.Kind switch
    {
        RuleKind.Money or RuleKind.Int => ToInt32(field.Default),
        RuleKind.Float => ToSingle(field.Default),
        RuleKind.Toggle => ToBoolean(field.Default),
        RuleKind.Text or RuleKind.Combo => field.Default as string
            ?? field.Default?.ToString()
            ?? string.Empty,
        RuleKind.ItemSearch => (uint)ToInt32(field.Default),
        _ => field.Default ?? 0
    };

    private static int ToInt32(object? value) => value switch
    {
        null => 0,
        int i => i,
        long l => (int)l,
        uint u => (int)u,
        float f => (int)f,
        double d => (int)d,
        decimal m => (int)m,
        string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
        JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) => n,
        JsonElement el when el.ValueKind == JsonValueKind.String
            && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
        _ => 0
    };

    private static float ToSingle(object? value) => value switch
    {
        null => 0f,
        float f => f,
        double d => (float)d,
        decimal m => (float)m,
        int i => i,
        long l => l,
        string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) => f,
        JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetSingle(out var f) => f,
        _ => 0f
    };

    private static bool ToBoolean(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        string s => bool.TryParse(s, out var b) && b,
        JsonElement el when el.ValueKind == JsonValueKind.True => true,
        JsonElement el when el.ValueKind == JsonValueKind.False => false,
        _ => false
    };

    private static int ClampInt(RuleField field, int value)
    {
        if (field.Min.HasValue && field.Max.HasValue)
            return ClampToRange(value, (int)field.Min.Value, (int)field.Max.Value);
        return field.Min.HasValue ? ClampToMin(value, (int)field.Min.Value) : value;
    }

    private static int ClampToRange(int value, int min, int max) => Math.Clamp(value, min, max);

    private static int ClampToMin(int value, int min) => Math.Max(value, min);

    private static float ClampToMinF(float value, float min) => MathF.Max(value, min);

    private static int PresetInt(Dictionary<string, object> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out var raw))
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

    private static float PresetFloat(Dictionary<string, object> values, string key, float fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Number => el.GetSingle(),
            float f => f,
            double d => (float)d,
            int i => i,
            _ => fallback
        };
    }

    private static bool PresetBool(Dictionary<string, object> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.True => true,
            JsonElement el when el.ValueKind == JsonValueKind.False => false,
            bool b => b,
            _ => fallback
        };
    }

    private static string PresetString(Dictionary<string, object> values, string key, string fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        return raw switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString() ?? fallback,
            string s => s,
            _ => fallback
        };
    }
}
