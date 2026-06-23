using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

// Chat settings sub-tab: companion plugin detection and in-game alert options.
public partial class SettingsTab
{
    private static readonly string[] SoundEffectOptions = BuildSoundEffectOptions();

    public void DrawChatSection()
    {
        ImGui.Spacing();
        DrawCompanionPluginDetection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAlertOptions();
    }

    private void DrawCompanionPluginDetection()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Companion Plugin Detection");

        var enabled = _config.AutoSessionDetection;

        if (ImGui.Checkbox("Remind to start a session on companion plugin open", ref enabled))
        {
            _config.AutoSessionDetection = enabled;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "When a supported companion plugin opens (e.g. Chocobo Racing), a chat\n" +
                "message will appear reminding you to start a session, with a clickable link\n" +
                "that opens GambaWhere and pre-selects the correct game type for you.");
        }
    }

    private void DrawAlertOptions()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Alerts");

        var toastEnabled = _config.AlertToastEnabled;
        if (ImGui.Checkbox("Show popup notification when an alert fires", ref toastEnabled))
        {
            _config.AlertToastEnabled = toastEnabled;
            _config.Save();
        }

        var soundEnabled = _config.AlertSoundEnabled;
        if (ImGui.Checkbox("Play sound effect when an alert fires", ref soundEnabled))
        {
            _config.AlertSoundEnabled = soundEnabled;
            _config.Save();
        }

        using (ImRaii.Disabled(!_config.AlertSoundEnabled))
        {
            ImGui.Indent();

            ImGui.Text("Sound:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f);

            var idx = Math.Clamp(_config.AlertSoundEffectId - 1, 0, SoundEffectOptions.Length - 1);
            if (ImGui.Combo("##AlertSe", ref idx, SoundEffectOptions, SoundEffectOptions.Length))
            {
                _config.AlertSoundEffectId = idx + 1;
                _config.Save();
            }

            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.VolumeUp, "Test", "##TestSound"))
                PlaySoundEffect(Math.Clamp(_config.AlertSoundEffectId, 1, 16));

            ImGui.Unindent();
        }

        var pauseInDuty = _config.AlertPauseInDuty;
        if (ImGui.Checkbox("Silence alerts while in a duty", ref pauseInDuty))
        {
            _config.AlertPauseInDuty = pauseInDuty;
            _config.Save();
        }
    }

    private void PlaySoundEffect(int id)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)id);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PlayChatSoundEffect failed for SE {Id}", id);
        }
    }

    private static string[] BuildSoundEffectOptions()
    {
        var arr = new string[16];
        for (var i = 0; i < 16; i++)
            arr[i] = $"SE {i + 1}";
        return arr;
    }
}
