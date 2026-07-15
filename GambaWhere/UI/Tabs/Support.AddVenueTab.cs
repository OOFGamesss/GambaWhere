using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Support tab with the OOF Games Discord join link and a guide for adding venues via #add_venue.</summary>
public sealed class AddVenueTab
{
    private const float DiscordLogoMaxWidth = 98f;

    private const string OofGamesDiscordUrl = "https://discord.gg/vM6ff4h5Ym";

    private readonly Configuration _config;
    private readonly ImageService _imageService;

    public AddVenueTab(Configuration config, ImageService imageService)
    {
        _config = config;
        _imageService = imageService;
    }

    public void Draw()
    {
        DrawDiscordHeader();
        DrawJoinSection();
        DrawSetupGuide();
        DrawVenueCommands();
    }

    private void DrawDiscordHeader()
    {
        ImGuiHelpers.ScaledDummy(10f);

        var scale = ImGuiHelpers.GlobalScale;
        var maxW = DiscordLogoMaxWidth * scale;

        Vector2 drawSize;
        var tex = _imageService.GetBundled("Icons/discordlogo.png");
        if (tex != null && tex.Width > 0 && tex.Height > 0)
        {
            var ratio = tex.Height / (float)tex.Width;
            drawSize = new Vector2(maxW, maxW * ratio);
        }
        else
            drawSize = new Vector2(maxW, 48f * scale);

        CentreForWidth(drawSize.X);
        if (tex != null)
            ImGui.Image(tex.Handle, drawSize);
        else
            ImGui.Dummy(drawSize);

        ImGuiHelpers.ScaledDummy(10f);
        DrawJoinButtons();
        ImGuiHelpers.ScaledDummy(10f);
    }

    private void DrawJoinButtons()
    {
        var hasInvite = !string.IsNullOrWhiteSpace(OofGamesDiscordUrl);
        var scale = ImGuiHelpers.GlobalScale;
        var btnH = ImGui.GetFrameHeight() + 4f * scale;
        var joinW = 168f * scale;
        var copyW = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var rowW = joinW + spacing + copyW;

        CentreForWidth(rowW);

        using (ImRaii.Disabled(!hasInvite))
        {
            using (UIHelper.PushGreenButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Comments, "Join Discord", new Vector2(joinW, btnH), "##JoinDiscord"))
                    OpenBrowser.TryOpen(OofGamesDiscordUrl);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(hasInvite
                    ? $"Open the OOF Games Discord in your browser:\n{OofGamesDiscordUrl}"
                    : "Discord invite link is not set yet.");
            }

            ImGui.SameLine(0f, spacing);

            if (ImGuiComponents.IconButton("##CopyDiscordInvite", FontAwesomeIcon.Copy))
                ImGui.SetClipboardText(OofGamesDiscordUrl.Trim());

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(hasInvite
                    ? "Copy the Discord invite link to the clipboard."
                    : "Discord invite link is not set yet.");
            }
        }
    }

    private void DrawSectionHeader(string label)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, _config.SecondaryColour))
            ImGui.TextUnformatted(label);

        ImGuiHelpers.ScaledDummy(2f);
        using (ImRaii.PushColor(ImGuiCol.Separator, ThemeColours.SectionSeparator(_config.PrimaryColour)))
            ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

    private void DrawJoinSection()
    {
        DrawSectionHeader("Join Discord");

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            ImGui.TextWrapped(
                "Venue submissions are handled in the OOF Games Discord. Join the server, then open #add_venue "
                    + "under the Support category to add, edit, or delete venues on Gamba Where.");
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawSetupGuide()
    {
        DrawSectionHeader("Adding Venue Guide");

        GuideBullet("In Discord, open #add_venue under the Support category.");

        GuideBullet("Use /myvenues to see venues you have submitted.");

        GuideBullet("Choose Add venue to submit a new venue, or Edit venue to change one you already submitted.");

        GuideBullet("For both Add and Edit you then pick how to enter the details:");

        GuideSubBullet("Partake URL - use this if you have a Partake link for the venue.");

        GuideSubBullet(
            "Manual - use this if you have the venue name, Discord URL (optional), and image at hand.");

        GuideBullet(
            "Choose Delete venue to remove any venues you have submitted that you no longer want on Gamba Where.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawVenueCommands()
    {
        DrawSectionHeader("Discord commands");

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            ImGui.TextWrapped(
                "Slash commands are used inside Discord in #add_venue.");
        }

        ImGuiHelpers.ScaledDummy(6f);

        CommandBlock(
            "/myvenues",
            "Lists venues you have submitted so you can review, edit, or delete them.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void CommandBlock(string name, string description)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, _config.SecondaryColour))
            ImGui.TextUnformatted(name);

        ImGuiHelpers.ScaledDummy(2f);

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
            ImGui.TextWrapped(description);

        ImGuiHelpers.ScaledDummy(6f);
    }

    private void GuideBullet(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped(text);
        }
        ImGuiHelpers.ScaledDummy(2f);
    }

    private void GuideSubBullet(string text)
    {
        ImGui.Indent(18f * ImGuiHelpers.GlobalScale);
        GuideBullet(text);
        ImGui.Unindent(18f * ImGuiHelpers.GlobalScale);
    }

    private static void CentreForWidth(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - width) * 0.5f));
    }
}
