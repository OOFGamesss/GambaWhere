using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GambaWhere.Config;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.UI.Components;
using GambaWhere.Utility;
using static GambaWhere.Utility.ThemeColours;

namespace GambaWhere.UI;

/// <summary>Floating overlay pill showing the active session at a glance.</summary>
public class SessionPillOverlay : Window, IDisposable
{
    private readonly HostSessions _sessions;
    private readonly Configuration _config;
    private readonly SessionService _sessionService;

    private Vector2 _trackedPos;
    private Vector2 _currentWindowSize = new(320f, 36f);
    private bool _pendingReset;
    private Vector2 _pendingResetPos;
    private int _pushedStyleVars;
    private int _pushedStyleColours;
    private bool _expanded;

    public bool IsMoving { get; private set; }

    public SessionPillOverlay(HostSessions sessions, Configuration config, SessionService sessionService)
        : base("##GambaWherePill",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings)
    {
        _sessions = sessions;
        _config = config;
        _sessionService = sessionService;

        RespectCloseHotkey = false;
        Position = new Vector2(_config.PillPositionX, _config.PillPositionY);
        PositionCondition = ImGuiCond.Always;
    }

    public void EnterMoveMode() => IsMoving = true;

    public void ExitMoveMode()
    {
        IsMoving = false;
        _config.PillPositionX = _trackedPos.X;
        _config.PillPositionY = _trackedPos.Y;
        _config.Save();
    }

    public void ResetPosition()
    {
        var display = ImGui.GetIO().DisplaySize;
        var centre = (display - _currentWindowSize) / 2f;

        _pendingResetPos = centre;
        _pendingReset = true;

        _config.PillPositionX = centre.X;
        _config.PillPositionY = centre.Y;
        _config.Save();
    }

    public override void PreDraw()
    {
        _pushedStyleVars = 0;
        _pushedStyleColours = 0;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 20f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f);
        _pushedStyleVars = 3;

        var primary = _sessions.Primary();
        var borderColour = GameTypeColours.PillBorderForGame(primary?.GameType);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, TintedWindowBg(_config.PrimaryColour));
        ImGui.PushStyleColor(ImGuiCol.Border, borderColour);
        _pushedStyleColours = 2;

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
            Position = new Vector2(_config.PillPositionX, _config.PillPositionY);
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

        var sessions = _sessions.Snapshot();
        var primary = _sessions.Primary();

        using (ImRaii.Disabled(IsMoving))
        {
            DrawTimer(primary);
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();
            DrawSummaryLabel(sessions, primary);
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();

            if (sessions.Count > 1)
                DrawExpandButton();
            else
                DrawPauseButton(primary);

            ImGui.SameLine();
            DrawStopButton();

            if (_expanded && sessions.Count > 1)
                DrawSessionRows(sessions);
        }

        DrawStopConfirmPopup();
    }

    private static void DrawTimer(SessionState? primary)
    {
        if (primary == null)
        {
            ImGui.TextUnformatted(TimeSpan.Zero.ToString(@"hh\:mm\:ss"));
            return;
        }

        if (primary.AutoEndAt.HasValue)
        {
            var remaining = primary.AutoEndAt.Value - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            ImGui.TextUnformatted(remaining.ToString(@"hh\:mm\:ss"));
            return;
        }

        ImGui.TextUnformatted(primary.Elapsed().ToString(@"hh\:mm\:ss"));
    }

    private static void DrawSummaryLabel(IReadOnlyList<SessionState> sessions, SessionState? primary)
    {
        var label = sessions.Count switch
        {
            0 => "Hosting Example Game",
            1 => $"Hosting {primary!.GameType}",
            _ => $"Hosting {sessions.Count} Games"
        };

        ImGui.TextUnformatted(label);
    }

    private void DrawExpandButton()
    {
        var icon = _expanded ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        using var small = PushSmallButtonScale();
        if (ImGuiComponents.IconButton("##PillExpand", icon))
            _expanded = !_expanded;
        ImGui.SetWindowFontScale(1.0f);
    }

    private void DrawPauseButton(SessionState? session)
    {
        var icon = session is { IsPaused: true } ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        using var small = PushSmallButtonScale();
        if (ImGuiComponents.IconButton("##PillPause", icon) && session != null)
        {
            var eventId = session.EventId;
            _ = Task.Run(() => _sessionService.TogglePauseAsync(eventId));
        }
        ImGui.SetWindowFontScale(1.0f);
    }

    private static IDisposable PushSmallButtonScale()
    {
        const float scale = 0.65f;
        const float vPad = 3f;
        var pad = ImGui.GetTextLineHeight() * (1f - scale) / 2f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, pad - vPad) + 2f);
        var framePad = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(vPad + 0.2f, vPad));
        ImGui.SetWindowFontScale(scale);
        return framePad;
    }

    private void DrawSessionRows(IReadOnlyList<SessionState> sessions)
    {
        ImGui.Separator();

        foreach (var session in sessions)
        {
            using var id = ImRaii.PushId(session.EventId);

            ImGui.TextUnformatted(session.Elapsed().ToString(@"hh\:mm\:ss"));
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();

            if (session.IsPaused)
                ImGui.TextDisabled(session.GameType);
            else
                ImGui.TextUnformatted(session.GameType);

            ImGui.SameLine();

            var pauseIcon = session.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
            using (PushSmallButtonScale())
            {
                if (ImGuiComponents.IconButton("##RowPause", pauseIcon))
                {
                    var eventId = session.EventId;
                    _ = Task.Run(() => _sessionService.TogglePauseAsync(eventId));
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButton("##RowStop", FontAwesomeIcon.Stop))
                {
                    var eventId = session.EventId;
                    _ = Task.Run(() => _sessionService.StopSessionAsync(eventId));
                }
            }
            ImGui.SetWindowFontScale(1.0f);
        }
    }

    private void DrawStopButton()
    {
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1f);
        if (ImGui.SmallButton("Stop##PillStop"))
            ImGui.OpenPopup("##GWStopConfirm");
    }

    private void DrawStopConfirmPopup()
    {
        using var popup = ImRaii.Popup("##GWStopConfirm");
        if (!popup.Success)
            return;

        var count = _sessions.Count;
        ImGui.TextUnformatted(count > 1 ? $"End all {count} sessions?" : "End the current session?");
        ImGui.Spacing();

        var label = count > 1 ? "Yes, stop them all" : "Yes, stop session";
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, label, "##YesStop"))
        {
            _ = Task.Run(() => _sessionService.StopAllSessionsAsync());
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##CancelStop"))
            ImGui.CloseCurrentPopup();
    }

    public void Dispose() { }
}
