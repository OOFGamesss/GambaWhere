using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Images;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class GambaEventsTab
{
    private readonly GambaWhereClient _client;
    private readonly ImageCache _imageCache;

    private List<EventResponse> _events = new();
    private DateTime? _lastUpdated;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;

    private readonly HashSet<string> _expandedCards = new();
    private readonly HashSet<string> _selectedGameTypes = new();
    private readonly HashSet<string> _selectedDataCentres = new();

    private static readonly string[] KnownGameTypes =
        { "Bingo", "Blackjack", "Chocobo Racing", "Mini Games", "Unknown" };

    private static readonly string[] KnownDataCentres =
    {
        "Aether", "Crystal", "Dynamis", "Primal",
        "Chaos", "Light",
        "Elemental", "Gaia", "Mana", "Meteor",
        "Materia"
    };

    private static readonly float ImageSize = 70f;

    public GambaEventsTab(GambaWhereClient client, ImageCache imageCache)
    {
        _client = client;
        _imageCache = imageCache;
    }

    public void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        DrawEventList();
    }

    private void DrawHeader()
    {
        if (_isRefreshing)
            ImGui.BeginDisabled();

        if (ImGui.Button(_isRefreshing ? "Refreshing..." : "Refresh"))
            TriggerRefresh();

        if (_isRefreshing)
            ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled(_lastUpdated.HasValue
            ? $"Last updated: {_lastUpdated.Value:HH:mm:ss}"
            : "Press Refresh to load events.");

        DrawFilters();
    }

    private void DrawFilters()
    {
        const float FilterWidth = 140f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = FilterWidth * 2 * ImGuiHelpers.GlobalScale + spacing;
        var rightEdge = ImGui.GetContentRegionMax().X;
        var filtersStart = rightEdge - totalWidth;

        ImGui.SameLine(filtersStart);
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        MultiSelectCombo.Draw("##gameTypeFilter", "Game Type", KnownGameTypes, _selectedGameTypes);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by game type");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        MultiSelectCombo.Draw("##dataCentreFilter", "Data Centre", KnownDataCentres, _selectedDataCentres);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by data centre");
    }

    private void TriggerRefresh()
    {
        _isRefreshing = true;
        _ = Task.Run(async () =>
        {
            var results = await _client.GetEventsAsync();
            if (results == null)
            {
                _events = new List<EventResponse>();
                _lastRefreshFailed = true;
            }
            else
            {
                _events = new List<EventResponse>(results);
                _lastRefreshFailed = false;
            }
            _lastUpdated = DateTime.Now;
            _isRefreshing = false;
        });
    }

    private void DrawEventList()
    {
        if (_lastRefreshFailed)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Server error fetching events, please try again later.");
            return;
        }

        if (_events.Count == 0)
        {
            ImGui.TextDisabled(_lastUpdated.HasValue
                ? "No active events found."
                : "No events loaded yet - press Refresh above.");
            return;
        }

        var filtered = GetFilteredEvents();

        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("No events match the current filters.");
            return;
        }

        foreach (var ev in filtered)
            DrawEventCard(ev);
    }

    private List<EventResponse> GetFilteredEvents()
    {
        var query = _events.AsEnumerable();

        if (_selectedGameTypes.Count > 0)
            query = query.Where(ev => _selectedGameTypes.Contains(ev.Game));

        if (_selectedDataCentres.Count > 0)
            query = query.Where(ev => _selectedDataCentres.Contains(InferDataCentre(ev.Location)));

        return query.ToList();
    }

    private static string InferDataCentre(string location)
    {
        foreach (var dc in KnownDataCentres)
        {
            if (location.Contains(dc, StringComparison.OrdinalIgnoreCase))
                return dc;
        }

        return "Unknown";
    }

    private void DrawEventCard(EventResponse ev)
    {
        using var id = ImRaii.PushId(ev.CharacterName);

        var scaledImageSize = new Vector2(ImageSize, ImageSize) * ImGuiHelpers.GlobalScale;
        var isExpanded = _expandedCards.Contains(ev.CharacterName);
        var gameType = ev.Game;
        var (bgColor, gameTypeTextColor) = GetGameTypeColors(gameType);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        if (ImGui.BeginTable("##card", 2, ImGuiTableFlags.None))
        {
            ImGui.TableSetupColumn("##img", ImGuiTableColumnFlags.WidthFixed, scaledImageSize.X);
            ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var tex = !string.IsNullOrWhiteSpace(ev.ImageUrl) ? _imageCache.Get(ev.ImageUrl!) : null;
            if (tex != null)
                ImGui.Image(tex.Handle, scaledImageSize);
            else
                DrawImagePlaceholder(scaledImageSize);

            ImGui.TableSetColumnIndex(1);

            var cellRightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
            var rowTopY = ImGui.GetCursorPosY();

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), FormatDisplayName(ev.CharacterName));
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

            if (!string.IsNullOrWhiteSpace(ev.VenueName) && ev.VenueName != "No Venue")
                ImGui.TextDisabled($"@ {ev.VenueName}");

            if (!string.IsNullOrWhiteSpace(ev.Description))
            {
                ImGuiHelpers.ScaledDummy(2f);
                ImGui.TextWrapped(ev.Description);
            }

            ImGui.EndTable();
        }

        var headerBottomScreen = ImGui.GetCursorScreenPos();

        if (isExpanded)
        {
            var indent = scaledImageSize.X + ImGui.GetStyle().ItemSpacing.X;
            ImGui.Indent(indent);
            DrawExpandedDetails(ev);
            ImGui.Unindent(indent);
            ImGuiHelpers.ScaledDummy(4f);
        }

        var cardBottomScreen = ImGui.GetCursorScreenPos();

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(
            cardTopScreen,
            new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y),
            ImGui.GetColorU32(bgColor),
            4f * ImGuiHelpers.GlobalScale);
        drawList.ChannelsMerge();

        var headerRect = new Vector2(cardTopScreen.X + availWidth, headerBottomScreen.Y);
        var popupIsOpen = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

        if (!popupIsOpen && ImGui.IsMouseHoveringRect(cardTopScreen, headerRect))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (isExpanded)
                    _expandedCards.Remove(ev.CharacterName);
                else
                    _expandedCards.Add(ev.CharacterName);
            }
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

    private static void DrawExpandedDetails(EventResponse ev)
    {
        ImGuiHelpers.ScaledDummy(4f);

        if (ImGui.BeginTable("##details", 2, ImGuiTableFlags.None))
        {
            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            if (ev.Rules.Count > 0)
            {
                var keyColWidth = ev.Rules.Keys.Max(k => ImGui.CalcTextSize(RuleKeyFormatting.FormatDisplayKey(k)).X) + 12f * ImGuiHelpers.GlobalScale;

                if (ImGui.BeginTable("##rules", 2, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("##rk", ImGuiTableColumnFlags.WidthFixed, keyColWidth);
                    ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthStretch);

                    foreach (var rule in ev.Rules)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.TextDisabled(RuleKeyFormatting.FormatDisplayKey(rule.Key));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(FormatRuleValue(rule.Value, rule.Key));
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.TableSetColumnIndex(1);

            ImGui.TextDisabled("Location");
            ImGui.SameLine();
            ImGui.Text(ev.Location);

            if (!string.IsNullOrWhiteSpace(ev.DiscordUrl))
            {
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextDisabled("Discord");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), ev.DiscordUrl);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton($"##copy_{ev.CharacterName}", FontAwesomeIcon.Copy))
                    ImGui.SetClipboardText(ev.DiscordUrl);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Copy to clipboard");
            }

            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(4f);
    }

    private static void DrawImagePlaceholder(Vector2 size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 1f)),
            4f * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(size);
    }

    private static string FormatDisplayName(string characterName)
    {
        var lastSpace = characterName.LastIndexOf(' ');
        return lastSpace > 0
            ? $"{characterName[..lastSpace]}@{characterName[(lastSpace + 1)..]}"
            : characterName;
    }

    private static (Vector4 bg, Vector4 text) GetGameTypeColors(string gameType) => gameType switch
    {
        "Bingo" => (new Vector4(0.85f, 0.25f, 0.25f, 0.18f), new Vector4(1.00f, 0.50f, 0.50f, 1f)),
        "Blackjack" => (new Vector4(0.25f, 0.50f, 0.90f, 0.18f), new Vector4(0.50f, 0.75f, 1.00f, 1f)),
        "Chocobo Racing" => (new Vector4(0.85f, 0.80f, 0.15f, 0.18f), new Vector4(1.00f, 0.95f, 0.30f, 1f)),
        "Mini Games" => (new Vector4(0.20f, 0.80f, 0.40f, 0.18f), new Vector4(0.40f, 1.00f, 0.55f, 1f)),
        "Poker" => (new Vector4(0.00f, 0.80f, 0.80f, 0.18f), new Vector4(0.00f, 1.00f, 1.00f, 1f)),
        "Roulette" => (new Vector4(0.85f, 0.70f, 0.10f, 0.18f), new Vector4(1.00f, 0.84f, 0.00f, 1f)),
        "Scratchcards" => (new Vector4(0.85f, 0.45f, 0.00f, 0.18f), new Vector4(1.00f, 0.60f, 0.00f, 1f)),
        "Spin the Wheel" => (new Vector4(0.90f, 0.60f, 0.70f, 0.18f), new Vector4(1.00f, 0.75f, 0.85f, 1f)),
        _ => (new Vector4(0.50f, 0.50f, 0.50f, 0.12f), new Vector4(0.75f, 0.75f, 0.75f, 1f)),
    };

    private static string FormatRuleValue(object? value, string key = "")
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
                int i => i.ToString("N0"),
                long l => l.ToString("N0"),
                float f => isOdds ? f.ToString("N2") : f.ToString("N0"),
                double d => isOdds ? d.ToString("N2") : d.ToString("N0"),
                _ => value?.ToString() ?? "-"
            };
        }

        return isOdds ? formatted + "x" : formatted;
    }

}
