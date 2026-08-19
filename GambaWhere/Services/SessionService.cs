using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Models;
using GambaWhere.Config;
using GambaWhere.Discord;
using GambaWhere.State;

namespace GambaWhere.Services;

/// <summary>Manages the lifecycle of every hosting session, including start, pause, stop, heartbeat, and crash recovery.</summary>
public class SessionService : IDisposable
{
    public const string BreakMessage = "Gamba Host is currently on a break!";

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

    private readonly GambaWhereClient _client;
    private readonly PlayerInfoService _playerInfo;
    private readonly HostSessions _sessions;
    private readonly Configuration _config;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly WebhookService _discordWebhook;
    private readonly IChatGui _chatGui;

    private readonly object _saveGate = new();
    private readonly object _loopGate = new();

    private CancellationTokenSource? _heartbeatCts;

    public Func<string, string, Dictionary<string, object>?>? RefreshAutomaticRulesFromIpc { get; set; }

    public SessionService(
        GambaWhereClient client,
        PlayerInfoService playerInfo,
        HostSessions sessions,
        Configuration config,
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        WebhookService discordWebhook,
        IChatGui chatGui)
    {
        _client = client;
        _playerInfo = playerInfo;
        _sessions = sessions;
        _config = config;
        _clientState = clientState;
        _framework = framework;
        _log = log;
        _discordWebhook = discordWebhook;
        _chatGui = chatGui;

        _clientState.TerritoryChanged += OnTerritoryChanged;
        _clientState.Login += OnLogin;

        if (_playerInfo.IsLoggedIn && _config.ActiveSessions.Count > 0)
            _ = Task.Run(TryRecoverSessionsAsync);
    }

    public async Task<(string? Error, EventCreateResponse? Created)> StartSessionAsync(
        PostEventRequest request,
        DateTime? autoEndAt = null,
        string? automaticRuleSourceName = null)
    {
        if (_sessions.AtCapacity)
            return ($"You are already hosting {HostSessions.MaxConcurrent} sessions.", null);

        var (response, error) = await _client.PostEventAsync(request);
        if (response == null)
            return (error ?? "Failed to create session. Check the log for details.", null);

        var session = new SessionState
        {
            IsActive = true,
            EventId = response.Id,
            SessionToken = response.SessionToken,
            CharacterName = response.CharacterName,
            Location = response.Location,
            GameType = request.Game,
            VenueName = request.VenueName,
            ActiveRules = request.Rules,
            DiscordUrl = response.DiscordUrl,
            ImageUrl = response.ImageUrl,
            Description = request.Description,
            StartedAt = DateTime.UtcNow,
            AutoEndAt = autoEndAt,
            AutomaticRuleSourceName = automaticRuleSourceName,
        };

        _sessions.Add(session);
        SaveSessionSnapshots();

        EnsureHeartbeatLoop();
        StartAutoEndTask(session);

        QueueDiscordWebhook(() => _discordWebhook.SyncActiveSessionEmbedsAsync());

        return (null, response);
    }

    private void EnsureHeartbeatLoop()
    {
        lock (_loopGate)
        {
            if (_heartbeatCts != null)
                return;

            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;
            _ = Task.Run(() => RunHeartbeatLoopAsync(token));
        }
    }

    private void StopHeartbeatLoopIfIdle()
    {
        lock (_loopGate)
        {
            if (_sessions.AnyActive || _heartbeatCts == null)
                return;

            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }
    }

    private void StartAutoEndTask(SessionState session)
    {
        if (!session.AutoEndAt.HasValue)
            return;

        var cts = new CancellationTokenSource();
        session.AutoEndCts = cts;
        _ = Task.Run(() => RunAutoEndAsync(session, session.AutoEndAt.Value, cts.Token));
    }

    private static void CancelAutoEndTask(SessionState session)
    {
        session.AutoEndCts?.Cancel();
        session.AutoEndCts?.Dispose();
        session.AutoEndCts = null;
    }

    public async Task TogglePauseAsync(string eventId)
    {
        var session = _sessions.Find(eventId);
        if (session == null)
            return;

        if (session.IsPaused)
            await ResumeSessionAsync(eventId);
        else
            await PauseSessionAsync(eventId);
    }

    public async Task PauseSessionAsync(string eventId)
    {
        var session = _sessions.Find(eventId);
        if (session == null || !session.IsActive || session.IsPaused)
            return;

        session.IsPaused = true;
        session.PausedAt = DateTime.UtcNow;

        var putRequest = new PutEventRequest
        {
            Location = session.Location,
            Rules = session.ActiveRules,
            Description = BreakMessage,
            BoosterKey = BoosterKeyForRequest()
        };
        await _client.PutEventAsync(session.EventId, session.SessionToken, putRequest);

        SaveSessionSnapshots();
        QueueDiscordWebhook(() => _discordWebhook.SyncActiveSessionEmbedsAsync());
    }

