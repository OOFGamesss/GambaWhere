using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace GambaWhere.UI.Components;

public static class ItemSearchCombo
{
    private static Dictionary<uint, string>? _itemById;
    private static List<(uint Id, string Name)>? _allSorted;
    private static List<(uint Id, string Name)> _filtered = [];
    private static string _search = string.Empty;
    private static bool _wasOpen;

    public static string? GetItemName(uint itemId) =>
        itemId == 0 ? null : _itemById?.GetValueOrDefault(itemId);

    public static void Draw(string id, ref uint selectedItemId)
    {
        EnsureLoaded();

        var preview = selectedItemId == 0
            ? "(None)"
            : _itemById!.GetValueOrDefault(selectedItemId, "(Unknown)");

        var isOpen = ImGui.BeginCombo(id, preview);

        if (!isOpen)
        {
            if (_wasOpen)
            {
                _search = string.Empty;
                _wasOpen = false;
            }

            return;
        }

        if (!_wasOpen)
        {
            ImGui.SetKeyboardFocusHere();
            _wasOpen = true;
        }

        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##ItemSearchInput", ref _search, 128))
            RefreshFilter();

        if (ImGui.Selectable("(None)", selectedItemId == 0))
            selectedItemId = 0;

        ImGui.Separator();

        foreach (var (itemId, name) in _filtered)
        {
            if (ImGui.Selectable($"{name}##{itemId}", selectedItemId == itemId))
                selectedItemId = itemId;
        }

        ImGui.EndCombo();
    }

    private static void EnsureLoaded()
    {
        if (_allSorted != null) return;

        _itemById = [];
        _allSorted = [];

        var sheet = GambaWhere.DataManager.GetExcelSheet<Item>();
        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            _itemById[row.RowId] = name;
            _allSorted.Add((row.RowId, name));
        }

        _allSorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        RefreshFilter();
    }

    private static void RefreshFilter()
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            _filtered = _allSorted!.Take(200).ToList();
            return;
        }

        _filtered = _allSorted!
            .Where(x => x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .Take(200)
            .ToList();
    }
}
