using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Auto-flowing column layout for rule fields. Flows fields into as many equal-width columns
/// as fit the available width, giving the multi-column field grid without each rule needing to
/// know the host window size. Wrap in a using block and call <see cref="Cell"/> before each field.
/// The ImGui table here is driven directly rather than via ImRaii, because ImRaii's table handle
/// is a ref struct and so cannot live inside this reusable disposable.
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
