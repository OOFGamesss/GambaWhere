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
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Models;
using GambaWhere.Services;
using GambaWhere.UI.CardEffects;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Tab listing scheduled gamba sessions that have not started yet, shown in Server Time and a countdown timer.</summary>
public class UpcomingGambaEventsTab : IDisposable
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly GambaWhereClient _client;
    private readonly ImageService _imageService;
    private readonly Configuration _config;
    private readonly ScheduledSessionService _scheduledSessions;
    private readonly PlayerInfoService _playerInfo;

    private const int PageSize = 12;

    private List<ScheduledEventResponse> _schedules = new();
    private bool _hasLoaded;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;
    private DateTime _nextAutoRefreshUtc;

    private int _page = 1;
    private int _totalPages = 1;
    private int _total;
    private bool _hasActiveFilters;
    private bool _thisCharacterOnly;
    private string? _currentCharacterName;
    private string _querySignature = string.Empty;

    private string? _infoPopupId;
    private bool _openInfoRequested;
    private string? _profilePopupId;
    private bool _openProfileRequested;
    private string? _pendingCancelId;
    private bool _openCancelRequested;
    private string? _pendingStartId;
    private bool _openStartWarningRequested;

    private string? _editScheduleId;
    private bool _openEditRequested;
    private readonly DateTimePicker _editPicker = new();
    private string _editLocation = string.Empty;
    private string _editDescription = string.Empty;
    private int _editRecurrenceIndex;
    private DateTime _editOriginalTime;
    private string _editOriginalLocation = string.Empty;
    private string _editOriginalDescription = string.Empty;
    private string _editOriginalRecurrence = ScheduleRecurrence.Once;
    private string _editError = string.Empty;
    private volatile bool _isSaving;

    private readonly HashSet<string> _selectedGameTypes = new();
    private readonly HashSet<string> _selectedDataCentres = new();
    private readonly CancellationTokenSource _cts = new();

    private static readonly string[] SortOptions = { "Soonest", "Venue Name", "Host Name", "Game Type" };
    private int _sortBy;

    private static string? SortParam(int index) => index switch
    {
        1 => "venue",
        2 => "host",
        3 => "game",
        _ => "soonest",
    };

    private const float AvatarSize = 52f;
    private const float VenueLogoSize = 112f;
    private const float CardWidth = 360f;
    private const float CardRounding = 6f;
    private const float CardPad = 12f;
    private const float BodyIndent = 20f;
    private const float DescriptionTopGap = 6f;
    private const float GameBadgeTopNudge = 5f;
    private const float InfoPopupMaxWidth = 520f;
    private const string InfoPopupId = "##GambaUpcomingInfoPopup";
    private const string ProfilePopupId = "##GambaUpcomingProfilePopup";
    private const string CancelPopupId = "##GambaUpcomingCancelPopup";
    private const string StartWarningPopupId = "##GambaUpcomingStartWarningPopup";
    private const string EditPopupId = "##GambaUpcomingEditPopup";
    private const int EditLocationMaxLength = 128;
    private const int EditDescriptionMaxLength = 511;

    private static readonly Vector4 SoftRed = new(1f, 0.4f, 0.4f, 1f);

    public static string[] KnownGameTypes => GameCategories.Keys;

    public UpcomingGambaEventsTab(
        GambaWhereClient client,
        ImageService imageService,
        Configuration config,
        ScheduledSessionService scheduledSessions,
        PlayerInfoService playerInfo)
    {
        _client = client;
        _imageService = imageService;
        _config = config;
        _scheduledSessions = scheduledSessions;
        _playerInfo = playerInfo;

        _querySignature = BuildQuerySignature();
    }

    public void EnsureLoaded()
    {
        _currentCharacterName = _playerInfo.GetCharacterName();

        if (!_hasLoaded && !_isRefreshing)
            TriggerRefresh();
    }

    public void RequestRefresh()
    {
        if (!_isRefreshing)
            TriggerRefresh();
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
        _currentCharacterName = _playerInfo.GetCharacterName();

        DrawHeader();
        ApplyQueryChanges();
        ImGui.Separator();
        DrawPaginationBar();
        ImGuiHelpers.ScaledDummy(4f);
        DrawScheduleList();
        DrawInfoPopup();
        DrawProfilePopup();
        DrawCancelPopup();
        DrawStartWarningPopup();
        DrawEditPopup();
    }

    private void DrawHeader()
    {
        var refreshing = _isRefreshing;
        using (ImRaii.Disabled(refreshing))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Sync, refreshing ? "Refreshing..." : "Refresh", "##UpcomingRefresh"))
                TriggerRefresh();
        }

        ImGui.SameLine();
        DrawThisCharacterToggle();

        DrawFilters();
    }

    private void DrawThisCharacterToggle()
    {
        using (ImRaii.Disabled(!_playerInfo.IsLoggedIn))
        using (new ImRaii.ColorDisposable()
                   .Push(ImGuiCol.Button, ThemeColours.ButtonHovered(_config.SecondaryColour), _thisCharacterOnly)
                   .Push(ImGuiCol.ButtonHovered, ThemeColours.ButtonHovered(_config.SecondaryColour), _thisCharacterOnly)
                   .Push(ImGuiCol.ButtonActive, ThemeColours.ButtonPressed(_config.SecondaryColour), _thisCharacterOnly))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.User, "This Character", "##UpcomingMineOnly"))
                _thisCharacterOnly = !_thisCharacterOnly;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(_playerInfo.IsLoggedIn
                ? "Show only the sessions scheduled on the character you are logged in as."
                : "Log in to filter by your own character.");
        }
    }

    private void DrawFilters()
    {
        const float FilterWidth = 140f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = FilterWidth * 3 * ImGuiHelpers.GlobalScale + spacing * 2;
        var rightEdge = ImGui.GetContentRegionMax().X;

        using var disabled = ImRaii.Disabled(_thisCharacterOnly);

        ImGui.SameLine(rightEdge - totalWidth);
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##upcomingSortBy", "Sort: " + SortOptions[_sortBy]))
        {
            if (combo)
            {
                for (var i = 0; i < SortOptions.Length; i++)
                {
                    if (ImGui.Selectable(SortOptions[i], _sortBy == i))
                        _sortBy = i;
                }
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sort scheduled sessions");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        MultiSelectCombo.Draw("##upcomingGameTypeFilter", "Game Type", KnownGameTypes, _selectedGameTypes);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by game type");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(FilterWidth * ImGuiHelpers.GlobalScale);
        MultiSelectCombo.Draw("##upcomingDataCentreFilter", "Data Centre", GambaEventsTab.KnownDataCentres, _selectedDataCentres);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by data centre");
    }

    private string BuildQuerySignature()
    {
        var games = string.Join(",", _selectedGameTypes.OrderBy(g => g, StringComparer.Ordinal));
        var dcs = string.Join(",", _selectedDataCentres.OrderBy(d => d, StringComparer.Ordinal));
        return $"{_thisCharacterOnly}|{_sortBy}|{games}|{dcs}";
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
        if (_cts.IsCancellationRequested)
            return;

        _isRefreshing = true;
        _nextAutoRefreshUtc = DateTime.UtcNow + AutoRefreshInterval;
        var ct = _cts.Token;

        if (_thisCharacterOnly)
        {
            TriggerThisCharacterRefresh(_currentCharacterName, ct);
            return;
        }

        var page = _page;
        var sort = SortParam(_sortBy);
        var gameTypes = _selectedGameTypes.Count > 0 ? _selectedGameTypes.ToArray() : null;
        var dataCentres = _selectedDataCentres.Count > 0 ? _selectedDataCentres.ToArray() : null;
        var hasFilters = gameTypes != null || dataCentres != null;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _client.GetScheduledPageAsync(page, PageSize, sort, gameTypes, dataCentres, ct);
                if (ct.IsCancellationRequested)
                    return;

                if (result == null)
                {
                    _schedules = new List<ScheduledEventResponse>();
                    _lastRefreshFailed = true;
                }
                else
                {
                    _schedules = new List<ScheduledEventResponse>(result.Items);
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

    private void TriggerThisCharacterRefresh(string? characterName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(characterName))
        {
            _schedules = new List<ScheduledEventResponse>();
            _total = 0;
            _totalPages = 1;
            _page = 1;
            _lastRefreshFailed = false;
            _hasLoaded = true;
            _isRefreshing = false;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _client.GetScheduledAsync(characterName, ct);
                if (ct.IsCancellationRequested)
                    return;

                if (result == null)
                {
                    _schedules = new List<ScheduledEventResponse>();
                    _lastRefreshFailed = true;
                }
                else
                {
                    _schedules = result.OrderBy(s => s.ScheduledFor).ToList();
                    _total = _schedules.Count;
                    _totalPages = 1;
                    _page = 1;
                    _hasActiveFilters = false;
                    _lastRefreshFailed = false;
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

    private void DrawPaginationBar()
    {
        if (_thisCharacterOnly || !_hasLoaded || _total <= 0)
            return;

        var refreshing = _isRefreshing;

        var pageText = $"Page {_page} of {_totalPages}";
        var countText = $"({_total} session{(_total == 1 ? "" : "s")})";

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
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleLeft, "First", "##upcomingFirstPage"))
                GoToPage(1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleLeft, "Prev", "##upcomingPrevPage"))
                GoToPage(_page - 1);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(pageText);

        ImGui.SameLine();
        using (ImRaii.Disabled(refreshing || _page >= _totalPages))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleRight, "Next", "##upcomingNextPage"))
                GoToPage(_page + 1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleRight, "Last", "##upcomingLastPage"))
                GoToPage(_totalPages);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(countText);
    }

    private void DrawScheduleList()
    {
        if (_lastRefreshFailed)
        {
            ImGui.TextColored(SoftRed, "Server error fetching scheduled sessions, please try again later.");
            return;
        }

        if (!_hasLoaded)
        {
            ImGui.TextDisabled("No scheduled sessions loaded yet - press Refresh above.");
            return;
        }

        if (_schedules.Count == 0)
        {
            if (_thisCharacterOnly)
                ImGui.TextDisabled("This character has nothing scheduled. Book one from the Host Gamba tab.");
            else
                ImGui.TextDisabled(_hasActiveFilters
                    ? "No scheduled sessions match the current filters."
                    : "Nothing scheduled yet. Host one from the Host Gamba tab.");
            return;
        }

        DrawCardGrid(_schedules);
    }

    private void DrawCardGrid(List<ScheduledEventResponse> schedules)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var cardWidth = CardWidth * scale;
        var cardHeight = ComputeCardHeight();

        var columns = Math.Max(1, (int)((avail + spacing) / (cardWidth + spacing)));

        var mine = new HashSet<string>(
            _scheduledSessions.Snapshot().Select(s => s.Id), StringComparer.Ordinal);

        var col = 0;
        foreach (var schedule in schedules)
        {
            if (col > 0)
                ImGui.SameLine(0f, spacing);

            DrawScheduleCard(schedule, cardWidth, cardHeight, mine.Contains(schedule.Id));

            col++;
            if (col >= columns)
                col = 0;
        }
    }

    private static float HeaderBandHeight()
    {
        var scale = ImGuiHelpers.GlobalScale;
        return Math.Max(AvatarSize * scale, 2f * ImGui.GetTextLineHeightWithSpacing());
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
             + 3f * lineHeight
             + ImGui.GetFrameHeight();
    }

    private void DrawScheduleCard(ScheduledEventResponse schedule, float cardWidth, float cardHeight, bool mine)
    {
        using var id = ImRaii.PushId(schedule.Id);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var rounding = CardRounding * scale;
        var (bgColour, accent) = EventCardRenderer.GetGameTypeColors(schedule.Game);

        var cardEffect = GetCardEffect(schedule);
        var cardBg = CardEffectResolver.BaseColour(cardEffect) ?? bgColour;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, rounding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        var child = ImRaii.Child("##upcomingCard", new Vector2(cardWidth, cardHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        var borderMin = Vector2.Zero;
        var borderMax = Vector2.Zero;

        using (child)
        {
            if (child.Success)
            {
                var p0 = ImGui.GetWindowPos();
                var sz = ImGui.GetWindowSize();
                borderMin = p0;
                borderMax = p0 + sz;
                var dl = ImGui.GetWindowDrawList();
                var cardTopRight = new Vector2(p0.X + sz.X, p0.Y);

                CardEffectDrawer.DrawFill(dl, cardEffect, p0, p0 + sz, ImGui.GetTime(), CardEffectHelpers.Seed(schedule.CharacterName));

                var logoSize = VenueLogoSize * scale;
                var buttonsTop = sz.Y - pad - ImGui.GetFrameHeight();
                var logoTop = Math.Max(pad + 4f * scale, buttonsTop - 2f * scale - logoSize);
                DrawVenueLogo(dl, schedule, cardTopRight, logoTop, logoSize);
                DrawGameTypeBadge(dl, schedule, accent, cardTopRight, pad, pad - GameBadgeTopNudge * scale);

                if (!CardEffectDrawer.HasCustomBorder(cardEffect))
                    DrawCardBorder(dl, p0, p0 + sz, mine ? _config.SecondaryColour : accent, rounding, mine);

                var contentWidth = sz.X - pad * 2f;
                var badgeWidth = string.IsNullOrWhiteSpace(schedule.Game) ? 0f : ImGui.CalcTextSize(schedule.Game).X;

                ImGui.SetCursorPos(new Vector2(pad, pad));
                DrawCardHeader(schedule, contentWidth, badgeWidth + 12f * scale);

                ImGui.SetCursorPos(new Vector2(pad, pad + HeaderBandHeight() + DescriptionTopGap * scale + ImGui.GetStyle().ItemSpacing.Y));
                DrawTimingBlock(schedule, contentWidth);

                ImGui.SetCursorPos(new Vector2(pad, sz.Y - pad - ImGui.GetFrameHeight()));
                DrawCardButtons(schedule, contentWidth, mine);
            }
        }

        if (borderMin != borderMax)
            CardEffectDrawer.DrawBorderAfterChildWindow(cardEffect, borderMin, borderMax, rounding, ImGui.GetTime());
    }

    private void DrawCardHeader(ScheduledEventResponse schedule, float contentWidth, float rightReserve)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avatar = AvatarSize * scale;
        var gap = 8f * scale;

        var avatarPos = ImGui.GetCursorScreenPos();
        DrawAvatar(schedule, avatarPos, avatar);
        ImGui.Dummy(new Vector2(avatar, avatar));
        ImGui.SameLine(0f, gap);

        var textWidth = contentWidth - avatar - gap - rightReserve;
        if (textWidth < 60f * scale)
            textWidth = contentWidth - avatar - gap;

        using (ImRaii.Group())
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextColored(_config.SecondaryColour, EventCardRenderer.FormatDisplayName(schedule.CharacterName));

            if (!string.IsNullOrWhiteSpace(schedule.VenueName) && schedule.VenueName != "No Venue")
                ImGui.TextDisabled($"@ {schedule.VenueName}");

            ImGui.PopTextWrapPos();
        }

        if (!HasProfile(schedule))
            return;

        var headerMin = avatarPos;
        var headerMax = new Vector2(ImGui.GetItemRectMax().X, Math.Max(avatarPos.Y + avatar, ImGui.GetItemRectMax().Y));

        ImGui.SetCursorScreenPos(headerMin);
        ImGui.InvisibleButton("##upcomingProfile", headerMax - headerMin);

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
        {
            _profilePopupId = schedule.Id;
            _openProfileRequested = true;
        }
    }

    private void DrawTimingBlock(ScheduledEventResponse schedule, float contentWidth)
    {
        var indent = BodyIndent * ImGuiHelpers.GlobalScale;
        var width = contentWidth - indent;

        using var indented = ImRaii.PushIndent(indent, false);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);

        using (ImRaii.PushColor(ImGuiCol.Text, TimeColour(schedule.ScheduledFor)))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(FontAwesomeIcon.CalendarAlt.ToIconString());

            ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.TextUnformatted($"{ServerTime.FormatServerTime(schedule.ScheduledFor)}  {ServerTime.FormatCountdown(schedule.ScheduledFor)}");
        }

        ImGui.PopTextWrapPos();

        DrawDescriptionArea(schedule, width);
    }

    private void DrawDescriptionArea(ScheduledEventResponse schedule, float contentWidth)
    {
        if (string.IsNullOrWhiteSpace(schedule.Description))
            return;

        var maxHeight = 2f * ImGui.GetTextLineHeight() + 1f;
        var text = UIHelper.TruncateToFit(schedule.Description, contentWidth, maxHeight);
        var startScreen = ImGui.GetCursorScreenPos();

        if (ImGui.InvisibleButton("##upcomingDescDetails", new Vector2(contentWidth, maxHeight)))
        {
            _infoPopupId = schedule.Id;
            _openInfoRequested = true;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        ImGui.GetWindowDrawList().AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize(),
            startScreen,
            ImGui.GetColorU32(ImGuiCol.Text),
            text,
            contentWidth);
    }

    private static void DrawRecurrenceLine(ScheduledEventResponse schedule)
    {
        var icon = schedule.Recurrence != ScheduleRecurrence.Once
            ? FontAwesomeIcon.Sync
            : FontAwesomeIcon.CalendarAlt;

        var colour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        var text = ScheduleRecurrence.Describe(
            schedule.Recurrence, schedule.ScheduledFor, schedule.RecurrenceDay, schedule.RecurrenceWeek);

        using var pushed = ImRaii.PushColor(ImGuiCol.Text, colour);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(icon.ToIconString());

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.TextUnformatted(text);
    }

    private void DrawCardButtons(ScheduledEventResponse schedule, float rowWidth, bool mine)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var frameHeight = ImGui.GetFrameHeight();
        var buttonCount = mine ? 4 : 1;
        var buttonWidth = (rowWidth - spacing * (buttonCount - 1)) / buttonCount;
        var buttonSize = new Vector2(buttonWidth, frameHeight);

        using var buttonColours = ImRaii.PushColor(ImGuiCol.Button, ThemeColours.ButtonNormal(_config.PrimaryColour))
            .Push(ImGuiCol.ButtonHovered, ThemeColours.ButtonHovered(_config.PrimaryColour))
            .Push(ImGuiCol.ButtonActive, ThemeColours.ButtonPressed(_config.PrimaryColour));

        if (UIHelper.IconTextButton(FontAwesomeIcon.InfoCircle, "Details", buttonSize, "##upcomingInfo"))
        {
            _infoPopupId = schedule.Id;
            _openInfoRequested = true;
        }

        if (!mine)
            return;

        ImGui.SameLine();

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, "Start", buttonSize, "##upcomingStart"))
                RequestStart(schedule);
        }

        ImGui.SameLine();

        using (UIHelper.PushAmberButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Pen, "Edit", buttonSize, "##upcomingEdit"))
                OpenEdit(schedule);
        }

        ImGui.SameLine();

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", buttonSize, "##upcomingCancel"))
            {
                _pendingCancelId = schedule.Id;
                _openCancelRequested = true;
            }
        }
    }

    private void RequestStart(ScheduledEventResponse schedule)
    {
        var current = _currentCharacterName;

        if (!string.IsNullOrEmpty(current)
            && !string.Equals(schedule.CharacterName, current, StringComparison.Ordinal))
        {
            _pendingStartId = schedule.Id;
            _openStartWarningRequested = true;
            return;
        }

        StartNow(schedule.Id);
    }

    private void StartNow(string scheduleId)
    {
        _ = Task.Run(() => _scheduledSessions.StartNowAsync(scheduleId));
    }

    private void DrawStartWarningPopup()
    {
        if (_openStartWarningRequested)
        {
            ImGui.OpenPopup(StartWarningPopupId);
            _openStartWarningRequested = false;
        }

        using var popup = ImRaii.Popup(StartWarningPopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        var schedule = _schedules.FirstOrDefault(s => s.Id == _pendingStartId);
        var current = _currentCharacterName;

        if (schedule == null || string.IsNullOrEmpty(current))
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        ImGui.TextUnformatted($"This session was scheduled on {EventCardRenderer.FormatDisplayName(schedule.CharacterName)}.");
        ImGui.TextColored(SoftRed, $"It will go live under {EventCardRenderer.FormatDisplayName(current)} instead.");

        ImGuiHelpers.ScaledDummy(6f);

        var id = schedule.Id;

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, "Start it anyway", "##upcomingStartAnyway"))
            {
                StartNow(id);
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Back", "##upcomingStartBack"))
            ImGui.CloseCurrentPopup();
    }

    private void OpenEdit(ScheduledEventResponse schedule)
    {
        _editScheduleId = schedule.Id;
        _openEditRequested = true;
        _editError = string.Empty;

        _editOriginalTime = schedule.ScheduledFor;
        _editOriginalLocation = schedule.Location;
        _editOriginalDescription = schedule.Description;
        _editOriginalRecurrence = schedule.Recurrence;

        _editPicker.Reset(schedule.ScheduledFor);
        _editLocation = schedule.Location;
        _editDescription = schedule.Description;
        _editRecurrenceIndex = Math.Max(0, Array.IndexOf(ScheduleRecurrence.All, schedule.Recurrence));
    }

    private void DrawEditPopup()
    {
        if (_openEditRequested)
        {
            ImGui.OpenPopup(EditPopupId);
            _openEditRequested = false;
        }

        using var popup = ImRaii.Popup(EditPopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        if (_editScheduleId == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;

        ImGui.TextColored(_config.SecondaryColour, "Edit Scheduled Session");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        _editPicker.Draw("##upcomingEditPicker", _config.SecondaryColour);

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        HostField.Label("Location");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##upcomingEditLocation", ref _editLocation, EditLocationMaxLength);
        DrawCounter(_editLocation.Length, EditLocationMaxLength);

        ImGuiHelpers.ScaledDummy(6f);

        HostField.Label("Description");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextMultiline("##upcomingEditDescription", ref _editDescription, EditDescriptionMaxLength,
            new Vector2(0f, 70f * scale));
        DrawCounter(_editDescription.Length, EditDescriptionMaxLength);

        ImGuiHelpers.ScaledDummy(6f);

        HostField.Label("Repeats");
        ImGui.SetNextItemWidth(200f * scale);
        using (var combo = ImRaii.Combo("##upcomingEditRecurrence", ScheduleRecurrence.Labels[_editRecurrenceIndex]))
        {
            if (combo)
            {
                for (var i = 0; i < ScheduleRecurrence.Labels.Length; i++)
                {
                    if (ImGui.Selectable(ScheduleRecurrence.Labels[i], _editRecurrenceIndex == i))
                        _editRecurrenceIndex = i;
                }
            }
        }

        ImGuiHelpers.ScaledDummy(8f);

        if (!string.IsNullOrEmpty(_editError))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 420f * scale);
            ImGui.TextColored(SoftRed, _editError);
            ImGui.PopTextWrapPos();
            ImGuiHelpers.ScaledDummy(4f);
        }

        using (ImRaii.Disabled(_isSaving))
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Save, _isSaving ? "Saving..." : "Save Changes", "##upcomingEditSave"))
                TriggerEditSave();
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(_isSaving))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Cancel", "##upcomingEditBack"))
                ImGui.CloseCurrentPopup();
        }
    }

    private static void DrawCounter(int length, int max)
    {
        ImGui.TextColored(
            length >= max ? SoftRed : new Vector4(0.5f, 0.5f, 0.5f, 1f),
            $"{length} / {max}");
    }

    private void TriggerEditSave()
    {
        var scheduleId = _editScheduleId;
        if (scheduleId == null)
            return;

        var scheduledFor = _editPicker.Value;
        var timeChanged = scheduledFor != _editOriginalTime;

        if (timeChanged && scheduledFor < DateTime.UtcNow.AddMinutes(5))
        {
            _editError = "Pick a time at least 5 minutes from now.";
            return;
        }

        if (timeChanged && scheduledFor > DateTime.UtcNow.AddDays(365))
        {
            _editError = "Pick a time within the next year.";
            return;
        }

        var location = _editLocation.Trim();
        if (location.Length == 0)
        {
            _editError = "Enter where the session will be held.";
            return;
        }

        if (UserTextGuard.ContainsDisallowedContent(location))
        {
            _editError = "Location must not contain URLs or HTML.";
            return;
        }

        var description = _editDescription.Trim();
        if (UserTextGuard.ContainsDisallowedContent(description))
        {
            _editError = "Description must not contain URLs or HTML.";
            return;
        }

        var recurrence = ScheduleRecurrence.All[_editRecurrenceIndex];

        var request = new PutScheduledEventRequest
        {
            ScheduledFor = timeChanged ? scheduledFor : null,
            Location = location == _editOriginalLocation ? null : location,
            Description = description == _editOriginalDescription ? null : description,
            Recurrence = recurrence == _editOriginalRecurrence ? null : recurrence,
        };

        if (!request.HasChanges)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        _editError = string.Empty;
        _isSaving = true;

        _ = Task.Run(async () =>
        {
            var error = await _scheduledSessions.EditAsync(scheduleId, request);
            _isSaving = false;

            if (error != null)
            {
                _editError = error;
                return;
            }

            _editScheduleId = null;
            TriggerRefresh();
        });
    }

    private void DrawCancelPopup()
    {
        if (_openCancelRequested)
        {
            ImGui.OpenPopup(CancelPopupId);
            _openCancelRequested = false;
        }

        using var popup = ImRaii.Popup(CancelPopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        var schedule = _schedules.FirstOrDefault(s => s.Id == _pendingCancelId);
        if (schedule == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var recurring = schedule.Recurrence != ScheduleRecurrence.Once;

        ImGui.TextUnformatted(recurring
            ? "Cancel this occurrence, or the whole repeating series?"
            : "Cancel this scheduled session?");

        ImGuiHelpers.ScaledDummy(6f);

        var id = schedule.Id;

        using (UIHelper.PushAmberButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.CalendarMinus, recurring ? "This occurrence" : "Yes, cancel it", "##upcomingCancelOne"))
            {
                _ = Task.Run(async () =>
                {
                    await _scheduledSessions.CancelAsync(id);
                    TriggerRefresh();
                });
                ImGui.CloseCurrentPopup();
            }
        }

        if (recurring)
        {
            ImGui.SameLine();

            using (UIHelper.PushRedButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.TrashAlt, "The whole series", "##upcomingCancelSeries"))
                {
                    _ = Task.Run(async () =>
                    {
                        await _scheduledSessions.DeleteSeriesAsync(id);
                        TriggerRefresh();
                    });
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Keep it", "##upcomingCancelBack"))
            ImGui.CloseCurrentPopup();
    }

    private static Vector4 TimeColour(DateTime scheduledForUtc) =>
        ServerTime.IsWithinHour(scheduledForUtc)
            ? ThemeColours.ScheduledTime
            : ThemeColours.ScheduledTimeDistant;

    private static bool HasProfile(ScheduledEventResponse schedule) =>
        !string.IsNullOrWhiteSpace(schedule.ProfileImageUrl)
        || !string.IsNullOrWhiteSpace(schedule.Bio)
        || schedule.PreferredGames.Count > 0
        || schedule.Booster;

    private static CardEffectType GetCardEffect(ScheduledEventResponse schedule) =>
        CardEffectResolver.Resolve(schedule.CardEffectStyle, schedule.Booster);

    private static void DrawGameTypeBadge(ImDrawListPtr dl, ScheduledEventResponse schedule, Vector4 accent, Vector2 cardTopRight, float xInset, float yOffset)
    {
        if (string.IsNullOrWhiteSpace(schedule.Game))
            return;

        var textSize = ImGui.CalcTextSize(schedule.Game);
        var pos = new Vector2(cardTopRight.X - xInset - textSize.X, cardTopRight.Y + yOffset);

        dl.AddText(pos, ImGui.GetColorU32(accent), schedule.Game);
    }

    private void DrawAvatar(ScheduledEventResponse schedule, Vector2 pos, float size)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = size * 0.5f;

        var profileTex = !string.IsNullOrWhiteSpace(schedule.ProfileImageUrl)
            ? _imageService.GetFromUrl(schedule.ProfileImageUrl!)
            : null;

        if (profileTex != null)
        {
            CircleImage.DrawAt(dl, pos, size, profileTex);
            DrawBorderFrame(schedule, dl, pos, size);
            return;
        }

        var placeholder = _imageService.GetBundled("Icons/profileplaceholder.png");
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

        DrawBorderFrame(schedule, dl, pos, size);
    }

    private void DrawBorderFrame(ScheduledEventResponse schedule, ImDrawListPtr dl, Vector2 pos, float size)
    {
        var imagePath = AvatarBorder.ImagePath(schedule.BorderStyle, schedule.Booster);
        if (imagePath == null)
            return;

        var tex = _imageService.GetBundled(imagePath);
        if (tex == null)
            return;

        var centre = pos + new Vector2(size * 0.5f, size * 0.5f);
        var half = size * 0.58f;
        dl.AddImage(tex.Handle, centre - new Vector2(half, half), centre + new Vector2(half, half),
            Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
    }

    private void DrawVenueLogo(ImDrawListPtr dl, ScheduledEventResponse schedule, Vector2 cardTopRight, float topOffset, float logoSize)
    {
        var tex = !string.IsNullOrWhiteSpace(schedule.ImageUrl) ? _imageService.GetFromUrl(schedule.ImageUrl!) : null;
        if (tex == null)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var right = cardTopRight.X - pad;
        var top = cardTopRight.Y + topOffset;

        dl.AddImageRounded(tex.Handle,
            new Vector2(right - logoSize, top), new Vector2(right, top + logoSize),
            Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)), 4f * scale);
    }

    private static void DrawCardBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 accent, float rounding, bool emphasised)
    {
        var border = new Vector4(accent.X, accent.Y, accent.Z, emphasised ? 0.95f : 0.55f);
        var thickness = (emphasised ? 2.5f : 1.5f) * ImGuiHelpers.GlobalScale;
        dl.AddRect(min, max, ImGui.GetColorU32(border), rounding, ImDrawFlags.None, thickness);
    }

    private void DrawInfoPopup()
    {
        if (_openInfoRequested)
        {
            ImGui.OpenPopup(InfoPopupId);
            _openInfoRequested = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var schedule = _schedules.FirstOrDefault(s => s.Id == _infoPopupId);

        var cardEffect = schedule != null ? GetCardEffect(schedule) : CardEffectType.None;
        var (bgColour, accent) = schedule != null
            ? EventCardRenderer.GetGameTypeColors(schedule.Game)
            : (default, default);
        var cardBg = CardEffectResolver.BaseColour(cardEffect) ?? SolidCardBg(bgColour);
        var rounding = CardRounding * scale;

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var colBg = ImRaii.PushColor(ImGuiCol.PopupBg, cardBg, schedule != null);
        using var popup = ImRaii.Popup(InfoPopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        if (schedule == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.TextColored(_config.SecondaryColour, EventCardRenderer.FormatDisplayName(schedule.CharacterName));

        if (!string.IsNullOrWhiteSpace(schedule.VenueName) && schedule.VenueName != "No Venue")
            ImGui.TextDisabled($"@ {schedule.VenueName}");

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2f);

        using (ImRaii.PushColor(ImGuiCol.Text, TimeColour(schedule.ScheduledFor)))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(FontAwesomeIcon.CalendarAlt.ToIconString());

            ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.TextUnformatted($"{ServerTime.FormatServerTime(schedule.ScheduledFor)}  {ServerTime.FormatCountdown(schedule.ScheduledFor)}");
        }

        DrawRecurrenceLine(schedule);

        if (!string.IsNullOrWhiteSpace(schedule.Description))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (InfoPopupMaxWidth - 28f) * scale);
            ImGui.TextWrapped(schedule.Description);
            ImGui.PopTextWrapPos();
        }

        ImGuiHelpers.ScaledDummy(6f);
        DrawExpandedDetails(schedule);
        ImGuiHelpers.ScaledDummy(8f);

        if (ImGui.Button("Close##upcomingPopupClose"))
            ImGui.CloseCurrentPopup();

        var p0 = ImGui.GetWindowPos();
        var sz = ImGui.GetWindowSize();

        if (!string.IsNullOrWhiteSpace(schedule.Game))
        {
            var wp = ImGui.GetStyle().WindowPadding;
            var badgeSize = ImGui.CalcTextSize(schedule.Game);
            dl.AddText(new Vector2(p0.X + sz.X - wp.X - badgeSize.X, p0.Y + wp.Y),
                ImGui.GetColorU32(accent), schedule.Game);
        }

        dl.ChannelsSetCurrent(0);
        dl.PushClipRect(new Vector2(-10000f, -10000f), new Vector2(100000f, 100000f), false);
        var t = ImGui.GetTime();
        if (cardEffect != CardEffectType.None)
            CardEffectDrawer.DrawFoil(dl, cardEffect, p0, p0 + sz, t);

        if (CardEffectDrawer.HasCustomBorder(cardEffect))
            CardEffectDrawer.DrawBorder(dl, cardEffect, p0, p0 + sz, rounding, t);
        else
            DrawCardBorder(dl, p0, p0 + sz, accent, rounding, false);
        dl.PopClipRect();
        dl.ChannelsMerge();
    }

    private void DrawExpandedDetails(ScheduledEventResponse schedule)
    {
        var scale = ImGuiHelpers.GlobalScale;

        using (ImRaii.Group())
        {
            if (schedule.Rules.Count > 0)
            {
                using var rulesTable = ImRaii.Table("##upcomingRules", 2, ImGuiTableFlags.SizingFixedFit);
                if (rulesTable)
                {
                    ImGui.TableSetupColumn("##rk", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthFixed);

                    var disabledColour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
                    foreach (var rule in schedule.Rules)
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
                ImGui.TextDisabled("Rules are announced when");
                ImGui.TextDisabled("the session goes live.");
            }
        }

        ImGui.SameLine(0f, 24f * scale);

        using (ImRaii.Group())
        {
            ImGui.TextDisabled("Location");
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(schedule.Location)
                ? "To be confirmed"
                : schedule.Location);

            if (!string.IsNullOrWhiteSpace(schedule.DiscordUrl))
            {
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextDisabled("Discord");
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 1f, 1f)))
                    ImGui.TextUnformatted(schedule.DiscordUrl!);

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip($"Open in browser:\n{schedule.DiscordUrl}");
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    OpenBrowser.TryOpen(schedule.DiscordUrl!);
            }
        }
    }

    private void DrawProfilePopup()
    {
        var schedule = _schedules.FirstOrDefault(s => s.Id == _profilePopupId);
        var data = schedule == null
            ? null
            : new ProfilePopup.Data
            {
                DisplayName = EventCardRenderer.FormatDisplayName(schedule.CharacterName),
                ProfileImageUrl = schedule.ProfileImageUrl,
                Bio = schedule.Bio,
                PreferredGames = schedule.PreferredGames,
                Booster = schedule.Booster,
                BorderStyle = schedule.BorderStyle,
                CardEffectStyle = schedule.CardEffectStyle,
            };

        ProfilePopup.Draw(ProfilePopupId, ref _openProfileRequested, _imageService, _config, data);
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

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
