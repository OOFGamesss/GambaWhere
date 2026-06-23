using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

// Booster Key settings sub-tab: Discord server booster key entry and management.
public partial class SettingsTab
{
    private const int BoosterKeyBufferLength = 256;

    private string _boosterKeyDraft = string.Empty;
    private bool _boosterDraftLoaded;

    public void DrawBoosterSection()
    {
        ImGui.Spacing();
        DrawBoosterKeySettings();
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
                "Key saved. Please head to the profiles tab and select the booster border and card effect.");

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
}
