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

/// <summary>Tab listing live gamba events fetched from the API.</summary>
public class GambaEventsTab : IDisposable
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly GambaWhereClient _client;
    private readonly ImageCache _imageCache;
    private readonly EventLocationTeleportService _teleport;
    private readonly Configuration _config;

    private const int PageSize = 12;

    private List<EventResponse> _events = new();
    private bool _hasLoaded;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;
    private DateTime _nextAutoRefreshUtc;

    private int _page = 1;
    private int _totalPages = 1;
    private int _total;
    private bool _hasActiveFilters;
    private string _querySignature = string.Empty;

    private string? _pendingScrollCharacter;
    private string? _infoPopupCharacter;
    private bool _openInfoRequested;
    private string? _profilePopupCharacter;
    private bool _openProfileRequested;
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

    private static readonly string[] SortOptions = { "Default", "Venue Name", "Host Name", "Game Type" };
    private int _sortBy = 0;

    private static string? SortParam(int index) => index switch
    {
        1 => "venue",
        2 => "host",
        3 => "game",
        _ => null,
    };

    private const float AvatarSize = 52f;
    private const float AvatarOffset = 0f;
    private const float VenueLogoSize = 96f;
    private const float DescriptionTopGap = 6f;
    private const float GameBadgeTopNudge = 5f;
    private const float VenueLineNudge = 1f;
    private const float CardWidth = 360f;
    private const float CardRounding = 6f;
    private const float CardPad = 12f;
    private const float InfoPopupMaxWidth = 520f;
    private const string InfoPopupId = "##GambaGameInfoPopup";
    private const string ProfilePopupId = "##GambaProfilePopup";

    public GambaEventsTab(GambaWhereClient client, ImageCache imageCache, EventLocationTeleportService teleport, Configuration config)
    {
        _client = client;
        _imageCache = imageCache;
        _teleport = teleport;
        _config = config;

        _querySignature = BuildQuerySignature();
        TriggerRefresh();
    }

    public void ExpandAndScrollTo(string characterName)
    {
        _pendingScrollCharacter = characterName;
        _infoPopupCharacter = characterName;
        _openInfoRequested = true;
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
        ApplyQueryChanges();
        ImGui.Separator();
        DrawPaginationBar();
        ImGuiHelpers.ScaledDummy(4f);
        DrawEventList();
        DrawInfoPopup();
        DrawProfilePopup();
    }

    private void DrawHeader()
    {
        var refreshing = _isRefreshing;
        using (ImRaii.Disabled(refreshing))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, refreshing ? "Refreshing..." : "Refresh", "##Refresh"))
                TriggerRefresh();
        }

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

    private string BuildQuerySignature()
    {
        var games = string.Join(",", _selectedGameTypes.OrderBy(g => g, StringComparer.Ordinal));
        var dcs = string.Join(",", _selectedDataCentres.OrderBy(d => d, StringComparer.Ordinal));
        return $"{_sortBy}|{games}|{dcs}";
    }

    private void ApplyQueryChanges()
    {
        var signature = BuildQuerySignature();
        if (signature == _querySignature)
            return;

        _querySignature = signature;
        _page = 1;
        TriggerRefresh();
    }

    private void GoToPage(int page)
    {
        var clamped = Math.Clamp(page, 1, Math.Max(1, _totalPages));
        if (clamped == _page)
            return;

        _page = clamped;
        TriggerRefresh();
    }

    private void TriggerRefresh()
    {
        if (_cts.IsCancellationRequested) return;
        _isRefreshing = true;
        _nextAutoRefreshUtc = DateTime.UtcNow + AutoRefreshInterval;
        var ct = _cts.Token;

        var page = _page;
        var sort = SortParam(_sortBy);
        var gameTypes = _selectedGameTypes.Count > 0 ? _selectedGameTypes.ToArray() : null;
        var dataCentres = _selectedDataCentres.Count > 0 ? _selectedDataCentres.ToArray() : null;
        var hasFilters = gameTypes != null || dataCentres != null;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _client.GetEventsPageAsync(page, PageSize, sort, gameTypes, dataCentres, ct);
                if (ct.IsCancellationRequested) return;

                if (result == null)
                {
                    _events = new List<EventResponse>();
                    _lastRefreshFailed = true;
                }
                else
                {
                    _events = new List<EventResponse>(result.Items);
                    _total = result.Total;
                    _totalPages = Math.Max(1, result.TotalPages);
                    _hasActiveFilters = hasFilters;
                    _lastRefreshFailed = false;

                    if (_page > _totalPages)
                    {
                        _page = _totalPages;
                        _isRefreshing = false;
                        _hasLoaded = true;
                        TriggerRefresh();
                        return;
                    }
                }

                _hasLoaded = true;
                _isRefreshing = false;
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

    private void DrawPaginationBar()
    {
        if (!_hasLoaded || _total <= 0)
            return;

        var refreshing = _isRefreshing;

        var pageText = $"Page {_page} of {_totalPages}";
        var countText = $"({_total} event{(_total == 1 ? "" : "s")})";

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth =
            UIHelper.CalcButtonSize(FontAwesomeIcon.AngleDoubleLeft, "First").X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleLeft, "Prev").X + spacing
            + ImGui.CalcTextSize(pageText).X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleRight, "Next").X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleDoubleRight, "Last").X + spacing
            + ImGui.CalcTextSize(countText).X;

        var offset = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;
        if (offset > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        using (ImRaii.Disabled(refreshing || _page <= 1))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleLeft, "First", "##firstPage"))
                GoToPage(1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleLeft, "Prev", "##prevPage"))
                GoToPage(_page - 1);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(pageText);

        ImGui.SameLine();
        using (ImRaii.Disabled(refreshing || _page >= _totalPages))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleRight, "Next", "##nextPage"))
                GoToPage(_page + 1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleRight, "Last", "##lastPage"))
                GoToPage(_totalPages);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(countText);
    }

    private void DrawEventList()
    {
        if (_lastRefreshFailed)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Server error fetching events, please try again later.");
            return;
        }

        if (!_hasLoaded)
        {
            ImGui.TextDisabled("No events loaded yet - press Refresh above.");
            return;
        }

        if (_events.Count == 0)
        {
            ImGui.TextDisabled(_hasActiveFilters
                ? "No events match the current filters."
                : "No active events found.");
            return;
        }

        DrawCardGrid(_events);
    }

    private void DrawCardGrid(List<EventResponse> events)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var cardWidth = CardWidth * scale;
        var cardHeight = ComputeCardHeight();

        var columns = Math.Max(1, (int)((avail + spacing) / (cardWidth + spacing)));

        var col = 0;
        foreach (var ev in events)
        {
            if (col > 0)
                ImGui.SameLine(0f, spacing);

            DrawCompactCard(ev, cardWidth, cardHeight);

            col++;
            if (col >= columns)
                col = 0;
        }
    }

    private static float HeaderBandHeight()
    {
        var scale = ImGuiHelpers.GlobalScale;
        return AvatarOffset * scale + Math.Max(AvatarSize * scale, 2f * ImGui.GetTextLineHeightWithSpacing());
    }

    private static float ComputeCardHeight()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();

        return pad * 2f
             + HeaderBandHeight()
             + DescriptionTopGap * scale
             + ImGui.GetStyle().ItemSpacing.Y * 2f
             + 2f * lineHeight
             + ImGui.GetFrameHeight();
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

    private void DrawCompactCard(EventResponse ev, float cardWidth, float cardHeight)
    {
        using var id = ImRaii.PushId(ev.CharacterName);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var rounding = CardRounding * scale;
        var (bgColour, accent) = EventCardRenderer.GetGameTypeColors(ev.Game);

        var cardBg = ev.Booster ? BoosterCardEffect.BaseColour : bgColour;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, rounding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        var child = ImRaii.Child("##card", new Vector2(cardWidth, cardHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        using (child)
        {
            if (child.Success)
            {
                var p0 = ImGui.GetWindowPos();
                var sz = ImGui.GetWindowSize();
                var dl = ImGui.GetWindowDrawList();
                var cardTopRight = new Vector2(p0.X + sz.X, p0.Y);

                if (ev.Booster)
                    BoosterCardEffect.DrawHolographicFill(dl, p0, p0 + sz, ImGui.GetTime(), BoosterCardEffect.Seed(ev.CharacterName));

                var offset = AvatarOffset * scale;
                var logoSize = VenueLogoSize * scale;
                var buttonsTop = sz.Y - pad - ImGui.GetFrameHeight();
                var logoTop = Math.Max(pad + 4f * scale, buttonsTop - 2f * scale - logoSize);
                DrawVenueLogo(dl, ev, cardTopRight, logoTop, logoSize);
                DrawGameTypeBadge(dl, ev, accent, cardTopRight, pad, pad + offset - GameBadgeTopNudge * scale);

                if (ev.Booster)
                    BoosterCardEffect.DrawHolographicBorder(dl, p0, p0 + sz, rounding, ImGui.GetTime());
                else
                    DrawCardBorder(dl, p0, p0 + sz, accent, rounding);

                var contentWidth = sz.X - pad * 2f;
                var badgeWidth = string.IsNullOrWhiteSpace(ev.Game) ? 0f : ImGui.CalcTextSize(ev.Game).X;
                var rightReserve = badgeWidth + 12f * scale;
                ImGui.SetCursorPos(new Vector2(pad + offset, pad + offset));
                DrawCardHeader(ev, contentWidth - offset, rightReserve);

                ImGui.SetCursorPos(new Vector2(pad, pad + HeaderBandHeight() + DescriptionTopGap * scale + ImGui.GetStyle().ItemSpacing.Y));
                DrawDescriptionArea(ev, contentWidth);

                ImGui.SetCursorPos(new Vector2(pad, sz.Y - pad - ImGui.GetFrameHeight()));
                DrawCardButtons(ev, contentWidth);
            }
        }

        if (_pendingScrollCharacter == ev.CharacterName)
        {
            ImGui.SetScrollHereY(0.2f);
            _pendingScrollCharacter = null;
        }
    }

    private void DrawCardHeader(EventResponse ev, float contentWidth, float rightReserve)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avatar = AvatarSize * scale;
        var gap = 8f * scale;

        var avatarPos = ImGui.GetCursorScreenPos();
        DrawAvatar(ev, avatarPos, avatar);
        ImGui.Dummy(new Vector2(avatar, avatar));
        ImGui.SameLine(0f, gap);

        var textWidth = contentWidth - avatar - gap - rightReserve;
        if (textWidth < 60f * scale)
            textWidth = contentWidth - avatar - gap;

        using (ImRaii.Group())
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);

            var displayName = EventCardRenderer.FormatDisplayName(ev.CharacterName);
            var splitName = TrySplitNameWorld(displayName, out var name, out var world)
                            && ImGui.CalcTextSize(displayName).X > textWidth;

            if (splitName)
            {
                ImGui.TextColored(_config.SecondaryColour, name);
                ImGui.TextColored(_config.SecondaryColour, world);
            }
            else
            {
                ImGui.TextColored(_config.SecondaryColour, displayName);
            }

            if (!string.IsNullOrWhiteSpace(ev.VenueName) && ev.VenueName != "No Venue")
            {
                if (splitName)
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + VenueLineNudge * scale);

                ImGui.TextDisabled($"@ {ev.VenueName}");
            }

            ImGui.PopTextWrapPos();
        }

        if (!HasProfile(ev))
            return;

        var headerMin = avatarPos;
        var headerMax = new Vector2(ImGui.GetItemRectMax().X, Math.Max(avatarPos.Y + avatar, ImGui.GetItemRectMax().Y));

        ImGui.SetCursorScreenPos(headerMin);
        ImGui.InvisibleButton("##profile", headerMax - headerMin);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.GetWindowDrawList().AddRectFilled(
                headerMin, headerMax,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)),
                4f * scale);
            ImGui.SetTooltip("View profile");
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            OpenProfile(ev);
    }

    private static bool HasProfile(EventResponse ev) =>
        !string.IsNullOrWhiteSpace(ev.ProfileImageUrl)
        || !string.IsNullOrWhiteSpace(ev.Bio)
        || ev.PreferredGames.Count > 0
        || ev.Booster;

    private void OpenProfile(EventResponse ev)
    {
        _profilePopupCharacter = ev.CharacterName;
        _openProfileRequested = true;
    }

    private static bool TrySplitNameWorld(string displayName, out string name, out string world)
    {
        var at = displayName.LastIndexOf('@');
        if (at > 0 && at < displayName.Length - 1)
        {
            name = displayName[..at];
            world = displayName[at..];
            return true;
        }

        name = displayName;
        world = string.Empty;
        return false;
    }

    private static void DrawGameTypeBadge(ImDrawListPtr dl, EventResponse ev, Vector4 accent, Vector2 cardTopRight, float xInset, float yOffset)
    {
        if (string.IsNullOrWhiteSpace(ev.Game))
            return;

        var textSize = ImGui.CalcTextSize(ev.Game);
        var pos = new Vector2(cardTopRight.X - xInset - textSize.X, cardTopRight.Y + yOffset);

        dl.AddText(pos, ImGui.GetColorU32(accent), ev.Game);
    }

    private void DrawAvatar(EventResponse ev, Vector2 pos, float size)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = size * 0.5f;

        var profileTex = !string.IsNullOrWhiteSpace(ev.ProfileImageUrl) ? _imageCache.GetProfile(ev.ProfileImageUrl!) : null;
        if (profileTex != null)
        {
            CircleImage.DrawAt(dl, pos, size, profileTex);
            DrawBoosterFrame(ev, dl, pos, size);
            return;
        }

        var placeholder = _imageCache.GetBundledImage("profileplaceholder.png");
        if (placeholder != null)
        {
            dl.AddImageRounded(placeholder.Handle, pos, pos + new Vector2(size, size),
                Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), rounding);
        }
        else
        {
            dl.AddRectFilled(pos, pos + new Vector2(size, size),
                ImGui.GetColorU32(new Vector4(0.22f, 0.22f, 0.26f, 1f)), rounding);
        }

        DrawBoosterFrame(ev, dl, pos, size);
    }

    private void DrawBoosterFrame(EventResponse ev, ImDrawListPtr dl, Vector2 pos, float size)
    {
        if (!ev.Booster)
            return;

        var tex = _imageCache.GetBundledImage("boosterborder.png");
        if (tex == null)
            return;

        var centre = pos + new Vector2(size * 0.5f, size * 0.5f);
        var half = size * 0.58f;
        var min = centre - new Vector2(half, half);
        var max = centre + new Vector2(half, half);
        dl.AddImage(tex.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
    }

    private void DrawVenueLogo(ImDrawListPtr dl, EventResponse ev, Vector2 cardTopRight, float topOffset, float logoSize)
    {
        var tex = !string.IsNullOrWhiteSpace(ev.ImageUrl) ? _imageCache.Get(ev.ImageUrl!) : null;
        if (tex == null)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var right = cardTopRight.X - pad;
        var top = cardTopRight.Y + topOffset;
        var min = new Vector2(right - logoSize, top);
        var max = new Vector2(right, top + logoSize);

        dl.AddImageRounded(tex.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)), 4f * scale);
    }

    private void DrawDescriptionArea(EventResponse ev, float contentWidth)
    {
        if (ev.Description == SessionConstants.BreakMessage)
        {
            EventCardRenderer.DrawBreakBadge();
            return;
        }

        if (string.IsNullOrWhiteSpace(ev.Description))
            return;

        var maxHeight = 2f * ImGui.GetTextLineHeight() + 1f;
        var text = UIHelper.TruncateToFit(ev.Description, contentWidth, maxHeight);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
    }

    private void DrawCardButtons(EventResponse ev, float rowWidth)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonWidth = (rowWidth - spacing) / 2f;
        var buttonSize = new Vector2(buttonWidth, ImGui.GetFrameHeight());

        using var buttonColours = ImRaii.PushColor(ImGuiCol.Button, ThemeColours.ButtonNormal(_config.PrimaryColour))
            .Push(ImGuiCol.ButtonHovered, ThemeColours.ButtonHovered(_config.PrimaryColour))
            .Push(ImGuiCol.ButtonActive, ThemeColours.ButtonPressed(_config.PrimaryColour));

        if (UIHelper.IconTextButton(FontAwesomeIcon.InfoCircle, "Game Info", buttonSize, "##gameInfo"))
        {
            _infoPopupCharacter = ev.CharacterName;
            _openInfoRequested = true;
        }

        ImGui.SameLine();
        var teleportEnabled = _teleport.IsLifestreamAvailable;
        using (ImRaii.Disabled(!teleportEnabled))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.MapMarkerAlt, "Teleport", buttonSize, "##teleport"))
                _teleport.RequestTravel(ev);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(
                teleportEnabled
                    ? "Travel through Lifestream using world, housing district, ward, and plot when this listing includes them."
                    : "Install NightmareXIV Lifestream from the plugin installer, enable it on this character, then reload plugins.");
        }
    }

    private static void DrawCardBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 accent, float rounding)
    {
        var border = new Vector4(accent.X, accent.Y, accent.Z, 0.55f);
        dl.AddRect(min, max, ImGui.GetColorU32(border), rounding, ImDrawFlags.None, 1.5f * ImGuiHelpers.GlobalScale);
    }

    private void DrawInfoPopup()
    {
        if (_openInfoRequested)
        {
            ImGui.OpenPopup(InfoPopupId);
            _openInfoRequested = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var ev = _events.FirstOrDefault(e => e.CharacterName == _infoPopupCharacter);

        var booster = ev?.Booster ?? false;
        var (bgColour, accent) = ev != null
            ? EventCardRenderer.GetGameTypeColors(ev.Game)
            : (default, default);
        var cardBg = booster ? BoosterCardEffect.BaseColour : SolidCardBg(bgColour);
        var rounding = CardRounding * scale;

        ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new Vector2(float.MaxValue, float.MaxValue));

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var colBg = ImRaii.PushColor(ImGuiCol.PopupBg, cardBg, ev != null);
        using var popup = ImRaii.Popup(InfoPopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        if (ev == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.TextColored(_config.SecondaryColour, EventCardRenderer.FormatDisplayName(ev.CharacterName));
        if (!string.IsNullOrWhiteSpace(ev.Game))
        {
            ImGui.SameLine(0f, 12f * scale);
            ImGui.Dummy(ImGui.CalcTextSize(ev.Game));
        }

        if (!string.IsNullOrWhiteSpace(ev.VenueName) && ev.VenueName != "No Venue")
            ImGui.TextDisabled($"@ {ev.VenueName}");

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2f);

        if (ev.Description == SessionConstants.BreakMessage)
        {
            EventCardRenderer.DrawBreakBadge();
        }
        else if (!string.IsNullOrWhiteSpace(ev.Description))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (InfoPopupMaxWidth - 28f) * scale);
            ImGui.TextWrapped(ev.Description);
            ImGui.PopTextWrapPos();
        }

        ImGuiHelpers.ScaledDummy(6f);
        DrawExpandedDetails(ev);
        ImGuiHelpers.ScaledDummy(8f);

        var teleportEnabled = _teleport.IsLifestreamAvailable;
        using (ImRaii.Disabled(!teleportEnabled))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.MapMarkerAlt, "Teleport", "##popupTeleport"))
                _teleport.RequestTravel(ev);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(
                teleportEnabled
                    ? "Travel through Lifestream using world, housing district, ward, and plot when this listing includes them."
                    : "Install NightmareXIV Lifestream from the plugin installer, enable it on this character, then reload plugins.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Close##popupClose"))
            ImGui.CloseCurrentPopup();

        var p0 = ImGui.GetWindowPos();
        var sz = ImGui.GetWindowSize();

        if (!string.IsNullOrWhiteSpace(ev.Game))
        {
            var wp = ImGui.GetStyle().WindowPadding;
            var badgeSize = ImGui.CalcTextSize(ev.Game);
            var badgePos = new Vector2(p0.X + sz.X - wp.X - badgeSize.X, p0.Y + wp.Y);
            dl.AddText(badgePos, ImGui.GetColorU32(accent), ev.Game);
        }

        dl.ChannelsSetCurrent(0);
        dl.PushClipRect(new Vector2(-10000f, -10000f), new Vector2(100000f, 100000f), false);
        if (booster)
        {
            BoosterCardEffect.DrawHolographicFoil(dl, p0, p0 + sz, ImGui.GetTime());
            BoosterCardEffect.DrawHolographicBorder(dl, p0, p0 + sz, rounding, ImGui.GetTime());
        }
        else
        {
            DrawCardBorder(dl, p0, p0 + sz, accent, rounding);
        }
        dl.PopClipRect();
        dl.ChannelsMerge();
    }

    private void DrawProfilePopup()
    {
        var ev = _events.FirstOrDefault(e => e.CharacterName == _profilePopupCharacter);
        var data = ev == null
            ? null
            : new ProfilePopup.Data
            {
                DisplayName = EventCardRenderer.FormatDisplayName(ev.CharacterName),
                ProfileImageUrl = ev.ProfileImageUrl,
                Bio = ev.Bio,
                PreferredGames = ev.PreferredGames,
                Booster = ev.Booster,
            };

        ProfilePopup.Draw(ProfilePopupId, ref _openProfileRequested, _imageCache, _config, data);
    }

    private Vector4 SolidCardBg(Vector4 gameColour)
    {
        var baseBg = ThemeColours.TintedWindowBg(_config.PrimaryColour);
        var a = gameColour.W;
        return new Vector4(
            baseBg.X * (1f - a) + gameColour.X * a,
            baseBg.Y * (1f - a) + gameColour.Y * a,
            baseBg.Z * (1f - a) + gameColour.Z * a,
            1f);
    }

    private void DrawExpandedDetails(EventResponse ev)
    {
        var scale = ImGuiHelpers.GlobalScale;

        using (ImRaii.Group())
        {
            if (ev.Rules.Count > 0)
            {
                using var rulesTable = ImRaii.Table("##rules", 2, ImGuiTableFlags.SizingFixedFit);
                if (rulesTable)
                {
                    ImGui.TableSetupColumn("##rk", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthFixed);

                    var disabledColour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
                    foreach (var rule in ev.Rules)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        using (ImRaii.PushColor(ImGuiCol.Text, disabledColour))
                            ImGui.TextUnformatted(RuleKeyFormatting.FormatDisplayKey(rule.Key));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextUnformatted(EventCardRenderer.FormatRuleValue(rule.Value, rule.Key));
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("No rules listed.");
            }
        }

        ImGui.SameLine(0f, 24f * scale);
        using (ImRaii.Group())
        {
            ImGui.TextDisabled("Location");
            ImGui.TextUnformatted(ev.Location);

            if (!string.IsNullOrWhiteSpace(ev.DiscordUrl))
            {
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextDisabled("Discord");
                var url = ev.DiscordUrl!;
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 1f, 1f)))
                    ImGui.TextUnformatted(url);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip($"Open in browser:\n{url}");
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    OpenBrowser.TryOpen(url);
            }
        }
    }
}
