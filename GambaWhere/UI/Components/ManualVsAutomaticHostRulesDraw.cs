using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.Rules;
using GambaWhere.State;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>Host UI: manual fields versus automatic IPC rules, with active-side highlighting.</summary>
public static class ManualVsAutomaticHostRulesDraw
{
    private static float s_naturalHeightManual;
    private static float s_naturalHeightAuto;

    /// <summary>Outer height for both rule panels from last layout; does not track host window resize.</summary>
    private static float s_lockedRulesRowOuterHeight;

    public static void Draw(
        HostFormState form,
        IRuleConfig manualRules,
        IAutomaticHostRuleSource automaticSource,
        object? ipcContext,
        Vector4 primary,
        Vector4 secondary)
    {
        if (ipcContext == null)
            form.UseManualHostRules = true;

        var canUseAuto = ipcContext != null;
        var useManual = form.UseManualHostRules;
        var manualSectionActive = useManual || !canUseAuto;
        var autoSectionActive = canUseAuto && !useManual;
        var autoLabel = $"Use game info from {automaticSource.AutomaticRulesPluginName}##gw_auto";

        var padManual = Math.Max(0f, s_naturalHeightAuto - s_naturalHeightManual);
        var padAuto = Math.Max(0f, s_naturalHeightManual - s_naturalHeightAuto);

        var style = ImGui.GetStyle();
        var chromeY = style.WindowPadding.Y * 2f + style.ChildBorderSize * 2f;

        var rowH = s_lockedRulesRowOuterHeight > 0f ? s_lockedRulesRowOuterHeight : 0f;

        if (!ImGui.BeginTable("##gw_host_rule_source", 2, ImGuiTableFlags.None))
            return;

        ImGui.TableSetupColumn("##gw_manual", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##gw_auto", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, manualSectionActive ? ThemeColours.SectionActiveBg(primary) : ThemeColours.SectionInactiveBg(primary));
        ImGui.BeginChild("gw_host_manual_rules", new Vector2(-1, rowH), true, ImGuiWindowFlags.NoScrollbar);
        PushWidgetColoursForSection(manualSectionActive, primary, secondary);
        var manualY0 = ImGui.GetCursorPosY();
        {
            var manualCb = useManual;
            if (!canUseAuto)
            {
                ImGui.BeginDisabled();
                manualCb = true;
            }

            if (ImGui.Checkbox("Set game info manually##gw_manual", ref manualCb) && canUseAuto)
                form.UseManualHostRules = manualCb;

            if (!canUseAuto)
                ImGui.EndDisabled();

            ImGuiHelpers.ScaledDummy(4f);

            if (!useManual && canUseAuto)
                ImGui.BeginDisabled();
            manualRules.Draw();
            if (!useManual && canUseAuto)
                ImGui.EndDisabled();
        }

        s_naturalHeightManual = Math.Max(0f, ImGui.GetCursorPosY() - manualY0);
        ImGui.Dummy(new Vector2(0, padManual));
        var spanManual = ImGui.GetCursorPosY() - manualY0;
        var needOuterManual = spanManual + chromeY;
        PopWidgetColoursForSection();
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.TableSetColumnIndex(1);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, autoSectionActive ? ThemeColours.SectionActiveBg(primary) : ThemeColours.SectionInactiveBg(primary));
        ImGui.BeginChild("gw_host_auto_rules", new Vector2(-1, rowH), true, ImGuiWindowFlags.NoScrollbar);
        PushWidgetColoursForSection(autoSectionActive, primary, secondary);
        var autoY0 = ImGui.GetCursorPosY();
        {
            var autoCb = canUseAuto && !useManual;
            if (!canUseAuto)
                ImGui.BeginDisabled();

            if (ImGui.Checkbox(autoLabel, ref autoCb) && canUseAuto)
                form.UseManualHostRules = !autoCb;

            if (!canUseAuto)
                ImGui.EndDisabled();

            ImGuiHelpers.ScaledDummy(4f);
            automaticSource.DrawAutomaticRulesSummary(ipcContext);
        }

        s_naturalHeightAuto = Math.Max(0f, ImGui.GetCursorPosY() - autoY0);
        ImGui.Dummy(new Vector2(0, padAuto));
        var spanAuto = ImGui.GetCursorPosY() - autoY0;
        var needOuterAuto = spanAuto + chromeY;
        PopWidgetColoursForSection();
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.EndTable();

        s_lockedRulesRowOuterHeight = Math.Max(needOuterManual, needOuterAuto);
    }

    private static void PushWidgetColoursForSection(bool sectionActive, Vector4 primary, Vector4 secondary)
    {
        if (sectionActive)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ThemeColours.ActiveFrameBg(primary));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ThemeColours.ActiveFrameBgHovered(primary));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ThemeColours.ActiveFrameBgActive(primary));
            ImGui.PushStyleColor(ImGuiCol.Border, ThemeColours.ActiveBorder(primary));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ThemeColours.ActiveCheckMark(secondary));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.18f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.24f, 0.24f, 0.27f, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.28f, 0.28f, 0.32f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, ThemeColours.InactiveBorder(primary));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ThemeColours.InactiveCheckMark(secondary));
        }
    }

    private static void PopWidgetColoursForSection() => ImGui.PopStyleColor(5);
}
