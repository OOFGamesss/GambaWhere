using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using GambaWhere.Images;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class SupportTab
{
    private readonly ImageCache _imageCache;

    private const string OofGamesDiscordUrl = "https://discord.gg/vM6ff4h5Ym";

    private const string DiscordJoinLine = "Join the OOF Games Discord ";

    private const float LogoSide = 160f;

    private static readonly Vector4 FaqHeadingColour = new(1f, 0.85f, 0.4f, 1f);

    public SupportTab(ImageCache imageCache)
    {
        _imageCache = imageCache;
    }

    public void Draw()
    {
        DrawBranding();
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(8f);
        DrawSupportGuidance();
        DrawDiscordInvite();
    }

    private void DrawBranding()
    {
        var side = LogoSide * ImGuiHelpers.GlobalScale;
        var logoDrawSize = new Vector2(side, side);
        CentreForWidth(logoDrawSize.X);

        var tex = _imageCache.GetBundledPng("oofgames.png");
        if (tex != null)
            ImGui.Image(tex.Handle, logoDrawSize);
        else
            DrawLogoPlaceholder(logoDrawSize);

        ImGuiHelpers.ScaledDummy(4f);
        const string attribution = "Created by OOF Games";
        CentreForWidth(ImGui.CalcTextSize(attribution).X);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        ImGui.TextUnformatted(attribution);
        ImGui.PopStyleColor();
    }

    private static void DrawSupportGuidance()
    {
        DrawFaqSection(
            "##faq_venue",
            "How do I add a venue?",
            "Create a post in #add_venue with your venue name, Discord invite link and venue logo. We aim to list suitable venues as soon as possible.");

        DrawFaqSection(
            "##faq_bug",
            "How do I report a bug?",
            "Post in #report_bugs with a clear description of what you were doing when the problem occurred. Screenshots help us reproduce and fix issues.");

        DrawFaqSection(
            "##faq_feature",
            "How do I request a feature?",
            "Use #request_features and explain what you would like added, plus any context that helps us understand the request.");

        DrawFaqSection(
            "##faq_software",
            "Can I request a plugin, Discord bot or website?",
            "For a similar plugin, a Discord bot, another plugin type or a website, post in #request_software and we will see what we can do.");
    }

    private static void DrawFaqSection(string idSuffix, string heading, string body)
    {
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.18f, 0.18f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.24f, 0.24f, 0.30f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.28f, 0.28f, 0.34f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, FaqHeadingColour);

        if (ImGui.CollapsingHeader($"{heading}{idSuffix}", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.PopStyleColor(4);
            ImGui.TextWrapped(body);
        }
        else
        {
            ImGui.PopStyleColor(4);
        }

        ImGuiHelpers.ScaledDummy(6f);
    }

    private static void DrawDiscordInvite()
    {
        var style = ImGui.GetStyle();
        var rowH = ImGui.GetFrameHeight();
        var avail = ImGui.GetContentRegionAvail();
        var padBottom = 2f * ImGuiHelpers.GlobalScale;
        var liftUp = 14f * ImGuiHelpers.GlobalScale;
        var spareY = Math.Max(0f, avail.Y - rowH - padBottom - liftUp);
        if (spareY > 0f)
            ImGui.Dummy(new Vector2(1f, spareY));

        var btnW = rowH;
        var rowW = ImGui.CalcTextSize(DiscordJoinLine).X + style.ItemSpacing.X + btnW;
        CentreForWidth(rowW);

        ImGui.BeginGroup();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(DiscordJoinLine);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##openOofDiscord", FontAwesomeIcon.Globe))
            OpenBrowser.TryOpen(OofGamesDiscordUrl);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Open in browser:\n{OofGamesDiscordUrl}");

        ImGui.EndGroup();
    }

    private static void CentreForWidth(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - width) * 0.5f));
    }

    private static void DrawLogoPlaceholder(Vector2 size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            pos,
            pos + size,
            ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
            4f * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(size);
    }
}
