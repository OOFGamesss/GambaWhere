using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.UI.CardEffects;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>Shared profile popup used by the Gamba Events and Recruitment tabs.</summary>
internal static class ProfilePopup
{
    public sealed class Data
    {
        public string DisplayName = string.Empty;
        public string? ProfileImageUrl;
        public string? Bio;
        public List<string> PreferredGames = new();
        public bool Booster;
        public string? BorderStyle;
        public string? CardEffectStyle;
    }

    public static void Draw(string popupId, ref bool openRequested, ImageCache imageCache, Configuration config, Data? data)
    {
        if (openRequested)
        {
            ImGui.OpenPopup(popupId);
            openRequested = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 6f * scale;
        var cardEffect = CardEffectFor(data);
        var cardBg = CardEffectResolver.BaseColour(cardEffect) ?? ThemeColours.TintedWindowBg(config.PrimaryColour);

        ImGui.SetNextWindowSizeConstraints(new Vector2(380f * scale, 0f), new Vector2(520f * scale, float.MaxValue));

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var colBg = ImRaii.PushColor(ImGuiCol.PopupBg, cardBg, data != null);
        using var popup = ImRaii.Popup(popupId);
        if (!popup.Success)
            return;

        if (data == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var avatarTex = !string.IsNullOrWhiteSpace(data.ProfileImageUrl) ? imageCache.GetProfile(data.ProfileImageUrl!) : null;
        DrawCardBody(imageCache, config, data, avatarTex, drawCloseButton: true);
    }

    public static void DrawInlinePreview(ImageCache imageCache, Configuration config, Data data, IDalamudTextureWrap? localTex)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 6f * scale;
        var cardEffect = CardEffectFor(data);
        var cardBg = CardEffectResolver.BaseColour(cardEffect) ?? ThemeColours.TintedWindowBg(config.PrimaryColour);

        var avail = ImGui.GetContentRegionAvail().X;
        var cardW = Math.Min(avail, 440f * scale);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - cardW) * 0.5f));

        var clip = (ImGui.GetWindowDrawList().GetClipRectMin(), ImGui.GetWindowDrawList().GetClipRectMax());

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
        using var colBg = ImRaii.PushColor(ImGuiCol.ChildBg, cardBg);

        using var child = ImRaii.Child("##profile_preview", new Vector2(cardW, 330f * scale), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding);
        if (!child.Success) return;

        DrawCardBody(imageCache, config, data, localTex, drawCloseButton: false, clip, bioMaxLines: 2);
    }

    private static void DrawCardBody(ImageCache imageCache, Configuration config, Data data,
        IDalamudTextureWrap? avatarTex, bool drawCloseButton, (Vector2 Min, Vector2 Max)? clip = null,
        int? bioMaxLines = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 6f * scale;
        var cardEffect = CardEffectFor(data);
        var accent = config.SecondaryColour;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var width = ImGui.GetContentRegionAvail().X;

        ImGuiHelpers.ScaledDummy(6f);
        var diameter = 104f * scale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - diameter) * 0.5f));
        DrawCentredAvatar(imageCache, data, avatarTex, diameter);

        ImGuiHelpers.ScaledDummy(6f);
        var displayName = string.IsNullOrWhiteSpace(data.DisplayName) ? "(no name)" : data.DisplayName;
        CentreText(width, displayName, () => ImGui.TextColored(accent, displayName));
        if (data.Booster)
            CentreText(width, "Booster", () => ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.78f, 1f), "Booster"));

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        var contentWidth = ImGui.GetContentRegionAvail().X;

        ImGui.TextDisabled("Bio");
        if (!string.IsNullOrWhiteSpace(data.Bio))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
            var bioText = bioMaxLines is { } lines ? TextTruncate.ToLines(data.Bio, contentWidth, lines) : data.Bio;
            ImGui.TextWrapped(bioText);
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.TextDisabled("No bio provided.");
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextDisabled("Preferred Games");
        if (data.PreferredGames.Count > 0)
            GamePill.DrawList(data.PreferredGames, contentWidth);
        else
            ImGui.TextDisabled("None listed.");

        if (drawCloseButton)
        {
            ImGuiHelpers.ScaledDummy(10f);
            if (ImGui.Button("Close##profilePopupClose"))
                ImGui.CloseCurrentPopup();
        }

        var p0 = ImGui.GetWindowPos();
        DrawCardBackground(dl, p0, ImGui.GetWindowSize(), rounding, cardEffect, data.DisplayName, accent, clip);
    }

    public static void DrawCardBackground(ImDrawListPtr dl, Vector2 p0, Vector2 sz, float rounding,
        CardEffectType cardEffect, string seedText, Vector4 accent, (Vector2 Min, Vector2 Max)? clip)
    {
        var scale = ImGuiHelpers.GlobalScale;
        dl.ChannelsSetCurrent(0);

        if (clip is { } c)
        {
            var bleed = 2f * scale;
            dl.PushClipRect(new Vector2(p0.X - bleed, c.Min.Y), new Vector2(p0.X + sz.X + bleed, c.Max.Y), false);
        }
        else
        {
            dl.PushClipRect(new Vector2(-10000f, -10000f), new Vector2(100000f, 100000f), false);
        }

        var t = ImGui.GetTime();
        if (cardEffect != CardEffectType.None)
        {
            CardEffectDrawer.DrawFill(dl, cardEffect, p0, p0 + sz, t, CardEffectHelpers.Seed(seedText));
            CardEffectDrawer.DrawBorder(dl, cardEffect, p0, p0 + sz, rounding, t);
        }
        else
        {
            dl.AddRect(p0, p0 + sz, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
                rounding, ImDrawFlags.None, 1.5f * scale);
        }

        dl.PopClipRect();
        dl.ChannelsMerge();
    }

    private static void DrawCentredAvatar(ImageCache imageCache, Data data, IDalamudTextureWrap? tex, float diameter)
    {
        var pos = ImGui.GetCursorScreenPos();
        DrawAvatarAt(ImGui.GetWindowDrawList(), imageCache, pos, diameter, tex, data.BorderStyle, data.Booster);
        ImGui.Dummy(new Vector2(diameter, diameter));
    }

    public static void DrawAvatarAt(ImDrawListPtr dl, ImageCache imageCache, Vector2 pos, float diameter,
        IDalamudTextureWrap? tex, string? borderStyle, bool booster)
    {
        if (tex != null)
            CircleImage.DrawAt(dl, pos, diameter, tex);
        else
            CircleImage.DrawPlaceholderAt(dl, pos, diameter);

        DrawAvatarBorder(dl, imageCache, pos, diameter, borderStyle, booster);
    }

    public static void DrawAvatarBorder(ImDrawListPtr dl, ImageCache imageCache, Vector2 pos, float diameter, string? borderStyle, bool booster)
    {
        var borderPath = AvatarBorder.ImagePath(borderStyle, booster);
        if (borderPath == null) return;

        var ring = imageCache.GetBundledImage(borderPath);
        if (ring == null) return;

        var centre = pos + new Vector2(diameter * 0.5f, diameter * 0.5f);
        var half = diameter * 0.58f;
        dl.AddImage(ring.Handle, centre - new Vector2(half, half), centre + new Vector2(half, half),
            Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
    }

    private static CardEffectType CardEffectFor(Data? data)
        => data != null ? CardEffectResolver.Resolve(data.CardEffectStyle, data.Booster) : CardEffectType.None;


    private static void CentreText(float width, string text, Action draw)
    {
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - size.X) * 0.5f));
        draw();
    }
}
