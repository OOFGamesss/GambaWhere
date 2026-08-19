using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GambaWhere.Config;
using GambaWhere.Models;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI;

/// <summary>Reminder overlay that appears near the top right when one of the host's own scheduled sessions is due, offering to start it now or cancel it.</summary>
public class ScheduledSessionOverlay : Window, IDisposable
{
    private const float DefaultRightInset = 24f;
    private const float DefaultTopInset = 60f;
    private const float PanelWidth = 320f;
    private const float BackgroundOpacity = 0.82f;
    private const float ShellPadding = 6f;
    private const float CardPadding = 12f;
    private const float CardRounding = 8f;
    private const float CardBorderThickness = 1.5f;
    private const float CardGap = 8f;

    private readonly ScheduledSessionService _scheduledSessions;
    private readonly Configuration _config;

    private Vector2 _trackedPos;
    private Vector2 _currentWindowSize = new(PanelWidth, 140f);
    private bool _pendingReset;
    private Vector2 _pendingResetPos;
    private int _pushedStyleVars;
    private int _pushedStyleColours;
    private string? _confirmCancelId;
    private bool _openCancelConfirmRequested;

    public bool IsMoving { get; private set; }

    public ScheduledSessionOverlay(ScheduledSessionService scheduledSessions, Configuration config)
        : base("##GambaWhereScheduledReminder",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings)
    {
        _scheduledSessions = scheduledSessions;
        _config = config;

        RespectCloseHotkey = false;
        PositionCondition = ImGuiCond.Always;
    }

    public void EnterMoveMode() => IsMoving = true;

    public void ExitMoveMode()
    {
        IsMoving = false;
        _config.ScheduledOverlayX = _trackedPos.X;
        _config.ScheduledOverlayY = _trackedPos.Y;
        _config.Save();
    }

    public void ResetPosition()
    {
        var corner = DefaultCorner();

        _pendingResetPos = corner;
        _pendingReset = true;

        _config.ScheduledOverlayX = corner.X;
        _config.ScheduledOverlayY = corner.Y;
        _config.Save();
    }

    private Vector2 DefaultCorner()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var viewportPos = ImGuiHelpers.MainViewport.Pos;
        var viewportSize = ImGuiHelpers.MainViewport.Size;
        var width = Math.Max(_currentWindowSize.X, PanelWidth * scale);

