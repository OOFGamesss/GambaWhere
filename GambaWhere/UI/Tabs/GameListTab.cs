using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Models;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Game store tab showing the approved games from the API as searchable, filterable full width cards.</summary>
public class GameListTab
{
    private const float FilterWidth = 130f;
    private const float ImageSize = 80f;
    private const float ImagePadding = 8f;

    private const string WarningHeading = "Third Party Software";
    private const string WarningBody =
        "These games are made by third party developers and are not reviewed or vetted by OOF Games. "
        + "Install and use them at your own risk. OOF Games accepts no responsibility for any harm or "
        + "damage caused by third party software.";

    private static readonly Vector4 WarningText = new(1f, 0.65f, 0.25f, 1f);
    private static readonly Vector4 WarningYellow = new(1f, 0.83f, 0.25f, 1f);

    private readonly ImageService _imageService;
    private readonly Configuration _config;
    private readonly GameStoreService _store;

    private string _search = string.Empty;
    private readonly HashSet<string> _selectedCategories = new();
    private readonly HashSet<string> _selectedAuthors = new();

    private GameStoreGame? _installGame;
    private bool _openInstallRequested;
    private long _copiedAtTick;

    public GameListTab(ImageService imageService, Configuration config, GameStoreService store)
    {
        _imageService = imageService;
        _config = config;
        _store = store;
    }

    public void OnSelected() => _store.RequestRefresh();

    public void Draw()
    {
        DrawThirdPartyWarning();
        DrawHeader();
        DrawStatusLine();
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        DrawCards();
        DrawInstallPopup();
    }

