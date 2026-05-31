using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.UI.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.UI;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Settings tab providing UI, chat alert, and image cache configuration options.</summary>
public class SettingsTab
{
    private static readonly string[] SoundEffectOptions = BuildSoundEffectOptions();

    private readonly Configuration _config;
    private readonly ImageCache _imageCache;
    private readonly IPluginLog _log;
    private readonly SessionPillOverlay _pillOverlay;

    public SettingsTab(Configuration config, ImageCache imageCache, IPluginLog log, SessionPillOverlay pillOverlay)
    {
        _config = config;
        _imageCache = imageCache;
        _log = log;
        _pillOverlay = pillOverlay;
    }

    public void Draw()
    {
        using var tabBar = ImRaii.TabBar("SettingsTabs");
        if (!tabBar) return;

        ImGui.PushStyleColor(ImGuiCol.Text, _config.SecondaryColour);
        using (var uiTab = ImRaii.TabItem("UI"))
        {
            ImGui.PopStyleColor();
            if (uiTab)
            {
                ImGui.Spacing();
                DrawPillOverlaySettings();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                DrawThemeColours();
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Text, _config.SecondaryColour);
        using (var chatTab = ImRaii.TabItem("Chat"))
        {
            ImGui.PopStyleColor();
            if (chatTab)
            {
                ImGui.Spacing();
                DrawCompanionPluginDetection();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                DrawAlertOptions();
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Text, _config.SecondaryColour);
        using (var otherTab = ImRaii.TabItem("Other"))
        {
            ImGui.PopStyleColor();
            if (otherTab)
            {
                ImGui.Spacing();
                DrawImageCacheSettings();
            }
        }
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
                "When a supported companion plugin opens (e.g. Chocobo Racing Gamba), a chat\n" +
                "message will appear reminding you to start a session, with a clickable link\n" +
                "that opens GambaWhere and pre-selects the correct game type for you.");
        }
    }

    private void DrawPillOverlaySettings()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Session Overlay Pill");

        var enabled = _config.PillOverlayEnabled;
        if (ImGui.Checkbox("Show overlay pill during active sessions", ref enabled))
        {
            _config.PillOverlayEnabled = enabled;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Displays a floating pill showing the session timer, game name,\n" +
                "and session controls while a session is active.");
        }

        using (ImRaii.Disabled(!_config.PillOverlayEnabled))
        {
            ImGui.Indent();

            if (_pillOverlay.IsMoving)
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Lock, "Lock Pill in Place", "##LockPill"))
                    _pillOverlay.ExitMoveMode();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Click to lock the overlay at its current position.");

                ImGui.SameLine();

                if (UIHelper.IconTextButton(FontAwesomeIcon.Undo, "Reset Pill Position", "##ResetPillPos"))
                    _pillOverlay.ResetPosition();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Snaps the overlay to the centre of your screen.");
            }
            else
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowsUpDownLeftRight, "Move Pill", "##MovePill"))
                    _pillOverlay.EnterMoveMode();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Drag the overlay to reposition it, then return here to lock it in place.");
            }

            ImGui.Unindent();
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
    }

    private void DrawThemeColours()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Theme Colours");
        ImGui.Spacing();

        var labelOffset = 100f * ImGuiHelpers.GlobalScale;
        var pickerWidth = 200f * ImGuiHelpers.GlobalScale;

        var primary = _config.PrimaryColour;
        ImGui.Text("Primary");
        ImGui.SameLine(labelOffset);
        ImGui.SetNextItemWidth(pickerWidth);
        if (ImGui.ColorEdit4("##PrimaryColour", ref primary, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.DisplayHex))
        {
            primary.W = 1f;
            _config.PrimaryColour = primary;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Used for card backgrounds, section panels, and borders.");

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Undo, "Reset", "##ResetPrimary"))
        {
            _config.PrimaryColour = Configuration.DefaultPrimaryColour;
            _config.Save();
        }

        var secondary = _config.SecondaryColour;
        ImGui.Text("Secondary");
        ImGui.SameLine(labelOffset);
        ImGui.SetNextItemWidth(pickerWidth);
        if (ImGui.ColorEdit4("##SecondaryColour", ref secondary, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.DisplayHex))
        {
            secondary.W = 1f;
            _config.SecondaryColour = secondary;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Used for accent text, headings, and highlighted labels.");

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Undo, "Reset", "##ResetSecondary"))
        {
            _config.SecondaryColour = Configuration.DefaultSecondaryColour;
            _config.Save();
        }
    }

    private void DrawImageCacheSettings()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Image Cache");
        var count = _imageCache.GetCachedImageCount();
        ImGui.TextUnformatted($"Images stored: {count}");

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear Venue Image Cache", "##ClearImageCache"))
            _imageCache.ClearCache();
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