    public async Task ResumeSessionAsync(string eventId)
    {
        var session = _sessions.Find(eventId);
        if (session == null || !session.IsActive || !session.IsPaused)
            return;

        if (session.PausedAt.HasValue)
            session.TotalPausedDuration += DateTime.UtcNow - session.PausedAt.Value;

        session.IsPaused = false;
        session.PausedAt = null;

        var putRequest = new PutEventRequest
        {
            Location = session.Location,
            Rules = session.ActiveRules,
            Description = session.Description,
            BoosterKey = BoosterKeyForRequest()
        };
        await _client.PutEventAsync(session.EventId, session.SessionToken, putRequest);

        SaveSessionSnapshots();
        QueueDiscordWebhook(() => _discordWebhook.SyncActiveSessionEmbedsAsync());
    }

    public async Task StopSessionAsync(string eventId)
    {
        var session = _sessions.Find(eventId);
        if (session == null)
            return;

        CancelAutoEndTask(session);

        await _client.DeleteEventAsync(session.EventId, session.SessionToken);

        _sessions.Remove(eventId);
        SaveSessionSnapshots();
        StopHeartbeatLoopIfIdle();

        QueueDiscordWebhook(() => _sessions.AnyActive
            ? _discordWebhook.SyncActiveSessionEmbedsAsync()
            : _discordWebhook.PublishIdleEmbedsAsync());
    }

    public async Task StopAllSessionsAsync()
    {
        foreach (var session in _sessions.Snapshot())
        {
            CancelAutoEndTask(session);

            await _client.DeleteEventAsync(session.EventId, session.SessionToken);
            _sessions.Remove(session.EventId);
        }

        SaveSessionSnapshots();
        StopHeartbeatLoopIfIdle();

        QueueDiscordWebhook(() => _discordWebhook.PublishIdleEmbedsAsync());
    }

    private void OnLogin()
    {
        if (_config.ActiveSessions.Count > 0)
            _ = Task.Run(TryRecoverSessionsAsync);
    }

    private async Task TryRecoverSessionsAsync()
    {
        if (_sessions.AnyActive)
            return;

        var snapshots = _config.ActiveSessions.ToList();
        if (snapshots.Count == 0)
            return;

        var recovered = 0;
        foreach (var snapshot in snapshots)
        {
            if (await TryRecoverSessionAsync(snapshot))
                recovered++;
        }

        SaveSessionSnapshots();

        if (recovered == 0)
            return;

        var text = recovered == 1
            ? "Your previous hosting session has been recovered."
            : $"{recovered} previous hosting sessions have been recovered.";
        _chatGui.Print(new SeStringBuilder().AddText(text).Build(), "GambaWhere");

        QueueDiscordWebhook(() => _discordWebhook.SyncActiveSessionEmbedsAsync());
    }

