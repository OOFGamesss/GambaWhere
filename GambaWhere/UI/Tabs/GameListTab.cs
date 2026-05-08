using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Images;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class GameListTab
{
    private readonly ImageCache _imageCache;

    private static readonly float ImageSize = 70f;

    private const string SimpleGambaGamesUrl = "https://simple.gamba.pro/#games";
    private const string OofGamesPluginsRepoUrl = "https://github.com/OOFGamesss/OOFGamesPlugins";

    private static readonly (string DisplayName, string? CompanionPlugin)[] HostableGames =
    {
        ("Bingo", "SimpleBingo"),
        ("Blackjack", "SimpleBlackjack"),
        ("Chocobo Racing", "Chocobo Racing Gamba"),
        ("Mini Games", null),
        ("Poker", "SimplePoker"),
        ("Roulette", "SimpleRoulette"),
        ("Scratchcards", "SimpleScratch"),
        ("Spin the Wheel", "SimpleWheel")
    };

    public GameListTab(ImageCache imageCache)
    {
        _imageCache = imageCache;
    }

    public void Draw()
    {
        ImGui.TextWrapped("Games you can advertise when hosting a session. Companion plugins integrate with Host Gamba for automatic rule capture where supported.");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        foreach (var game in HostableGames)
            DrawGameCard(game.DisplayName, game.CompanionPlugin);
    }

    private static string? GetDownloadUrl(string displayName) => displayName switch
    {
        "Mini Games" => null,
        "Chocobo Racing" => OofGamesPluginsRepoUrl,
        _ => SimpleGambaGamesUrl
    };

    private static string? GetCreatorAttribution(string displayName) => displayName switch
    {
        "Mini Games" => "Created by No One",
        "Chocobo Racing" => "Created by OOF Games",
        _ => "Created by Asuna & Klia"
    };

    private static string? GetGameDescription(string displayName) => displayName switch
    {
        "Bingo" =>
            "Interactive bingo with automated ball calling, multiple card support, and real-time winner detection.",
        "Roulette" =>
            "Classic European roulette with live dealer controls, real-time stats, and spectator chat.",
        "Blackjack" =>
            "Professional blackjack with multiple hands, full game customization, and advanced betting options.",
        "Spin the Wheel" =>
            "Customizable spin to win wheels with variable segments, custom images, and exciting prize distribution options.",
        "Poker" =>
            "Professional Texas Hold'em poker with full customization and immersive table management.",
        "Scratchcards" =>
            "Fully customizable scratcher game with configurable prizes, adjustable odds, and custom images.",
        "Chocobo Racing" =>
            "Fully Customisable racing game with auto detected bets, bank management and much more.",
        "Mini Games" =>
            "Casual mini bar-style games designed for quick rounds, simple interactions, and social-friendly gameplay.",
        _ => null
    };

    private static string? GetBundledIconFileName(string displayName) => displayName switch
    {
        "Bingo" => "simplebingo.png",
        "Blackjack" => "simpleblackjack.png",
        "Chocobo Racing" => "chocoboracinggamba.png",
        "Mini Games" => "minigames.png",
        "Poker" => "simplepoker.png",
        "Roulette" => "simpleroulette.png",
        "Scratchcards" => "simplescratch.png",
        "Spin the Wheel" => "simplewheel.png",
        _ => null
    };

    private void DrawGameCard(string displayName, string? companionPlugin)
    {
        using var id = ImRaii.PushId(displayName);

        var scaledImageSize = new Vector2(ImageSize, ImageSize) * ImGuiHelpers.GlobalScale;
        var scaledWebColumnWidth = ImGui.GetFrameHeight() + 12f * ImGuiHelpers.GlobalScale;
        var (bgColor, accentColor) = GetGameTypeColors(displayName);
        var downloadUrl = GetDownloadUrl(displayName);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        if (ImGui.BeginTable("##gameCard", 3, ImGuiTableFlags.None))
        {
            ImGui.TableSetupColumn("##img", ImGuiTableColumnFlags.WidthFixed, scaledImageSize.X);
            ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##web", ImGuiTableColumnFlags.WidthFixed, scaledWebColumnWidth);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var iconFile = GetBundledIconFileName(displayName);
            var tex = iconFile != null ? _imageCache.GetBundledPng(iconFile) : null;
            if (tex != null)
                ImGui.Image(tex.Handle, scaledImageSize);
            else
                DrawImagePlaceholder(scaledImageSize);

            ImGui.TableSetColumnIndex(1);

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), displayName);

            ImGuiHelpers.ScaledDummy(2f);

            var description = GetGameDescription(displayName);
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

            var creatorLine = GetCreatorAttribution(displayName);
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

    private static void DrawImagePlaceholder(Vector2 size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            pos,
            pos + size,
            ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
            4f * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(size);
    }

    private static (Vector4 bg, Vector4 text) GetGameTypeColors(string gameType) => gameType switch
    {
        "Bingo" => (new Vector4(0.85f, 0.25f, 0.25f, 0.18f), new Vector4(1.00f, 0.50f, 0.50f, 1f)),
        "Blackjack" => (new Vector4(0.25f, 0.50f, 0.90f, 0.18f), new Vector4(0.50f, 0.75f, 1.00f, 1f)),
        "Chocobo Racing" => (new Vector4(0.85f, 0.80f, 0.15f, 0.18f), new Vector4(1.00f, 0.95f, 0.30f, 1f)),
        "Mini Games" => (new Vector4(0.20f, 0.80f, 0.40f, 0.18f), new Vector4(0.40f, 1.00f, 0.55f, 1f)),
        "Poker" => (new Vector4(0.00f, 0.80f, 0.80f, 0.18f), new Vector4(0.00f, 1.00f, 1.00f, 1f)),
        "Roulette" => (new Vector4(0.52f, 0.38f, 0.78f, 0.18f), new Vector4(0.82f, 0.68f, 1.00f, 1f)),
        "Scratchcards" => (new Vector4(0.85f, 0.45f, 0.00f, 0.18f), new Vector4(1.00f, 0.60f, 0.00f, 1f)),
        "Spin the Wheel" => (new Vector4(0.90f, 0.60f, 0.70f, 0.18f), new Vector4(1.00f, 0.75f, 0.85f, 1f)),
        _ => (new Vector4(0.50f, 0.50f, 0.50f, 0.12f), new Vector4(0.75f, 0.75f, 0.75f, 1f)),
    };
}
