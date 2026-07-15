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

/// <summary>Support tab with the Gamba Where Discord bot invite link, install guide, and command reference.</summary>
public sealed class DiscordBotTab
{
    private const float DiscordLogoMaxWidth = 98f;

    private const string BotInviteUrl = "https://discord.com/oauth2/authorize?client_id=1500574056154534078";

    private readonly Configuration _config;
    private readonly ImageService _imageService;

    public DiscordBotTab(Configuration config, ImageService imageService)
    {
        _config = config;
        _imageService = imageService;
    }

    public void Draw()
    {
        DrawDiscordHeader();
        DrawInviteSection();

        DrawSetupGuide();
        DrawBotCommands();
        DrawBotPreview();
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
        DrawInviteButtons();
        ImGuiHelpers.ScaledDummy(10f);
    }

    private void DrawInviteButtons()
    {
        var hasInvite = !string.IsNullOrWhiteSpace(BotInviteUrl);
        var scale = ImGuiHelpers.GlobalScale;
        var btnH = ImGui.GetFrameHeight() + 4f * scale;
        var inviteW = 168f * scale;
        var copyW = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var rowW = inviteW + spacing + copyW;

        CentreForWidth(rowW);

        using (ImRaii.Disabled(!hasInvite))
        {
            using (UIHelper.PushGreenButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Robot, "Invite Bot", new Vector2(inviteW, btnH), "##InviteBot"))
                    OpenBrowser.TryOpen(BotInviteUrl);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(hasInvite
                    ? $"Open the bot invite in your browser:\n{BotInviteUrl}"
                    : "Invite link is not set yet.");
            }

            ImGui.SameLine(0f, spacing);

            if (ImGuiComponents.IconButton("##CopyInvite", FontAwesomeIcon.Copy))
                ImGui.SetClipboardText(BotInviteUrl.Trim());

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(hasInvite
                    ? "Copy the bot invite link to the clipboard."
                    : "Invite link is not set yet.");
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

    private void DrawInviteSection()
    {
        DrawSectionHeader("Invite the bot");

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            ImGui.TextWrapped(
                "Add the Gamba Where bot to your Discord server to post live gamba listings with your own nickname, "
                    + "avatar, and optional dealer or venue filters. You need the Manage Server permission to invite it.");
        }

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawSetupGuide()
    {
        DrawSectionHeader("Bot setup");

        GuideBullet("Click Invite Bot above, choose your Discord server, and authorise the invite.");

        GuideBullet(
            "In the channel that should show listings, give the bot permission to Send Messages, Embed Links, "
                + "Attach Files, and Manage Webhooks.");

        GuideBullet(
            "In your server, run /gambawhere. Only Discord server administrators can use this command.");

        GuideBullet(
            "Choose Add Tracker, pick the channel from the list Discord shows, then enter a bot nickname "
                + "(for example GambaWhere | Your Venue) and optionally an avatar image URL.");

        GuideBullet(
            "Add at least your venue name under venue filters so only events for your venue appear on the tracker. "
                + "You can also add dealer filters as Name plus World (for example LARA CROFT TWINTANIA).");

        GuideBullet(
            "Choose Create Tracker. An idle No gamba banner is posted as a test and updates automatically when events go live.");

        GuideBullet(
            "Use Amend Tracker to change channel, nickname, image, or filters later, or Delete Tracker to remove it.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private void DrawBotCommands()
    {
        DrawSectionHeader("Bot commands");

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            ImGui.TextWrapped(
                "Slash commands are used inside Discord. Management sessions time out after five minutes of inactivity.");
        }

        ImGuiHelpers.ScaledDummy(6f);

        CommandBlock(
            "/help",
            "Shows the command list and a short overview of what the bot does. Available to everyone.");

        CommandBlock(
            "/gambawhere",
            "Opens the management menu for this server. Restricted to server administrators. Options:");

        GuideBullet("View Trackers - lists every tracker set up for this server, including nickname, image, and filters.");

        GuideBullet(
            "Add Tracker - select a channel from the list, then set nickname, optional avatar URL, and dealer or venue filters.");

        GuideBullet(
            "Amend Tracker - edit Channel, Nickname, Image URL, or Filters on an existing tracker. Moving channel "
                + "reposts the idle banner and removes the old messages.");

        GuideBullet(
            "Delete Tracker - removes the tracker from the sheet and deletes the bot messages from that channel.");

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

    private void DrawBotPreview()
    {
        ImGuiHelpers.ScaledDummy(8f);

        DrawSectionHeader("Discord preview example");

        var scale = ImGuiHelpers.GlobalScale;
        var tex = _imageService.GetBundled("Screenshots/discordwebhookexamplev2.png");
        var innerW = ImGui.GetContentRegionAvail().X;

        if (tex != null && tex.Width > 0 && tex.Height > 0 && innerW > 1f)
        {
            var maxW = scale * 480f;
            var w = Math.Min(innerW - scale * 8f, maxW);
            var h = w * tex.Height / tex.Width;

            CentreForWidth(w);
            ImGui.Image(tex.Handle, new Vector2(w, h));
            ImGuiHelpers.ScaledDummy(8f);
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ThemeColours.AccentTextMuted(_config.SecondaryColour)))
            {
                ImGui.TextWrapped(tex == null
                    ? "Preview image is still loading; switch away and back if it does not appear."
                    : "Preview image has no usable size.");
            }
            ImGuiHelpers.ScaledDummy(8f);
        }

        ImGuiHelpers.ScaledDummy(6f);
    }

    private static void CentreForWidth(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - width) * 0.5f));
    }
}
