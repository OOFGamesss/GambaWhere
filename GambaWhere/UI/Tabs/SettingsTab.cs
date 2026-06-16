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

    private const int BoosterKeyBufferLength = 256;

    private readonly Configuration _config;
    private readonly ImageCache _imageCache;
    private readonly IPluginLog _log;
    private readonly SessionPillOverlay _pillOverlay;

    private string _boosterKeyDraft = string.Empty;
    private bool _boosterDraftLoaded;

    public SettingsTab(Configuration config, ImageCache imageCache, IPluginLog log, SessionPillOverlay pillOverlay)
    {
        _config = config;
        _imageCache = imageCache;
        _log = log;
        _pillOverlay = pillOverlay;
    }

    public void DrawUiSection()
    {
        ImGui.Spacing();
        DrawPillOverlaySettings();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawMinimapHostSettings();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawThemeColours();
    }

    public void DrawChatSection()
    {
        ImGui.Spacing();
        DrawCompanionPluginDetection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAlertOptions();
    }

    public void DrawBoosterSection()
    {
        ImGui.Spacing();
        DrawBoosterKeySettings();
    }

    public void DrawOtherSection()
    {
        ImGui.Spacing();
        DrawImageCacheSettings();
    }

    private void DrawBoosterKeySettings()
    {
        if (!_boosterDraftLoaded)
        {
            _boosterKeyDraft = _config.BoosterKey ?? string.Empty;
            _boosterDraftLoaded = true;
        }

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Discord Server Booster");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextUnformatted(
            "Boost the OOF Games Discord server to unlock a shiny, holographic event card that "
                + "shimmers like a foil trading card. Once you are boosting, claim your key in "
                + "Discord and paste it below.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);

        GuideBullet("In the OOF Games Discord, run the /booster command in the booster channel.");
        GuideBullet("The bot replies with your personal booster key (only you can see it).");
        GuideBullet("Paste that key into the box below. It is sent with your events automatically.");

        ImGuiHelpers.ScaledDummy(8f);

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Booster Key");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.SetNextItemWidth(Math.Min(360f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("##BoosterKey", "GWB-xxxxxxxxxxxxxxxx", ref _boosterKeyDraft, BoosterKeyBufferLength);

        if (ImGui.IsItemDeactivatedAfterEdit())
            CommitBoosterKey();

        var saved = !string.IsNullOrWhiteSpace(_config.BoosterKey);
        if (saved)
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(new Vector4(0.35f, 0.85f, 0.45f, 1f),
                "Key saved. Your hosted events and recruitment listings will show the booster card.");

            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear", "##ClearBoosterKey"))
            {
                _boosterKeyDraft = string.Empty;
                CommitBoosterKey();
            }
        }

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.65f, 0.1f, 1f)))
        {
            ImGui.TextUnformatted(
                "Please do not share your key with anyone else. This is a thank you to you for boosting.");
        }
        ImGui.PopTextWrapPos();
    }

    private void CommitBoosterKey()
    {
        var trimmed = _boosterKeyDraft.Trim();
        _boosterKeyDraft = trimmed;
        _config.BoosterKey = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        _config.Save();
    }

    private void GuideBullet(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
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

    private void DrawMinimapHostSettings()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Minimap Host Icons");

        var enabled = _config.MinimapHostIconsEnabled;
        if (ImGui.Checkbox("Show a dice icon on the minimap for nearby hosts", ref enabled))
        {
            _config.MinimapHostIconsEnabled = enabled;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Places a game-coloured dice marker on your minimap for any active host\n" +
                "who is in your current area/instance. Hover the marker to see who they are.\n" +
                "Uses the existing 30-second event refresh, so it adds no extra network traffic.");
        }

        using (ImRaii.Disabled(!_config.MinimapHostIconsEnabled))
        {
            ImGui.Indent();
            ImGui.TextDisabled("Show markers for these games:");
            ImGui.Spacing();

            foreach (var game in GambaEventsTab.KnownGameTypes)
            {
                var (_, accent) = GameTypeColours.ForGame(game);
                var swatch = accent;
                ImGui.ColorButton($"##MinimapSwatch_{game}", swatch,
                    ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha,
                    new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                ImGui.SameLine();

                var gameEnabled = _config.IsMinimapGameTypeEnabled(game);
                if (ImGui.Checkbox($"{game}##MinimapGame_{game}", ref gameEnabled))
                {
                    _config.MinimapHostGameTypeEnabled[game] = gameEnabled;
                    _config.Save();
                }
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

        var venueCount = _imageCache.GetCachedImageCount();
        ImGui.TextUnformatted($"Venue images stored: {venueCount}");

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear Venue Image Cache", "##ClearImageCache"))
            _imageCache.ClearCache();

        ImGui.Spacing();

        var profileCount = _imageCache.GetCachedProfileImageCount();
        ImGui.TextUnformatted($"Profile images stored: {profileCount}");

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear Profile Image Cache", "##ClearProfileImageCache"))
            _imageCache.ClearProfileCache();
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