    private void DrawThirdPartyWarning()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 4f * scale;
        var pad = 10f * scale;
        var availWidth = ImGui.GetContentRegionAvail().X;
        var topScreen = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var startX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + pad);

        var icon = FontAwesomeIcon.ExclamationTriangle.ToIconString();
        float iconWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconWidth = ImGui.CalcTextSize(icon).X;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var headingWidth = ImGui.CalcTextSize(WarningHeading).X + (iconWidth + spacing) * 2f;

        ImGui.SetCursorPosX(startX + Math.Max(pad, (availWidth - headingWidth) * 0.5f));
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(WarningYellow, icon);
        ImGui.SameLine();
        ImGui.TextColored(WarningYellow, WarningHeading);
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(WarningYellow, icon);

        ImGuiHelpers.ScaledDummy(2f);
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(spacing, 1f * scale)))
        {
            foreach (var line in WrapLines(WarningBody, availWidth - pad * 2f))
            {
                ImGui.SetCursorPosX(startX + Math.Max(pad, (availWidth - ImGui.CalcTextSize(line).X) * 0.5f));
                ImGui.TextUnformatted(line);
            }
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + pad);

        var bottomScreen = ImGui.GetCursorScreenPos();
        var boxMax = new Vector2(topScreen.X + availWidth, bottomScreen.Y);

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(topScreen, boxMax,
            ImGui.GetColorU32(new Vector4(WarningYellow.X, WarningYellow.Y, WarningYellow.Z, 0.10f)), rounding);
        drawList.ChannelsMerge();

        drawList.AddRect(topScreen, boxMax,
            ImGui.GetColorU32(new Vector4(WarningYellow.X, WarningYellow.Y, WarningYellow.Z, 0.55f)),
            rounding, ImDrawFlags.None, 1.5f * scale);

        ImGuiHelpers.ScaledDummy(6f);
    }

    private void DrawHeader()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var filtersWidth = FilterWidth * 2f * scale + spacing;
        var searchWidth = Math.Max(120f * scale, ImGui.GetContentRegionAvail().X - filtersWidth - spacing);

        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputTextWithHint("##storeSearch", "Search games...", ref _search, 128);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(FilterWidth * scale);
        MultiSelectCombo.Draw("##storeCategoryFilter", "Category", CategoryOptions(), _selectedCategories);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by category");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(FilterWidth * scale);
        MultiSelectCombo.Draw("##storeAuthorFilter", "Author", AuthorOptions(), _selectedAuthors);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by author");
    }

    private void DrawStatusLine()
    {
        if (_store.IsRefreshing || !_store.LastRefreshFailed)
            return;

        var updated = _store.LastUpdatedUtc;
        ImGui.TextColored(WarningText, updated != null
            ? $"Game store unreachable. Showing games saved {FormatAgo(updated.Value)}."
            : "Game store unreachable.");
    }

    private void DrawCards()
    {
        if (_store.Games.Count == 0)
        {
            if (_store.IsRefreshing)
                ImGui.TextDisabled("Loading games...");
            else if (_store.LastUpdatedUtc != null && !_store.LastRefreshFailed)
                ImGui.TextDisabled("The game store has no games listed yet.");
            else
                ImGui.TextDisabled("The game store is unreachable and no games have been saved yet.");
            return;
        }

        var filtered = _store.Games.Where(Matches).ToList();
        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("No games match your search.");
            return;
        }

        foreach (var game in filtered)
        {
            DrawCard(game);
            ImGuiHelpers.ScaledDummy(6f);
        }
    }

    private bool Matches(GameStoreGame game)
    {
        if (_selectedCategories.Count > 0 && !_selectedCategories.Contains(game.GameType))
            return false;

        if (_selectedAuthors.Count > 0 && !SplitAuthors(game.Authors).Any(_selectedAuthors.Contains))
            return false;

        if (string.IsNullOrWhiteSpace(_search))
            return true;

        var term = _search.Trim();
        return ContainsTerm(game.Name, term)
            || ContainsTerm(game.GameType, term)
            || ContainsTerm(game.Authors, term)
            || ContainsTerm(game.Description, term)
            || ContainsTerm(game.DeveloperName, term);
    }

    private void DrawCard(GameStoreGame game)
    {
        using var id = ImRaii.PushId(game.Id);

        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 4f * scale;
        var scaledImageSize = new Vector2(ImageSize, ImageSize) * scale;
        var (background, accent) = GameTypeColours.ForGame(game.GameType);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGuiHelpers.ScaledDummy(4f);

        using (var table = ImRaii.Table("##storeCard", 2, ImGuiTableFlags.None))
        {
            if (table)
            {
                ImGui.TableSetupColumn("##img", ImGuiTableColumnFlags.WidthFixed,
                    scaledImageSize.X + ImagePadding * 2f * scale);
                ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Dummy(scaledImageSize);

                ImGui.TableSetColumnIndex(1);
                DrawCardText(game, accent);
            }
        }

        ImGuiHelpers.ScaledDummy(4f);

        var cardBottomScreen = ImGui.GetCursorScreenPos();

        var rowHeight = cardBottomScreen.Y - cardTopScreen.Y;
        var imageTop = cardTopScreen.Y + Math.Max(0f, (rowHeight - scaledImageSize.Y) * 0.5f);
        var imageMin = new Vector2(cardTopScreen.X + ImagePadding * scale, imageTop);
        DrawCardImage(game, drawList, imageMin, scaledImageSize);

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(
            cardTopScreen,
            new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y),
            ImGui.GetColorU32(background),
            rounding);
        drawList.ChannelsMerge();

        drawList.AddRect(
            cardTopScreen,
            new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y),
            ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
            rounding,
            ImDrawFlags.None,
            1.5f * scale);
    }

    private void DrawCardImage(GameStoreGame game, ImDrawListPtr drawList, Vector2 boxMin, Vector2 boxSize)
    {
        var tex = string.IsNullOrWhiteSpace(game.ImageUrl) ? null : _imageService.GetFromUrlTrimmed(game.ImageUrl);
        if (tex == null || tex.Width <= 0 || tex.Height <= 0)
        {
            drawList.AddRectFilled(
                boxMin,
                boxMin + boxSize,
                ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
                4f * ImGuiHelpers.GlobalScale);
            return;
        }

        var textureRatio = tex.Width / (float)tex.Height;
        var boxRatio = boxSize.X / boxSize.Y;
        var drawSize = textureRatio > boxRatio
            ? new Vector2(boxSize.X, boxSize.X / textureRatio)
            : new Vector2(boxSize.Y * textureRatio, boxSize.Y);

        var drawMin = boxMin + (boxSize - drawSize) * 0.5f;
        drawList.AddImage(tex.Handle, drawMin, drawMin + drawSize);
    }

    private void DrawCardText(GameStoreGame game, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cellRightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 12f * scale;

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), game.Name);
        var afterNameY = ImGui.GetCursorPosY();

        var gameTypeX = cellRightEdge - ImGui.CalcTextSize(game.GameType).X;
        if (gameTypeX > ImGui.GetCursorPosX())
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(gameTypeX);
            ImGui.TextColored(accent, game.GameType);
            ImGui.SetCursorPosY(afterNameY);
        }
        else
        {
            ImGui.TextColored(accent, game.GameType);
        }

        ImGui.TextDisabled($"by {game.Authors}");

        if (!string.IsNullOrWhiteSpace(game.Description))
        {
            ImGuiHelpers.ScaledDummy(2f);
            ImGui.PushTextWrapPos(cellRightEdge);
            ImGui.TextWrapped(game.Description);
            ImGui.PopTextWrapPos();
        }

        ImGuiHelpers.ScaledDummy(4f);
        DrawCardFooter(game, accent, cellRightEdge);
    }

    private void DrawCardFooter(GameStoreGame game, Vector4 accent, float cellRightEdge)
    {
        var players = PlayerCountLabel(game);
        if (players != null)
        {
            DrawPlayerBadge(players, accent);
            ImGui.SameLine();
        }

        var buttons = CardButtons(game);
        if (buttons.Count == 0)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = buttons.Sum(b => b.Icon is { } icon ? IconButtonWidth(icon) : ImGui.GetFrameHeight())
            + spacing * (buttons.Count - 1);
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), cellRightEdge - totalWidth));

        for (var i = 0; i < buttons.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            if (buttons[i].Icon is { } buttonIcon)
            {
                if (ImGuiComponents.IconButton($"##storeLink{i}", buttonIcon))
                    buttons[i].OnClick();
            }
            else
            {
                DrawDiscordButton($"##storeLink{i}", buttons[i].OnClick);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(buttons[i].Tooltip);
        }
    }

    private List<(FontAwesomeIcon? Icon, string Tooltip, Action OnClick)> CardButtons(GameStoreGame game)
    {
        var buttons = new List<(FontAwesomeIcon?, string, Action)>();

        if (!string.IsNullOrWhiteSpace(game.RepoUrl))
            buttons.Add((FontAwesomeIcon.Download, "How to install", () =>
            {
                _installGame = game;
                _openInstallRequested = true;
            }));
        if (!string.IsNullOrWhiteSpace(game.InfoUrl))
            buttons.Add((FontAwesomeIcon.InfoCircle, $"More information\n{game.InfoUrl}", () => OpenBrowser.TryOpen(game.InfoUrl!)));
        if (!string.IsNullOrWhiteSpace(game.DiscordInviteUrl))
            buttons.Add((null, $"Join the Discord\n{game.DiscordInviteUrl}", () => OpenBrowser.TryOpen(game.DiscordInviteUrl!)));

        return buttons;
    }

    private void DrawDiscordButton(string id, Action onClick)
    {
        var tex = _imageService.GetBundled("Icons/discordlogo.png");
        if (tex == null)
        {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.Comments))
                onClick();
            return;
        }

        var size = new Vector2(ImGui.GetFrameHeight());
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton(id, size))
            onClick();

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var alpha = ImGui.IsItemHovered() ? 1f : 0.85f;
        ImGui.GetWindowDrawList().AddImage(
            tex.Handle,
            pos,
            pos + size,
            Vector2.Zero,
            Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private void DrawInstallPopup()
    {
        if (_openInstallRequested)
        {
            ImGui.OpenPopup($"Install {_installGame!.Name}###storeInstallPopup");
            _openInstallRequested = false;
        }

        if (_installGame == null)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var open = true;
        using var modal = ImRaii.PopupModal($"Install {_installGame.Name}###storeInstallPopup", ref open,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            if (!open)
                _installGame = null;
            return;
        }

        var game = _installGame;
        var wrapWidth = 460f * scale;

        ImGui.PushTextWrapPos(wrapWidth);
        ImGui.TextWrapped("1. Type /xlsettings in the in-game chat.");
        ImGui.TextWrapped("2. Navigate to the Experimental tab.");
        ImGui.TextWrapped("3. Paste the following link into the Custom Plugin Repositories section:");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), game.RepoUrl);
        ImGui.SameLine();

        var copied = Environment.TickCount64 - _copiedAtTick < 2000;
        if (UIHelper.IconTextButton(copied ? FontAwesomeIcon.Check : FontAwesomeIcon.Copy, copied ? "Copied" : "Copy Link", "##copyRepo"))
        {
            ImGui.SetClipboardText(game.RepoUrl);
            _copiedAtTick = Environment.TickCount64;
        }

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.PushTextWrapPos(wrapWidth);
        ImGui.TextWrapped("4. Click the + button, ensure the repository is Enabled, and click Save and Close.");
        ImGui.TextWrapped($"5. Type /xlplugins, search for \"{game.Name}\", and click Install.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2f);

        var closeWidth = 120f * scale;
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - closeWidth) * 0.5f);
        if (ImGui.Button("Close", new Vector2(closeWidth, 0f)))
        {
            _installGame = null;
            ImGui.CloseCurrentPopup();
        }
    }

    private static void DrawPlayerBadge(string text, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(textSize.X + 16f * scale, ImGui.GetFrameHeight());
        var pos = ImGui.GetCursorScreenPos();
        var rounding = size.Y * 0.5f;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(pos, pos + size,
            ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.18f)), rounding);
        drawList.AddRect(pos, pos + size,
            ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)), rounding, ImDrawFlags.None, 1f * scale);
        drawList.AddText(pos + new Vector2(8f * scale, (size.Y - textSize.Y) * 0.5f),
            ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.95f)), text);

        ImGui.Dummy(size);
    }

    private static string? PlayerCountLabel(GameStoreGame game)
    {
        if (game.PlayersOver24)
            return "24+ players";
        if (game.MaxPlayers is > 0)
            return $"Up to {game.MaxPlayers} players";
        return null;
    }

    private IReadOnlyList<string> CategoryOptions() =>
        _store.Games
            .Select(g => g.GameType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IReadOnlyList<string> AuthorOptions() =>
        _store.Games
            .SelectMany(g => SplitAuthors(g.Authors))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> WrapLines(string text, float maxWidth)
    {
        var line = string.Empty;
        foreach (var word in text.Split(' '))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && ImGui.CalcTextSize(candidate).X > maxWidth)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            yield return line;
    }

    private static IEnumerable<string> SplitAuthors(string authors) =>
        authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ContainsTerm(string? text, string term) =>
        !string.IsNullOrEmpty(text) && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static float IconButtonWidth(FontAwesomeIcon icon)
    {
        float iconWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;

        return iconWidth + ImGui.GetStyle().FramePadding.X * 2f;
    }

    private static string FormatAgo(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span < TimeSpan.FromMinutes(1))
            return "just now";
        if (span < TimeSpan.FromHours(1))
            return $"{(int)span.TotalMinutes} min ago";
        if (span < TimeSpan.FromHours(48))
            return $"{(int)span.TotalHours} hr ago";
        return $"{(int)span.TotalDays} days ago";
    }
}
