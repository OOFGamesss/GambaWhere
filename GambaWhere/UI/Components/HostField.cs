using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Themed, labelled field renderers shared by the host setup form and the game rule configs.
/// Each renderer draws an accent label, then a full-cell input, so fields tile neatly inside a
/// <see cref="RuleGrid"/>. Accent colours are read from <see cref="HostFieldTheme"/>.
/// </summary>
internal static class HostField
{
    private static readonly Vector4 CoinGold = new(0.96f, 0.78f, 0.26f, 1f);

    internal static void Label(string text) =>
        ImGui.TextColored(ThemeColours.AccentText(HostFieldTheme.Secondary), text);

    internal static void Money(string label, string id, ref int value)
    {
        Label(label);
        DrawCoin();
        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGuiEx.InputFancyNumeric(id, ref value, 0);
    }

    internal static bool Text(string label, string id, ref string value, int max)
    {
        Label(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.InputText(id, ref value, max);
    }

    internal static bool Int(string label, string id, ref int value)
    {
        Label(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.InputInt(id, ref value);
    }

    internal static bool Float(string label, string id, ref float value)
    {
        Label(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.InputFloat(id, ref value, 0.1f, 1.0f, "%.2f");
    }

    internal static bool Toggle(string label, string id, ref bool value)
    {
        Label(label);
        return ToggleSwitch.Draw(id, ref value, ThemeColours.ActiveCheckMark(HostFieldTheme.Secondary));
    }

    internal static void Combo(string label, string id, string preview, Action drawItems)
    {
        Label(label);
        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo(id, preview);
        if (combo)
            drawItems();
    }

    private static void DrawCoin()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, CoinGold))
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(FontAwesomeIcon.Coins.ToIconString());
    }
}
