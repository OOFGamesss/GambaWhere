using System;
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

public class SessionPillOverlay : Window, IDisposable
{
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly SessionService _sessionService;

    private Vector2 _trackedPos;
    private Vector2 _currentWindowSize = new(320f, 36f);
    private bool _pendingReset;
    private Vector2 _pendingResetPos;
    private int _pushedStyleVars;
    private int _pushedStyleColours;

    public bool IsMoving { get; private set; }

    public SessionPillOverlay(SessionState sessionState, Configuration config, SessionService sessionService)
        : base("##GambaWherePill",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar |
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings)
    {
        _sessionState = sessionState;
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

        var borderColour = _sessionState.IsActive
            ? GameTypeColours.PillBorderForGame(_sessionState.GameType)
            : GameTypeColours.PillBorderForGame(null);

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

        using (ImRaii.Disabled(IsMoving))
        {
            DrawTimer();
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();
            DrawGameLabel();
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();
            DrawPauseButton();
            ImGui.SameLine();
            DrawStopButton();
        }

        DrawStopConfirmPopup();
    }

    private void DrawTimer()
    {
        if (_sessionState.IsActive && _sessionState.AutoEndAt.HasValue)
        {
            var remaining = _sessionState.AutoEndAt.Value - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            ImGui.TextUnformatted(remaining.ToString(@"hh\:mm\:ss"));
            return;
        }

        TimeSpan elapsed;
        if (!_sessionState.IsActive || !_sessionState.StartedAt.HasValue)
        {
            elapsed = TimeSpan.Zero;
        }
        else if (_sessionState.IsPaused && _sessionState.PausedAt.HasValue)
        {
            elapsed = _sessionState.PausedAt.Value - _sessionState.StartedAt.Value - _sessionState.TotalPausedDuration;
        }
        else
        {
            elapsed = DateTime.UtcNow - _sessionState.StartedAt.Value - _sessionState.TotalPausedDuration;
        }

        ImGui.TextUnformatted(elapsed.ToString(@"hh\:mm\:ss"));
    }

    private void DrawGameLabel()
    {
        var label = _sessionState.IsActive
            ? $"Hosting {_sessionState.GameType}"
            : "Hosting Example Game";

        ImGui.TextUnformatted(label);
    }

    private void DrawPauseButton()
    {
        var icon = _sessionState.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        const float scale = 0.65f;
        const float vPad = 3f;
        var pad = ImGui.GetTextLineHeight() * (1f - scale) / 2f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, pad - vPad) + 2f);
        using var framePad = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(vPad + 0.2f, vPad));
        ImGui.SetWindowFontScale(scale);
        if (ImGuiComponents.IconButton("##PillPause", icon))
            _ = Task.Run(() => _sessionService.TogglePauseAsync());
        ImGui.SetWindowFontScale(1.0f);
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

        ImGui.TextUnformatted("End the current session?");
        ImGui.Spacing();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Yes, stop session", "##YesStop"))
        {
            _ = Task.Run(() => _sessionService.StopSessionAsync());
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##CancelStop"))
            ImGui.CloseCurrentPopup();
    }

    public void Dispose() { }
}
