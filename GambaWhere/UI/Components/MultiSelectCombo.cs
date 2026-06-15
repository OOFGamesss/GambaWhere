using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace GambaWhere.UI.Components;

/// <summary>Combo box allowing multiple options to be selected.</summary>
public static class MultiSelectCombo
{

    public static void Draw(string id, string hint, IReadOnlyList<string> items, HashSet<string> selected)
    {
        var preview = BuildPreview(hint, selected);

        if (!ImGui.BeginCombo(id, preview))
            return;

        DrawAnyOption(selected);
        ImGui.Separator();
        DrawItemOptions(items, selected);

        ImGui.EndCombo();
    }

    private static string BuildPreview(string hint, HashSet<string> selected) => selected.Count switch
    {
        0 => hint,
        1 => selected.First(),
        _ => $"{selected.Count} selected"
    };

    private static void DrawAnyOption(HashSet<string> selected)
    {
        if (ImGui.Selectable("Any", selected.Count == 0))
            selected.Clear();
    }

    private static void DrawItemOptions(IReadOnlyList<string> items, HashSet<string> selected)
    {
        foreach (var item in items)
        {
            var isSelected = selected.Contains(item);
            if (ImGui.Selectable(item, isSelected, ImGuiSelectableFlags.DontClosePopups))
                ToggleItem(item, isSelected, selected);
        }
    }

    private static void ToggleItem(string item, bool wasSelected, HashSet<string> selected)
    {
        if (wasSelected)
            selected.Remove(item);
        else
            selected.Add(item);
    }
}
