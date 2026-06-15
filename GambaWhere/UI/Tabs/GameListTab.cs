using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Utility;
using static GambaWhere.Utility.GameTypeColours;

namespace GambaWhere.UI.Tabs;

/// <summary>Tab showing the supported games and their details.</summary>
public class GameListTab
{
    private readonly ImageCache _imageCache;
    private readonly Configuration _config;

    private static readonly float ImageSize = 70f;
    private static readonly float ImageLeftPadding = 8f;

    private const string SimpleGambaGamesUrl = "https://simple.gamba.pro/#games";
    private const string OofGamesPluginsRepoUrl = "https://oofgames.fyi";
    private const string AsunaMiniGamesRepoUrl = "https://puni.sh/api/repository/asuna";

    private readonly record struct HostableGame(string Key, string Title, string? CompanionPlugin);

    private static readonly HostableGame[] HostableGames =
    {
        new("Bingo", "Bingo", "SimpleBingo"),
        new("Blackjack", "Blackjack", "SimpleBlackjack"),
        new("Chocobo Racing", "Chocobo Racing", "Chocobo Racing"),
        new("Mini Games", "Mini Games", "Mini Games Emporium"),
        new("Simple Mini Games", "Mini Games", "SimpleMiniGames"),
        new("Poker", "Poker", "SimplePoker"),
        new("Roulette", "Roulette", "SimpleRoulette"),
        new("Scratchcards", "Scratchcards", "SimpleScratch"),
        new("Spin the Wheel", "Spin the Wheel", "SimpleWheel")
    };

    public GameListTab(ImageCache imageCache, Configuration config)
    {
        _imageCache = imageCache;
        _config = config;
    }

    public void Draw()
    {
        ImGui.TextWrapped("Games you can advertise when hosting a session. Companion plugins integrate with Host Gamba for automatic rule capture where supported.");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        foreach (var game in HostableGames)
            DrawGameCard(game);
    }

    private static string? GetDownloadUrl(string key) => key switch
    {
        "Mini Games" => OofGamesPluginsRepoUrl,
        "Chocobo Racing" => OofGamesPluginsRepoUrl,
        "Simple Mini Games" => AsunaMiniGamesRepoUrl,
        _ => SimpleGambaGamesUrl
    };

    private static string? GetCreatorAttribution(string key) => key switch
    {
        "Mini Games" => "Created by OOF Games",
        "Chocobo Racing" => "Created by OOF Games",
        _ => "Created by Asuna & Klia"
    };

    private static string? GetGameDescription(string key) => key switch
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
        "Simple Mini Games" =>
            "DICE! GAMES! UNO! Cards Against Humanity",
        _ => null
    };

    private static string? GetBundledIconFileName(string key) => key switch
    {
        "Bingo" => "Games/simplebingo.png",
        "Blackjack" => "Games/simpleblackjack.png",
        "Chocobo Racing" => "Games/chocoboracinggamba.png",
        "Mini Games" => "Games/minigamesemporium.png",
        "Simple Mini Games" => "Games/simpleminigames.png",
        "Poker" => "Games/simplepoker.png",
        "Roulette" => "Games/simpleroulette.png",
        "Scratchcards" => "Games/simplescratch.png",
        "Spin the Wheel" => "Games/simplewheel.png",
        _ => null
    };

    private void DrawGameCard(HostableGame game)
    {
        var key = game.Key;
        var title = game.Title;
        var companionPlugin = game.CompanionPlugin;

        using var id = ImRaii.PushId(key);

        var scaledImageSize = new Vector2(ImageSize, ImageSize) * ImGuiHelpers.GlobalScale;
        var scaledWebColumnWidth = ImGui.GetFrameHeight() + 12f * ImGuiHelpers.GlobalScale;
        var (bgColor, accentColor) = ForGame(title);
        var downloadUrl = GetDownloadUrl(key);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var iconFile = GetBundledIconFileName(key);
        var tex = iconFile != null ? _imageCache.GetBundledPng(iconFile) : null;

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

            var description = GetGameDescription(key);
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

            var creatorLine = GetCreatorAttribution(key);
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