        return new Vector2(
            viewportPos.X + viewportSize.X - width - DefaultRightInset * scale,
            viewportPos.Y + DefaultTopInset * scale);
    }

    private Vector2 SavedOrDefaultPosition() =>
        _config.ScheduledOverlayX < 0f || _config.ScheduledOverlayY < 0f
            ? DefaultCorner()
            : new Vector2(_config.ScheduledOverlayX, _config.ScheduledOverlayY);

    public override void PreDraw()
    {
        _pushedStyleVars = 0;
        _pushedStyleColours = 0;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ShellPadding, ShellPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        _pushedStyleVars = 3;

        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        _pushedStyleColours = 1;

        if (_pendingReset)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
            Position = _pendingResetPos;
            PositionCondition = ImGuiCond.Always;
            _trackedPos = _pendingResetPos;
            _pendingReset = false;
        }
        else if (IsMoving)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
            Position = null;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
            Position = SavedOrDefaultPosition();
            PositionCondition = ImGuiCond.Always;
        }
    }

    public override void PostDraw()
    {
        if (_pushedStyleColours > 0)
            ImGui.PopStyleColor(_pushedStyleColours);

        if (_pushedStyleVars > 0)
            ImGui.PopStyleVar(_pushedStyleVars);
    }

    public override void Draw()
    {
        _trackedPos = ImGui.GetWindowPos();
        _currentWindowSize = ImGui.GetWindowSize();

        var due = _scheduledSessions.Due();

        using (ImRaii.Disabled(IsMoving))
        {
            if (due.Count == 0)
            {
                DrawPreview();
            }
            else
            {
                for (var i = 0; i < due.Count; i++)
                {
                    if (i > 0)
                        ImGuiHelpers.ScaledDummy(CardGap);

                    DrawDueSession(due[i]);
                }
            }
        }

        DrawCancelConfirmPopup();
    }

    private void DrawPreview()
    {
        var accent = ThemeColours.AccentText(_config.SecondaryColour);

        DrawCard(accent, () =>
        {
            using (ImRaii.PushColor(ImGuiCol.Text, accent))
                ImGui.TextUnformatted("Example Game is due");

            ImGui.TextUnformatted("Sat 1 Jan, 20:00 ST  in 15m");
            ImGui.TextDisabled("The Goblet, Ward 12 Plot 30");
        });
    }

    private static void DrawCard(Vector4 accent, Action drawContent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPadding * scale;
        var rounding = CardRounding * scale;
        var cardWidth = PanelWidth * scale;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var cardMin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(cardMin + new Vector2(pad, pad));

        using (ImRaii.Group())
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cardWidth - pad * 2f);
            drawContent();
            ImGui.PopTextWrapPos();
        }

        var contentMax = ImGui.GetItemRectMax();
        var cardMax = new Vector2(
            Math.Max(cardMin.X + cardWidth, contentMax.X + pad),
            contentMax.Y + pad);

        var background = ThemeColours.TintedWindowBg(accent);
        background.W *= BackgroundOpacity;

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(cardMin, cardMax, ImGui.GetColorU32(background), rounding);
        drawList.AddRect(cardMin, cardMax, ImGui.GetColorU32(accent), rounding, ImDrawFlags.None, CardBorderThickness * scale);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMax.Y));
        ImGui.Dummy(new Vector2(cardMax.X - cardMin.X, 0f));
    }

    private void DrawDueSession(ScheduledSessionSnapshot session)
    {
        using var id = ImRaii.PushId(session.Id);

        var (_, accent) = EventCardRenderer.GetGameTypeColors(session.GameType ?? string.Empty);

        DrawCard(accent, () => DrawDueSessionBody(session, accent));
    }

    private void DrawDueSessionBody(ScheduledSessionSnapshot session, Vector4 accent)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, accent))
            ImGui.TextUnformatted($"{session.GameType ?? "Session"} is due");

        if (!string.IsNullOrWhiteSpace(session.VenueName) && session.VenueName != "No Venue")
            ImGui.TextDisabled($"@ {session.VenueName}");

        using (ImRaii.PushColor(ImGuiCol.Text, ThemeColours.ScheduledTime))
            ImGui.TextUnformatted($"{ServerTime.FormatServerTime(session.ScheduledForUtc)}  {ServerTime.FormatCountdown(session.ScheduledForUtc)}");

        DrawRecurrenceLine(session);

        if (!string.IsNullOrWhiteSpace(session.Location))
            ImGui.TextDisabled(session.Location);

        ImGuiHelpers.ScaledDummy(6f);

        var sessionId = session.Id;

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, "Start Now", "##gw_sched_start"))
                _ = Task.Run(() => _scheduledSessions.StartNowAsync(sessionId));
        }

        ImGui.SameLine();

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##gw_sched_cancel"))
            {
                _confirmCancelId = sessionId;
                _openCancelConfirmRequested = true;
            }
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.EyeSlash, "Dismiss", "##gw_sched_dismiss"))
            _scheduledSessions.Dismiss(sessionId);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hide this reminder without changing the schedule.");
    }

    private static void DrawRecurrenceLine(ScheduledSessionSnapshot session)
    {
        var icon = session.Recurrence != ScheduleRecurrence.Once
            ? FontAwesomeIcon.Sync
            : FontAwesomeIcon.CalendarAlt;

        var colour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        var text = ScheduleRecurrence.Describe(
            session.Recurrence, session.ScheduledForUtc, session.RecurrenceDay, session.RecurrenceWeek);

        using var pushed = ImRaii.PushColor(ImGuiCol.Text, colour);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(icon.ToIconString());

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.TextUnformatted(text);
    }

    private void DrawCancelConfirmPopup()
    {
        const string popupId = "##gw_sched_cancel_confirm";

        if (_openCancelConfirmRequested)
        {
            ImGui.OpenPopup(popupId);
            _openCancelConfirmRequested = false;
        }

        using var popup = ImRaii.Popup(popupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
        {
            _confirmCancelId = null;
            return;
        }

        var sessionId = _confirmCancelId;
        if (sessionId == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        ImGui.TextUnformatted("Cancel this session?");
        ImGui.TextDisabled("A repeating session moves to its next slot. A one off is removed.");
        ImGuiHelpers.ScaledDummy(6f);

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Yes, cancel it", "##gw_sched_cancel_yes"))
            {
                _ = Task.Run(() => _scheduledSessions.CancelAsync(sessionId));
                _confirmCancelId = null;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Keep it", "##gw_sched_cancel_no"))
        {
            _confirmCancelId = null;
            ImGui.CloseCurrentPopup();
        }
    }

    public void Dispose() { }
}
