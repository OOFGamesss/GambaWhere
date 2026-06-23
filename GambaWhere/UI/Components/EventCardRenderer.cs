using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Services;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>Reusable renderer for an event card in the events list.</summary>
public static class EventCardRenderer
{
    private static readonly float ImageSize = 70f;

    public static void DrawPreviewCard(
        string characterName,
        string gameType,
        string venueName,
        string description,
        Dictionary<string, object> rules,
        ImageService imageService)
    {
        var scaledImageSize = new Vector2(ImageSize, ImageSize) * ImGuiHelpers.GlobalScale;
        var (bgColor, gameTypeTextColor) = GetGameTypeColors(gameType);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        {
            using var table = ImRaii.Table("##previewCard", 2, ImGuiTableFlags.None);
            if (table)
            {
                ImGui.TableSetupColumn("##img", ImGuiTableColumnFlags.WidthFixed, scaledImageSize.X);
                ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                var logo = imageService.GetBundled("Icons/gambawhere.png");
                if (logo != null)
                    ImGui.Image(logo.Handle, scaledImageSize);
                else
                    DrawImagePlaceholder(scaledImageSize);

                ImGui.TableSetColumnIndex(1);
                var cellRightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
                var rowTopY = ImGui.GetCursorPosY();

                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), characterName);
                var afterNameY = ImGui.GetCursorPosY();

                var gameTypeWidth = ImGui.CalcTextSize(gameType).X;
                var gameTypeX = cellRightEdge - gameTypeWidth - 12f * ImGuiHelpers.GlobalScale;
                var gameCentreY = rowTopY + (scaledImageSize.Y - ImGui.GetTextLineHeight()) / 2f;

                if (gameTypeX > ImGui.GetCursorPosX())
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPos(new Vector2(gameTypeX, gameCentreY));
                    ImGui.TextColored(gameTypeTextColor, gameType);
                    ImGui.SetCursorPosY(afterNameY);
                }
                else
                {
                    ImGui.TextColored(gameTypeTextColor, gameType);
                }

                if (!string.IsNullOrWhiteSpace(venueName) && venueName != "No Venue")
                    ImGui.TextDisabled($"@ {venueName}");

                if (!string.IsNullOrWhiteSpace(description))
                {
                    ImGuiHelpers.ScaledDummy(2f);
                    ImGui.TextWrapped(description);
                }

