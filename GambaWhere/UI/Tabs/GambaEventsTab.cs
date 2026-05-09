using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Images;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class GambaEventsTab
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly GambaWhereClient _client;
    private readonly ImageCache _imageCache;
    private readonly EventLocationTeleportService _teleport;

    private List<EventResponse> _events = new();
    private DateTime? _lastUpdated;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;
    private DateTime _nextAutoRefreshUtc;

    private readonly HashSet<string> _expandedCards = new();
    private string? _pendingScrollCharacter;
    private readonly HashSet<string> _selectedGameTypes = new();
    private readonly HashSet<string> _selectedDataCentres = new();

    public static readonly string[] KnownGameTypes =
    {
        "Bingo", "Blackjack", "Chocobo Racing", "Mini Games",
        "Poker", "Roulette", "Scratchcards", "Spin the Wheel"
    };

    public static readonly string[] KnownDataCentres =
    {
        "Aether", "Crystal", "Dynamis", "Primal",
        "Chaos", "Light",
        "Elemental", "Gaia", "Mana", "Meteor",
        "Materia"
    };

    private static readonly float ImageSize = 70f;

    public GambaEventsTab(GambaWhereClient client, ImageCache imageCache, EventLocationTeleportService teleport)
    {
        _client = client;
        _imageCache = imageCache;
        _teleport = teleport;

        TriggerRefresh();
    }

    public Action<IReadOnlyList<EventResponse>>? OnEventsRefreshed { get; set; }

    public void ExpandAndScrollTo(string characterName)
    {
        _expandedCards.Add(characterName);
        _pendingScrollCharacter = characterName;
    }

    public void Tick()
    {
        if (_isRefreshing)
            return;

        if (DateTime.UtcNow < _nextAutoRefreshUtc)
            return;

        TriggerRefresh();
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
        var refreshing = _isRefreshing;
        using (ImRaii.Disabled(refreshing))
        {
            if (ImGui.Button(refreshing ? "Refreshing..." : "Refresh"))
                TriggerRefresh();
        }

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
        _nextAutoRefreshUtc = DateTime.UtcNow + AutoRefreshInterval;
        _ = Task.Run(async () =>
        {
            var results = await _client.GetEventsAsync();
            IReadOnlyList<EventResponse>? successSnapshot = null;
            if (results == null)
            {
                _events = new List<EventResponse>();
                _lastRefreshFailed = true;
            }
            else
            {
                var snapshot = new List<EventResponse>(results);
                _events = snapshot;
                _lastRefreshFailed = false;
                successSnapshot = snapshot;
            }
            _lastUpdated = DateTime.Now;
            _isRefreshing = false;

            if (successSnapshot != null)
                OnEventsRefreshed?.Invoke(successSnapshot);
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

    public static string InferDataCentre(string location)
    {
        if (string.IsNullOrEmpty(location))
            return "Unknown";

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

            if (isExpanded)
            {
                DrawExpandedDetails(ev);
                ImGuiHelpers.ScaledDummy(4f);
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

        var cardRect = new Vector2(cardTopScreen.X + availWidth, cardBottomScreen.Y);
        var popupIsOpen = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

        if (!popupIsOpen && ImGui.IsMouseHoveringRect(cardTopScreen, cardRect) && !ImGui.IsAnyItemHovered())
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

        if (_pendingScrollCharacter == ev.CharacterName)
        {
            ImGui.SetScrollHereY(0.2f);
            _pendingScrollCharacter = null;
        }
    }

    private void DrawExpandedDetails(EventResponse ev)
    {
        if (ImGui.BeginTable("##details", 2, ImGuiTableFlags.None))
        {
            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            if (ev.Rules.Count > 0)
            {
                var availForRules = ImGui.GetContentRegionAvail().X;
                var rawKeyWidth = ev.Rules.Keys.Max(k => ImGui.CalcTextSize(RuleKeyFormatting.FormatDisplayKey(k)).X) + 12f * ImGuiHelpers.GlobalScale;
                var minKeyWidth = 60f * ImGuiHelpers.GlobalScale;
                var keyColWidth = Math.Min(rawKeyWidth, Math.Max(availForRules * 0.55f, minKeyWidth));

                if (ImGui.BeginTable("##rules", 2, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("##rk", ImGuiTableColumnFlags.WidthFixed, keyColWidth);
                    ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthStretch);

                    var disabledColour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
                    foreach (var rule in ev.Rules)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        using (ImRaii.PushColor(ImGuiCol.Text, disabledColour))
                            ImGui.TextWrapped(RuleKeyFormatting.FormatDisplayKey(rule.Key));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextWrapped(FormatRuleValue(rule.Value, rule.Key));
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.TableSetColumnIndex(1);

            ImGui.TextDisabled("Location");
            ImGui.SameLine();
            ImGui.TextWrapped(ev.Location);

            var teleportEnabled = _teleport.IsLifestreamAvailable;

            using (ImRaii.Disabled(!teleportEnabled))
            {
                if (ImGui.SmallButton($"Teleport##teleportBtn"))
                    _teleport.RequestTravel(ev);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(
                    teleportEnabled
                        ? "Travel through Lifestream using world, housing district, ward, and plot when this listing includes them."
                        : "Install NightmareXIV Lifestream from the plugin installer, enable it on this character, then reload plugins.");
            }

            if (!string.IsNullOrWhiteSpace(ev.DiscordUrl))
            {
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextDisabled("Discord");
                ImGui.SameLine();
                var url = ev.DiscordUrl!;
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 1f, 1f)))
                    ImGui.TextWrapped(url);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip($"Open in browser:\n{url}");
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    OpenBrowser.TryOpen(url);
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
        "Roulette" => (new Vector4(0.52f, 0.38f, 0.78f, 0.18f), new Vector4(0.82f, 0.68f, 1.00f, 1f)),
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
