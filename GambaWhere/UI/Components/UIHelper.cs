using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace GambaWhere.UI.Components;

/// <summary>
/// Shared UI rendering helpers used across all tab components.
/// </summary>
internal static class UIHelper
{
    internal static bool IconTextButton(FontAwesomeIcon icon, string label, string id = "")
    {
        var iconFont   = UiBuilder.IconFont;
        var defaultFont = ImGui.GetFont();
        var fontSize   = ImGui.GetFontSize();
        var iconStr    = icon.ToIconString();
        var style      = ImGui.GetStyle();

        ImGui.PushFont(iconFont);
        var iconSize = ImGui.CalcTextSize(iconStr);
        ImGui.PopFont();

        var textSize = ImGui.CalcTextSize(label);
        var height   = Math.Max(iconSize.Y, textSize.Y);

        var btnSize = new Vector2(
            style.FramePadding.X * 2 + iconSize.X + style.ItemInnerSpacing.X + textSize.X,
            style.FramePadding.Y * 2 + height);

        var btnId   = string.IsNullOrEmpty(id) ? $"##{label}" : id;
        var clicked = ImGui.InvisibleButton(btnId, btnSize);

        var isHovered = ImGui.IsItemHovered();
        var isActive  = ImGui.IsItemActive();

        var bgColor = isActive  ? ImGui.GetColorU32(ImGuiCol.ButtonActive)
                    : isHovered ? ImGui.GetColorU32(ImGuiCol.ButtonHovered)
                    :             ImGui.GetColorU32(ImGuiCol.Button);

        var dl  = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        dl.AddRectFilled(min, max, bgColor, style.FrameRounding);

        if (style.FrameBorderSize > 0f)
            dl.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), style.FrameRounding,
                ImDrawFlags.None, style.FrameBorderSize);

        var textColor = ImGui.GetColorU32(ImGuiCol.Text);

        var iconOffsetY = (height - iconSize.Y) * 0.5f;
        var textOffsetY = (height - textSize.Y) * 0.5f;

        var iconPos = new Vector2(min.X + style.FramePadding.X, min.Y + style.FramePadding.Y + iconOffsetY);
        dl.AddText(iconFont, fontSize, iconPos, textColor, iconStr);

        var textPos = new Vector2(iconPos.X + iconSize.X + style.ItemInnerSpacing.X,
                                  min.Y + style.FramePadding.Y + textOffsetY);
        dl.AddText(defaultFont, fontSize, textPos, textColor, label);

        return clicked;
    }

    internal static Vector2 CalcButtonSize(FontAwesomeIcon icon, string label)
    {
        var iconFont = UiBuilder.IconFont;
        var iconStr  = icon.ToIconString();
        var style    = ImGui.GetStyle();

        ImGui.PushFont(iconFont);
        var iconSize = ImGui.CalcTextSize(iconStr);
        ImGui.PopFont();

        var textSize = ImGui.CalcTextSize(label);
        var height   = Math.Max(iconSize.Y, textSize.Y);

        return new Vector2(
            style.FramePadding.X * 2 + iconSize.X + style.ItemInnerSpacing.X + textSize.X,
            style.FramePadding.Y * 2 + height);
    }

    internal static ImRaii.ColorDisposable PushGreenButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.1f,  0.6f,  0.1f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.2f,  0.8f,  0.2f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.05f, 0.4f,  0.05f, 1f));

    internal static ImRaii.ColorDisposable PushRedButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.6f,  0.1f,  0.1f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.8f,  0.2f,  0.2f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.4f,  0.05f, 0.05f, 1f));

    internal static ImRaii.ColorDisposable PushBlueButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.1f,  0.3f,  0.7f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.2f,  0.45f, 0.9f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.05f, 0.2f,  0.5f,  1f));

    internal static ImRaii.ColorDisposable PushAmberButtonColours() =>
        new ImRaii.ColorDisposable()
            .Push(ImGuiCol.Button,        new Vector4(0.7f,  0.5f,  0.0f,  1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.9f,  0.65f, 0.0f,  1f))
            .Push(ImGuiCol.ButtonActive,  new Vector4(0.5f,  0.35f, 0.0f,  1f));
}