                DrawPreviewExpandedDetails(rules);
                ImGuiHelpers.ScaledDummy(4f);
            }
        }

        var cardBottomScreen = ImGui.GetCursorScreenPos();
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(
            cardTopScreen,
            new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y),
            ImGui.GetColorU32(bgColor),
            4f * ImGuiHelpers.GlobalScale);
        drawList.ChannelsMerge();
    }

    private static void DrawPreviewExpandedDetails(Dictionary<string, object> rules)
    {
        using var table = ImRaii.Table("##previewDetails", 2, ImGuiTableFlags.None);
        if (!table)
            return;

        ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);

        if (rules.Count > 0)
        {
            var availForRules = ImGui.GetContentRegionAvail().X;
            var rawKeyWidth = rules.Keys.Max(k => ImGui.CalcTextSize(RuleKeyFormatting.FormatDisplayKey(k)).X) + 12f * ImGuiHelpers.GlobalScale;
            var minKeyWidth = 60f * ImGuiHelpers.GlobalScale;
            var keyColWidth = Math.Min(rawKeyWidth, Math.Max(availForRules * 0.55f, minKeyWidth));

            using var rulesTable = ImRaii.Table("##previewRules", 2, ImGuiTableFlags.None);
            if (rulesTable)
            {
                ImGui.TableSetupColumn("##rk", ImGuiTableColumnFlags.WidthFixed, keyColWidth);
                ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthStretch);

                var disabledColour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
                foreach (var rule in rules)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    using (ImRaii.PushColor(ImGuiCol.Text, disabledColour))
                        ImGui.TextWrapped(RuleKeyFormatting.FormatDisplayKey(rule.Key));
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextWrapped(FormatRuleValue(rule.Value, rule.Key));
                }
            }
        }

        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("Location");
        ImGui.SameLine();
        ImGui.TextWrapped("Your current location");

        using (ImRaii.Disabled(true))
            ImGui.SmallButton("Teleport##previewTeleport");

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Teleport is available once the session is live.");
    }

    public static void DrawImagePlaceholder(Vector2 size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
            4f * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(size);
    }

    public static string FormatDisplayName(string characterName)
    {
        var lastSpace = characterName.LastIndexOf(' ');
        return lastSpace > 0
            ? $"{characterName[..lastSpace]}@{characterName[(lastSpace + 1)..]}"
            : characterName;
    }

    public static void DrawBreakBadge()
    {
        ImGuiHelpers.ScaledDummy(4f);

        const string badgeText = "On Break";
        var textSize = ImGui.CalcTextSize(badgeText);
        var hPad = 6f * ImGuiHelpers.GlobalScale;
        var vPad = 3f * ImGuiHelpers.GlobalScale;
        var badgeWidth = textSize.X + hPad * 2f;
        var badgeHeight = textSize.Y + vPad * 2f;

        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            pos,
            pos + new Vector2(badgeWidth, badgeHeight),
            ImGui.GetColorU32(new Vector4(0.85f, 0.55f, 0.05f, 0.92f)),
            3f * ImGuiHelpers.GlobalScale);
        ImGui.GetWindowDrawList().AddText(
            pos + new Vector2(hPad, vPad),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)),
            badgeText);

        ImGui.Dummy(new Vector2(badgeWidth, badgeHeight));
        ImGuiHelpers.ScaledDummy(2f);
    }

    public static (Vector4 bg, Vector4 text) GetGameTypeColors(string gameType) =>
        GameTypeColours.ForGame(gameType);

    public static string FormatRuleValue(object? value, string key = "")
    {
        var isOdds = key.Contains("odds", StringComparison.OrdinalIgnoreCase);

        string formatted;
        if (value is JsonElement el)
        {
            formatted = el.ValueKind switch
            {
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Number when el.TryGetInt64(out var i) => i.ToString("N0"),
                JsonValueKind.Number when el.TryGetDouble(out var d) => d.ToString("N2"),
                _ => el.ToString()
            };
        }
        else
        {
            formatted = value switch
            {
                bool b => b ? "Yes" : "No",
                int i  => i.ToString("N0"),
                long l => l.ToString("N0"),
                float f  => isOdds ? f.ToString("N2") : f.ToString("N0"),
                double d => isOdds ? d.ToString("N2") : d.ToString("N0"),
                _ => value?.ToString() ?? "-"
            };
        }

        var result = isOdds ? formatted + "x" : formatted;
        if (ShouldAppendGilSuffix(key) && TryGetWholeRuleNumber(value, out var whole) && whole > 1000)
            result += " gil";

        return result;
    }

    private static bool ShouldAppendGilSuffix(string key) =>
        !key.Contains("odds", StringComparison.OrdinalIgnoreCase)
        && !key.Equals("playerCount", StringComparison.OrdinalIgnoreCase)
        && !key.Equals("cardsSold", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetWholeRuleNumber(object? value, out long whole)
    {
        whole = 0;
        switch (value)
        {
            case int i:
                whole = i;
                return true;
            case long l:
                whole = l;
                return true;
            case float f when Math.Abs(f - MathF.Round(f)) < 0.0001f:
                whole = (long)MathF.Round(f);
                return true;
            case double d when Math.Abs(d - Math.Round(d)) < 0.0000001:
                whole = (long)Math.Round(d);
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.Number:
                if (el.TryGetInt64(out var li))
                {
                    whole = li;
                    return true;
                }
                if (el.TryGetDouble(out var du) && Math.Abs(du - Math.Round(du)) < 0.0000001)
                {
                    whole = (long)Math.Round(du);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }
}
