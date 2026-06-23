using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Table grid for laying out rule fields in a structured layout.
/// </summary>
internal sealed class RuleGrid : IDisposable
{
    private const int MaxColumns = 4;

    internal bool Open { get; }

    private RuleGrid(bool open) => Open = open;

    internal static RuleGrid Begin(string id, float minCellWidth = 190f)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var scaledMin = minCellWidth * ImGuiHelpers.GlobalScale;
        var columns = Math.Clamp((int)(avail / scaledMin), 1, MaxColumns);

        var open = ImGui.BeginTable(id, columns,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.PadOuterX);

        return new RuleGrid(open);
    }

    internal void Cell()
    {
        if (Open)
            ImGui.TableNextColumn();
    }

    public void Dispose()
    {
        if (Open)
            ImGui.EndTable();
    }
}
