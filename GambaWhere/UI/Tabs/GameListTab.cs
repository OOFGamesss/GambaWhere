using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Services;
using GambaWhere.Utility;
using static GambaWhere.Utility.GameTypeColours;

namespace GambaWhere.UI.Tabs;

/// <summary>Tab showing the supported games and their details.</summary>
public class GameListTab
{
    private readonly ImageService _imageService;
    private readonly Configuration _config;

    private static readonly float ImageSize = 70f;
    private static readonly float ImageLeftPadding = 8f;

    public GameListTab(ImageService imageService, Configuration config)
    {
        _imageService = imageService;
        _config = config;
    }

    public void Draw()
    {
        ImGui.TextWrapped("Games you can advertise when hosting a session. Companion plugins integrate with Host Gamba for automatic rule capture where supported.");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        for (var i = 0; i < GameCatalog.DisplayGames.Count; i++)
            DrawGameCard(GameCatalog.DisplayGames[i], i);
    }

    private void DrawGameCard(Game entry, int index)
    {
        var title = entry.Category;
        var companionPlugin = entry.CompanionPlugin;

        using var id = ImRaii.PushId(index);

        var scaledImageSize = new Vector2(ImageSize, ImageSize) * ImGuiHelpers.GlobalScale;
        var scaledWebColumnWidth = ImGui.GetFrameHeight() + 12f * ImGuiHelpers.GlobalScale;
        var (bgColor, accentColor) = ForGame(title);
        var downloadUrl = entry.Url;

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var tex = _imageService.GetBundled(entry.IconFile);

        if (ImGui.BeginTable("##gameCard", 3, ImGuiTableFlags.None))
        {
            ImGui.TableSetupColumn("##img", ImGuiTableColumnFlags.WidthFixed, scaledImageSize.X + ImageLeftPadding * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##web", ImGuiTableColumnFlags.WidthFixed, scaledWebColumnWidth);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.Dummy(scaledImageSize);

            ImGui.TableSetColumnIndex(1);

            ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), title);

            ImGuiHelpers.ScaledDummy(2f);

            var description = entry.Description;
            var companionLine = !string.IsNullOrEmpty(companionPlugin)
                ? $"Companion plugin: {companionPlugin}"
                : "No Plugin Available at this time.";

            var companionColWidth = ImGui.CalcTextSize(companionLine).X + ImGui.GetStyle().CellPadding.X;
            var midCellMaxW = ImGui.GetContentRegionAvail().X;
            companionColWidth = Math.Clamp(companionColWidth, 140f * ImGuiHelpers.GlobalScale, midCellMaxW * 0.48f);

            if (ImGui.BeginTable("##descCompanion", 2, ImGuiTableFlags.None))
            {
                ImGui.TableSetupColumn("##desc", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##companion", ImGuiTableColumnFlags.WidthFixed, companionColWidth);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                if (!string.IsNullOrEmpty(description))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
                    ImGui.TextWrapped(description);
                    ImGui.PopStyleColor();
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + companionColWidth);
                if (!string.IsNullOrEmpty(companionPlugin))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, accentColor);
                    ImGui.TextWrapped(companionLine);
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                    ImGui.TextWrapped(companionLine);
                    ImGui.PopStyleColor();
                }

                ImGui.PopTextWrapPos();

                ImGui.EndTable();
            }

            var creatorLine = $"Created by {entry.Creator}";
            if (!string.IsNullOrEmpty(creatorLine))
            {
                ImGuiHelpers.ScaledDummy(2f);
                ImGui.TextDisabled(creatorLine);
            }

            ImGui.TableSetColumnIndex(2);

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                var btnHeight = ImGui.GetFrameHeight();
                var padY = Math.Max(0f, (scaledImageSize.Y - btnHeight) * 0.5f);
                ImGui.Dummy(new Vector2(0f, padY));

                if (ImGuiComponents.IconButton("##openDownload", FontAwesomeIcon.Globe))
                    OpenBrowser.TryOpen(downloadUrl);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Open in browser:\n{downloadUrl}");
            }

            ImGui.EndTable();
        }

        var cardBottomScreen = ImGui.GetCursorScreenPos();

        var rowHeight = cardBottomScreen.Y - cardTopScreen.Y;
        var iconY = cardTopScreen.Y + Math.Max(0f, (rowHeight - scaledImageSize.Y) * 0.5f);
        var iconMin = new Vector2(cardTopScreen.X + ImageLeftPadding * ImGuiHelpers.GlobalScale, iconY);
        var iconMax = iconMin + scaledImageSize;
        if (tex != null)
            drawList.AddImage(tex.Handle, iconMin, iconMax);
        else
            drawList.AddRectFilled(
                iconMin,
                iconMax,
                ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
                4f * ImGuiHelpers.GlobalScale);

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(
            cardTopScreen,
            new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y),
            ImGui.GetColorU32(bgColor),
            4f * ImGuiHelpers.GlobalScale);
        drawList.ChannelsMerge();

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

}
