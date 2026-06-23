using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

// UI settings sub-tab: session overlay pill, minimap host icons, and theme colours.
public partial class SettingsTab
{
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
}
