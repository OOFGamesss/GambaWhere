using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using GambaWhere.Rules;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.Games;

/// <summary>Generic, data-driven rule editor built from a game's RuleField list: renders the manual UI, presets, and API payload.</summary>
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
                _values[field.Name] = field.Min.HasValue ? RuleClamp.MinF(v, (float)field.Min.Value) : v;
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
                    _values[field.Name] = ClampInt(field, PresetReader.Int(values, field.Name, (int)_values[field.Name]));
                    break;
                case RuleKind.Float:
                    var f = PresetReader.Float(values, field.Name, (float)_values[field.Name]);
                    _values[field.Name] = field.Min.HasValue ? RuleClamp.MinF(f, (float)field.Min.Value) : f;
                    break;
                case RuleKind.Toggle:
                    _values[field.Name] = PresetReader.Bool(values, field.Name, (bool)_values[field.Name]);
                    break;
                case RuleKind.Text:
                case RuleKind.Combo:
                    _values[field.Name] = PresetReader.String(values, field.Name, (string)_values[field.Name]);
                    break;
                case RuleKind.ItemSearch:
                    _values[field.Name] = (uint)PresetReader.Int(values, $"{field.Name}ItemId", (int)(uint)_values[field.Name]);
                    break;
            }
        }
    }

    private static object InitialValue(RuleField field) => field.Kind switch
    {
        RuleKind.Money or RuleKind.Int => Convert.ToInt32(field.Default ?? 0),
        RuleKind.Float => Convert.ToSingle(field.Default ?? 0f),
        RuleKind.Toggle => Convert.ToBoolean(field.Default ?? false),
        RuleKind.Text or RuleKind.Combo => field.Default as string ?? string.Empty,
        RuleKind.ItemSearch => Convert.ToUInt32(field.Default ?? 0u),
        _ => field.Default ?? 0
    };

    private static int ClampInt(RuleField field, int value)
    {
        if (field.Min.HasValue && field.Max.HasValue)
            return RuleClamp.Range(value, (int)field.Min.Value, (int)field.Max.Value);
        return field.Min.HasValue ? RuleClamp.Min(value, (int)field.Min.Value) : value;
    }
}