    private async Task<bool> TryRecoverSessionAsync(ActiveSessionSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.SessionToken) || string.IsNullOrEmpty(snapshot.CharacterName))
            return false;

        var rules = TryDeserializeRules(snapshot.RulesJson);
        var putRequest = new PutEventRequest
        {
            Rules = rules,
            Description = snapshot.IsPaused ? BreakMessage : (snapshot.Description ?? string.Empty),
            BoosterKey = BoosterKeyForRequest()
        };

        var response = string.IsNullOrEmpty(snapshot.EventId)
            ? await _client.PutEventByCharacterAsync(snapshot.CharacterName, snapshot.SessionToken, putRequest)
            : await _client.PutEventAsync(snapshot.EventId, snapshot.SessionToken, putRequest);

        if (response == null || string.IsNullOrEmpty(response.Id))
            return false;

        var session = new SessionState
        {
            IsActive = true,
            EventId = response.Id,
            SessionToken = snapshot.SessionToken,
            CharacterName = snapshot.CharacterName,
            GameType = snapshot.GameType ?? string.Empty,
            VenueName = snapshot.VenueName,
            ActiveRules = rules,
            Description = snapshot.Description ?? string.Empty,
            Location = snapshot.Location ?? response.Location ?? string.Empty,
            StartedAt = snapshot.StartedAt,
            AutoEndAt = snapshot.AutoEndAt,
            IsPaused = snapshot.IsPaused,
            PausedAt = snapshot.PausedAt,
            TotalPausedDuration = TimeSpan.FromTicks(snapshot.TotalPausedDurationTicks),
            AutomaticRuleSourceName = snapshot.AutomaticRuleSourceName,
            DiscordUrl = response.DiscordUrl ?? snapshot.DiscordUrl,
            ImageUrl = response.ImageUrl ?? snapshot.ImageUrl,
        };

        _sessions.Add(session);

        EnsureHeartbeatLoop();

        if (session.AutoEndAt.HasValue && session.AutoEndAt.Value > DateTime.UtcNow)
            StartAutoEndTask(session);

        return true;
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        if (!_sessions.AnyActive)
            return;

        _ = PushLocationAsync();
    }

    private async Task PushLocationAsync() => await SendBatchHeartbeatAsync();

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval, ct);

                if (ct.IsCancellationRequested)
                    break;

                await SendBatchHeartbeatAsync(ct);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Unexpected error in the heartbeat loop.");
        }
    }

    private async Task SendBatchHeartbeatAsync(CancellationToken ct = default)
    {
        var sessions = _sessions.Snapshot();
        if (sessions.Count == 0)
            return;

        var location = await _framework.RunOnFrameworkThread(() => _playerInfo.GetCurrentLocation());
        if (location == null)
        {
            _log.Warning("Heartbeat skipped: could not resolve current territory.");
            return;
        }

        await RefreshAutomaticRulesAsync(sessions);

        var request = new PutEventsBatchRequest
        {
            Location = location,
            BoosterKey = BoosterKeyForRequest(),
            Sessions = sessions.Select(session => new PutEventsBatchItem
            {
                Id = session.EventId,
                SessionToken = session.SessionToken,
                Rules = session.ActiveRules,
                Description = session.IsPaused ? BreakMessage : session.Description,
            }).ToList()
        };

        var response = await _client.PutEventsBatchAsync(request, ct);
        if (response == null)
            return;

        foreach (var result in response.Results)
        {
            var session = _sessions.Find(result.Id);
            if (session == null)
                continue;

            if (result.IsGone)
            {
                CancelAutoEndTask(session);
                _sessions.Remove(result.Id);
                _log.Information("Session {Game} was dropped by the server ({Status}).", session.GameType, result.Status);
                continue;
            }

            session.Location = location;

            if (result.Event == null)
                continue;

            if (!string.IsNullOrWhiteSpace(result.Event.DiscordUrl))
                session.DiscordUrl = result.Event.DiscordUrl;

            if (!string.IsNullOrWhiteSpace(result.Event.ImageUrl))
                session.ImageUrl = result.Event.ImageUrl;
        }

        SaveSessionSnapshots();
        StopHeartbeatLoopIfIdle();

        QueueDiscordWebhook(() => _sessions.AnyActive
            ? _discordWebhook.SyncActiveSessionEmbedsAsync()
            : _discordWebhook.PublishIdleEmbedsAsync());
    }

    private async Task RefreshAutomaticRulesAsync(IReadOnlyList<SessionState> sessions)
    {
        if (RefreshAutomaticRulesFromIpc == null)
            return;

        var automatic = sessions.Where(s => !string.IsNullOrEmpty(s.AutomaticRuleSourceName)).ToList();
        if (automatic.Count == 0)
            return;

        var refreshed = await _framework.RunOnFrameworkThread(() =>
            automatic.Select(s => (Session: s, Rules: RefreshAutomaticRulesFromIpc.Invoke(s.GameType, s.AutomaticRuleSourceName!))).ToList());

        foreach (var (session, rules) in refreshed)
        {
            if (rules != null)
                session.ActiveRules = rules;
        }
    }

    private async Task RunAutoEndAsync(SessionState session, DateTime endAt, CancellationToken ct)
    {
        try
        {
            var warningAt = endAt - TimeSpan.FromMinutes(1);
            var warningDelay = warningAt - DateTime.UtcNow;
            if (warningDelay > TimeSpan.Zero)
                await Task.Delay(warningDelay, ct);

            if (!ct.IsCancellationRequested && DateTime.UtcNow < endAt)
            {
                var msg = new SeStringBuilder()
                    .AddText($"Your {session.GameType} hosting session will automatically end in 1 minute.")
                    .Build();
                _chatGui.Print(msg, "GambaWhere");

                var remaining = endAt - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, ct);
            }

            if (!ct.IsCancellationRequested)
                await StopSessionAsync(session.EventId);
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Unexpected error in auto-end task.");
        }
    }

    private void QueueDiscordWebhook(Func<Task> workload)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await workload.Invoke();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Discord webhook update task failed unexpectedly.");
            }
        });
    }

    private string? BoosterKeyForRequest() =>
        string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim();

    private void SaveSessionSnapshots()
    {
        lock (_saveGate)
            WriteSessionSnapshots();
    }

    private void WriteSessionSnapshots()
    {
        _config.ActiveSessions = _sessions.Snapshot().Select(session => new ActiveSessionSnapshot
        {
            EventId = session.EventId,
            SessionToken = session.SessionToken,
            CharacterName = session.CharacterName,
            GameType = session.GameType,
            VenueName = session.VenueName,
            RulesJson = session.ActiveRules != null ? JsonSerializer.Serialize(session.ActiveRules) : null,
            Description = session.Description,
            Location = session.Location,
            StartedAt = session.StartedAt,
            AutoEndAt = session.AutoEndAt,
            IsPaused = session.IsPaused,
            PausedAt = session.PausedAt,
            TotalPausedDurationTicks = session.TotalPausedDuration.Ticks,
            AutomaticRuleSourceName = session.AutomaticRuleSourceName,
            DiscordUrl = session.DiscordUrl,
            ImageUrl = session.ImageUrl,
        }).ToList();

        _config.Save();
    }

    private static Dictionary<string, object>? TryDeserializeRules(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _clientState.Login -= OnLogin;

        lock (_loopGate)
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
        }

        foreach (var session in _sessions.Snapshot())
            CancelAutoEndTask(session);
    }
}
