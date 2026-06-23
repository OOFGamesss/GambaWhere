using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Draws a themed card with a primary-tinted background, rounded corners, outer margin, and accent border.
/// </summary>
public sealed class ThemedCard
{
    private readonly Dictionary<string, float> _cardHeights = new();

    public void Draw(string id, string title, Vector4 primary, Vector4 secondary, Action content)
        => Draw(id, title, primary, secondary, 0f, content);

    public void Draw(string id, string title, Vector4 primary, Vector4 secondary, Action content, Action? headerActions, float headerActionsWidth = 0f)
        => Draw(id, title, primary, secondary, 0f, content, headerActions, headerActionsWidth);

    public void Draw(string id, string title, Vector4 primary, Vector4 secondary, float fixedHeight, Action content, Action? headerActions = null, float headerActionsWidth = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 6f * scale;
        var hPad = 22f * scale;
        var vPad = 12f * scale;
        var margin = 8f * scale;

        var startX = ImGui.GetCursorPosX();
        var availW = ImGui.GetContentRegionAvail().X;
        var cardW = Math.Max(60f * scale, availW - margin * 2f);

        ImGui.SetCursorPosX(startX + margin);
        var top = ImGui.GetCursorScreenPos();

        var bg = ThemeColours.CardBackground(primary);
        var bodyH = fixedHeight > 0f
            ? fixedHeight
            : (_cardHeights.TryGetValue(id, out var h) ? h : 4f * scale);

        using (ImRaii.PushColor(ImGuiCol.ChildBg, bg))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, rounding))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(hPad, vPad)))
        {
            using var child = ImRaii.Child(id, new Vector2(cardW, bodyH), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding);
            if (child.Success)
            {
                var y0 = ImGui.GetCursorPosY();

                DrawTitleRow(title, secondary, headerActions, headerActionsWidth);

                if (headerActions != null)
                    ImGuiHelpers.ScaledDummy(2f);

                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(2f);
                content();
                if (fixedHeight <= 0f)
                    _cardHeights[id] = ImGui.GetCursorPosY() - y0 + vPad * 2f;
            }
        }

        var bottom = ImGui.GetCursorScreenPos().Y;
        var border = new Vector4(primary.X, primary.Y, primary.Z, 0.55f);
        ImGui.GetWindowDrawList().AddRect(
            top,
            new Vector2(top.X + cardW, bottom),
            ImGui.GetColorU32(border),
            rounding,
            ImDrawFlags.None,
            1.5f * scale);

        ImGui.SetCursorPosX(startX);
    }

    private static void DrawTitleRow(string title, Vector4 secondary, Action? headerActions, float headerActionsWidth)
    {
        var rowStartX = ImGui.GetCursorPosX();
        var rowY = ImGui.GetCursorPosY();
        var availW = ImGui.GetContentRegionAvail().X;
        var scale = ImGuiHelpers.GlobalScale;
        var titleSize = ImGui.CalcTextSize(title);
        var rowH = Math.Max(titleSize.Y, ImGui.GetFrameHeight());

        ImGui.SetCursorPos(new Vector2(rowStartX + Math.Max(0f, (availW - titleSize.X) * 0.5f), rowY));
        ImGui.TextColored(ThemeColours.AccentText(secondary), title);

        if (headerActions != null)
        {
            var actionsW = headerActionsWidth > 0f
                ? headerActionsWidth
                : ImGui.GetFrameHeight() * 2f + ImGui.GetStyle().ItemSpacing.X;

            var actionY = rowY - 4f * scale;
            ImGui.SetCursorPos(new Vector2(rowStartX + availW - actionsW, actionY));
            headerActions();
        }

        ImGui.SetCursorPosY(rowY + rowH);
    }
}
