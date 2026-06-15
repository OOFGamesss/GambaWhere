using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Pill-shaped game-type chip coloured by the game, using the same dull translucent fill and vivid
/// accent text as the events cards. Reusable for static display and for a pick-as-many toggle
/// selector (which adds a checkbox so the selected state is obvious).
/// </summary>
public static class GamePill
{
    private const float HPad = 10f;
    private const float VPad = 4f;
    private const float CheckGap = 6f;

    public static Vector2 CalcSize(string game, bool withCheckbox = false)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var textSize = ImGui.CalcTextSize(game);
        var width = textSize.X + HPad * 2f * scale;
        if (withCheckbox)
            width += textSize.Y + CheckGap * scale;
        return new Vector2(width, textSize.Y + VPad * 2f * scale);
    }

    public static void Draw(string game)
    {
        var size = CalcSize(game);
        Render(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), size, game, selected: true, hovered: false, showCheckbox: false);
        ImGui.Dummy(size);
    }

    public static bool DrawToggle(string game, bool selected, string idSuffix = "")
    {
        var size = CalcSize(game, withCheckbox: true);
        var clicked = ImGui.InvisibleButton($"##pill_{game}_{idSuffix}", size);
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        Render(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), size, game, selected, ImGui.IsItemHovered(), showCheckbox: true);
        return clicked;
    }

    public static float CalcWrappedHeight(IReadOnlyList<string> games, float wrapWidth)
    {
        if (games.Count == 0)
            return 0f;

        var spacing = ImGui.GetStyle().ItemSpacing;
        var rowHeight = CalcSize(games[0]).Y;
        var x = 0f;
        var rows = 1;

        for (var i = 0; i < games.Count; i++)
        {
            var w = CalcSize(games[i]).X;
            if (i == 0)
            {
                x = w;
            }
            else if (x + spacing.X + w <= wrapWidth)
            {
                x += spacing.X + w;
            }
            else
            {
                rows++;
                x = w;
            }
        }

        return rows * rowHeight + (rows - 1) * spacing.Y;
    }

    public static void DrawList(IReadOnlyList<string> games, float wrapWidth)
    {
        if (games.Count == 0)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rightLimit = ImGui.GetCursorScreenPos().X + wrapWidth;

        for (var i = 0; i < games.Count; i++)
        {
            if (i > 0 && ImGui.GetItemRectMax().X + spacing + CalcSize(games[i]).X <= rightLimit)
                ImGui.SameLine();
            Draw(games[i]);
        }
    }

    public static void DrawSelector(IReadOnlyList<string> options, HashSet<string> selected, float wrapWidth, string idSuffix = "")
    {
        if (options.Count == 0)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rightLimit = ImGui.GetCursorScreenPos().X + wrapWidth;

        for (var i = 0; i < options.Count; i++)
        {
            if (i > 0 && ImGui.GetItemRectMax().X + spacing + CalcSize(options[i], withCheckbox: true).X <= rightLimit)
                ImGui.SameLine();

            var isSelected = selected.Contains(options[i]);
            if (DrawToggle(options[i], isSelected, idSuffix))
            {
                if (isSelected)
                    selected.Remove(options[i]);
                else
                    selected.Add(options[i]);
            }
        }
    }

    private static void Render(ImDrawListPtr dl, Vector2 pos, Vector2 size, string game, bool selected, bool hovered, bool showCheckbox)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var (bg, accent) = EventCardRenderer.GetGameTypeColors(game);
        var rounding = size.Y * 0.5f;

        var fillAlpha = (selected ? 0.30f : 0.12f) + (hovered ? 0.10f : 0f);
        dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(bg.X, bg.Y, bg.Z, fillAlpha)), rounding);

        var borderAlpha = selected ? 0.80f : 0.35f;
        dl.AddRect(pos, pos + size, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, borderAlpha)),
            rounding, ImDrawFlags.None, 1f * scale);

        var textSize = ImGui.CalcTextSize(game);
        var textCol = new Vector4(accent.X, accent.Y, accent.Z, selected ? 1f : 0.80f);

        if (showCheckbox)
        {
            var box = size.Y - VPad * 2f * scale;
            var boxPos = new Vector2(pos.X + HPad * scale, pos.Y + (size.Y - box) * 0.5f);
            DrawCheckbox(dl, boxPos, box, accent, selected, scale);

            var textPos = new Vector2(boxPos.X + box + CheckGap * scale, pos.Y + (size.Y - textSize.Y) * 0.5f);
            dl.AddText(textPos, ImGui.GetColorU32(textCol), game);
        }
        else
        {
            var textPos = new Vector2(pos.X + (size.X - textSize.X) * 0.5f, pos.Y + (size.Y - textSize.Y) * 0.5f);
            dl.AddText(textPos, ImGui.GetColorU32(textCol), game);
        }
    }

    private static void DrawCheckbox(ImDrawListPtr dl, Vector2 pos, float box, Vector4 accent, bool selected, float scale)
    {
        var rounding = 2f * scale;
        var max = pos + new Vector2(box, box);

        if (!selected)
        {
            dl.AddRect(pos, max, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.7f)),
                rounding, ImDrawFlags.None, 1.5f * scale);
            return;
        }

        dl.AddRectFilled(pos, max, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.95f)), rounding);

        var tick = ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.10f, 1f));
        var thick = Math.Max(1.5f, 2f * scale);
        var p1 = pos + new Vector2(box * 0.22f, box * 0.52f);
        var p2 = pos + new Vector2(box * 0.42f, box * 0.72f);
        var p3 = pos + new Vector2(box * 0.78f, box * 0.26f);
        dl.AddLine(p1, p2, tick, thick);
        dl.AddLine(p2, p3, tick, thick);
    }
}
