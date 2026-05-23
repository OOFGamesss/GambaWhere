using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class GambaEventsTab : IDisposable
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly GambaWhereClient _client;
    private readonly ImageCache _imageCache;
    private readonly EventLocationTeleportService _teleport;
    private readonly Configuration _config;

    private List<EventResponse> _events = new();
    private DateTime? _lastUpdated;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;
    private DateTime _nextAutoRefreshUtc;

    private readonly HashSet<string> _expandedCards = new();
    private string? _pendingScrollCharacter;
    private readonly HashSet<string> _selectedGameTypes = new();
    private readonly HashSet<string> _selectedDataCentres = new();
    private readonly CancellationTokenSource _cts = new();

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

    private static readonly string[] SortOptions = { "Venue Name", "Host Name", "Game Type" };
    private int _sortBy = 0;

    private static readonly float ImageSize = 70f;

    public GambaEventsTab(GambaWhereClient client, ImageCache imageCache, EventLocationTeleportService teleport, Configuration config)
    {
        _client = client;
        _imageCache = imageCache;
        _teleport = teleport;
        _config = config;

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
            if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, refreshing ? "Refreshing..." : "Refresh", "##Refresh"))
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
        var totalWidth = FilterWidth * 3 * ImGuiHelpers.GlobalScale + spacing * 2;
        var rightEdge = ImGui.GetContentRegionMax().X;
        var filtersStart = rightEdge - totalWidth;

        ImGui.SameLine(filtersStart);
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##sortBy", "Sort: " + SortOptions[_sortBy]))
        {
            for (var i = 0; i < SortOptions.Length; i++)
            {
                if (ImGui.Selectable(SortOptions[i], _sortBy == i))
                    _sortBy = i;
            }
            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sort events");

        ImGui.SameLine();
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
        if (_cts.IsCancellationRequested) return;
        _isRefreshing = true;
        _nextAutoRefreshUtc = DateTime.UtcNow + AutoRefreshInterval;
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _client.GetEventsAsync(ct);
                if (ct.IsCancellationRequested) return;
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
            }
            catch (OperationCanceledException)
            {
                _isRefreshing = false;
            }
        }, ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
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

        query = _sortBy switch
        {
            1 => query.OrderBy(ev => ev.CharacterName, StringComparer.OrdinalIgnoreCase),
            2 => query.OrderBy(ev => ev.Game, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(ev => ev.CharacterName, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(ev => string.IsNullOrWhiteSpace(ev.VenueName) || ev.VenueName == "No Venue" ? 1 : 0)
                      .ThenBy(ev => ev.VenueName, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(ev => ev.CharacterName, StringComparer.OrdinalIgnoreCase),
        };

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
        var (bgColor, gameTypeTextColor) = EventCardRenderer.GetGameTypeColors(gameType);

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
                EventCardRenderer.DrawImagePlaceholder(scaledImageSize);

            ImGui.TableSetColumnIndex(1);

            var cellRightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
            var rowTopY = ImGui.GetCursorPosY();

            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), EventCardRenderer.FormatDisplayName(ev.CharacterName));
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

            if (ev.Description == SessionConstants.BreakMessage)
            {
                EventCardRenderer.DrawBreakBadge();
                if (!isExpanded)
                    ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click to see more...");
            }
            else if (!string.IsNullOrWhiteSpace(ev.Description))
            {
                ImGuiHelpers.ScaledDummy(2f);
                if (isExpanded)
                {
                    ImGui.TextWrapped(ev.Description);
                }
                else
                {
                    var descWidth = ImGui.GetContentRegionAvail().X;
                    var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                    var isTruncated = ImGui.CalcTextSize(ev.Description, false, descWidth).Y > 5f * lineHeight;

                    if (isTruncated)
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                        using var clip = ImRaii.Child("##descClip", new Vector2(descWidth, 6f * lineHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoNav);
                        ImGui.PopStyleVar();
                        if (clip)
                        {
                            var clipStart = ImGui.GetCursorScreenPos();
                            ImGui.PushClipRect(clipStart, clipStart + new Vector2(descWidth, 5f * lineHeight), true);
                            ImGui.TextWrapped(ev.Description);
                            ImGui.PopClipRect();
                            ImGui.SetCursorPosY(5f * lineHeight);
                            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click to see more...");
                        }
                    }
                    else
                    {
                        ImGui.TextWrapped(ev.Description);
                        ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click to see more...");
                    }
                }
            }
            else if (!isExpanded)
            {
                ImGuiHelpers.ScaledDummy(2f);
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click to see more...");
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

            drawList.AddRectFilled(
                cardTopScreen,
                cardRect,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)),
                4f * ImGuiHelpers.GlobalScale);

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
                        ImGui.TextWrapped(EventCardRenderer.FormatRuleValue(rule.Value, rule.Key));
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
                if (UIHelper.IconTextButton(FontAwesomeIcon.MapMarkerAlt, "Teleport", "##teleportBtn"))
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

}
