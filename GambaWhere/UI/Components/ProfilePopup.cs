using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// The shared "View Profile" popup, used by both Gamba Events and recruitment Find-a-Host cards.
/// Styled like the Game Info popup: a bordered card (holographic foil for boosters) with the
/// profile picture centred at the top, the name beneath it, then bio and preferred games.
/// </summary>
internal static class ProfilePopup
{
    public sealed class Data
    {
        public string DisplayName = string.Empty;
        public string? ProfileImageUrl;
        public string? Bio;
        public List<string> PreferredGames = new();
        public bool Booster;
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
        var booster = data?.Booster ?? false;
        var cardBg = booster ? BoosterCardEffect.BaseColour : ThemeColours.TintedWindowBg(config.PrimaryColour);

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

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var accent = config.SecondaryColour;
        var width = ImGui.GetContentRegionAvail().X;

        ImGuiHelpers.ScaledDummy(6f);
        var diameter = 104f * scale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - diameter) * 0.5f));
        DrawCentredAvatar(imageCache, data, diameter);

        ImGuiHelpers.ScaledDummy(6f);

        CentreText(width, data.DisplayName, () => ImGui.TextColored(accent, data.DisplayName));
        if (booster)
            CentreText(width, "Booster", () => ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.78f, 1f), "Booster"));

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        var contentWidth = ImGui.GetContentRegionAvail().X;

        ImGui.TextDisabled("Bio");
        if (!string.IsNullOrWhiteSpace(data.Bio))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
            ImGui.TextWrapped(data.Bio);
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

        ImGuiHelpers.ScaledDummy(10f);
        if (ImGui.Button("Close##profilePopupClose"))
            ImGui.CloseCurrentPopup();

        var p0 = ImGui.GetWindowPos();
        var sz = ImGui.GetWindowSize();
        dl.ChannelsSetCurrent(0);
        dl.PushClipRect(new Vector2(-10000f, -10000f), new Vector2(100000f, 100000f), false);
        if (booster)
        {
            BoosterCardEffect.DrawHolographicFill(dl, p0, p0 + sz, ImGui.GetTime(), BoosterCardEffect.Seed(data.DisplayName));
            BoosterCardEffect.DrawHolographicBorder(dl, p0, p0 + sz, rounding, ImGui.GetTime());
        }
        else
        {
            dl.AddRect(p0, p0 + sz, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
                rounding, ImDrawFlags.None, 1.5f * scale);
        }
        dl.PopClipRect();
        dl.ChannelsMerge();
    }

    private static void DrawCentredAvatar(ImageCache imageCache, Data data, float diameter)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        var tex = !string.IsNullOrWhiteSpace(data.ProfileImageUrl) ? imageCache.GetProfile(data.ProfileImageUrl!) : null;
        if (tex != null)
            CircleImage.DrawAt(dl, pos, diameter, tex);
        else
            CircleImage.DrawPlaceholderAt(dl, pos, diameter);

        if (data.Booster)
        {
            var ring = imageCache.GetBundledImage("boosterborder.png");
            if (ring != null)
            {
                var centre = pos + new Vector2(diameter * 0.5f, diameter * 0.5f);
                var half = diameter * 0.58f;
                dl.AddImage(ring.Handle, centre - new Vector2(half, half), centre + new Vector2(half, half),
                    Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
            }
        }

        ImGui.Dummy(new Vector2(diameter, diameter));
    }

    private static void CentreText(float width, string text, Action draw)
    {
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - size.X) * 0.5f));
        draw();
    }
}
