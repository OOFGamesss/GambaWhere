using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.Discord;
using GambaWhere.State;
using GambaWhere.Utility;
using Dalamud.Game.Text.SeStringHandling;

namespace GambaWhere.Services;

/// <summary>Manages the lifecycle of a hosting session, including start, pause, stop, heartbeat, and crash recovery.</summary>
public class SessionService : IDisposable
{
    private readonly GambaWhereClient _client;
    private readonly PlayerInfoService _playerInfo;
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly DiscordWebhookService _discordWebhook;
    private readonly IChatGui _chatGui;

    public Func<string, Dictionary<string, object>?>? RefreshAutomaticRulesFromIpc { get; set; }

    public SessionService(
        GambaWhereClient client,
        PlayerInfoService playerInfo,
        SessionState sessionState,
        Configuration config,
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        DiscordWebhookService discordWebhook,
        IChatGui chatGui)
    {
        _client = client;
        _playerInfo = playerInfo;
        _sessionState = sessionState;
        _config = config;
        _clientState = clientState;
        _framework = framework;
        _log = log;
        _discordWebhook = discordWebhook;
        _chatGui = chatGui;

        _clientState.TerritoryChanged += OnTerritoryChanged;
        _clientState.Login += OnLogin;

        if (_clientState.IsLoggedIn && !string.IsNullOrEmpty(_config.ActiveSessionToken))
            _ = Task.Run(TryRecoverSessionAsync);
    }

    public async Task<(string? Error, EventCreateResponse? Created)> StartSessionAsync(PostEventRequest request, DateTime? autoEndAt = null)
    {
        var response = await _client.PostEventAsync(request);
        if (response == null)
            return ("Failed to create session. Check the log for details.", null);

        _sessionState.IsActive = true;
        _sessionState.StartedAt = DateTime.UtcNow;
        _sessionState.AutoEndAt = autoEndAt;
        _sessionState.SessionToken = response.SessionToken;
        _sessionState.CharacterName = response.CharacterName;
        _sessionState.Location = response.Location;
        _sessionState.GameType = request.Game;
        _sessionState.VenueName = request.VenueName;
        _sessionState.ActiveRules = request.Rules;
        _sessionState.DiscordUrl = response.DiscordUrl;
        _sessionState.ImageUrl = response.ImageUrl;
        _sessionState.Description = request.Description;

        SaveSessionSnapshot();

        var cts = new CancellationTokenSource();
        _sessionState.LoopCts = cts;

        _ = Task.Run(() => RunHeartbeatLoopAsync(cts.Token));

        if (autoEndAt.HasValue)
            _ = Task.Run(() => RunAutoEndAsync(autoEndAt.Value, cts.Token));

        QueueDiscordWebhook(async () =>
        {
            await _discordWebhook.SyncActiveSessionEmbedsAsync();
        });

        return (null, response);
    }

    public async Task TogglePauseAsync()
    {
        if (_sessionState.IsPaused)
            await ResumeSessionAsync();
        else
            await PauseSessionAsync();
    }

    public async Task PauseSessionAsync()
    {
        if (!_sessionState.IsActive || _sessionState.IsPaused)
            return;

        _sessionState.IsPaused = true;
        _sessionState.PausedAt = DateTime.UtcNow;

        var putRequest = new PutEventRequest
        {
            Location = _sessionState.Location,
            Rules = _sessionState.ActiveRules,
            Description = SessionConstants.BreakMessage,
            BoosterKey = BoosterKeyForRequest()
        };
        await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        SaveSessionSnapshot();
    }

    public async Task ResumeSessionAsync()
    {
        if (!_sessionState.IsActive || !_sessionState.IsPaused)
            return;

        if (_sessionState.PausedAt.HasValue)
            _sessionState.TotalPausedDuration += DateTime.UtcNow - _sessionState.PausedAt.Value;

        _sessionState.IsPaused = false;
        _sessionState.PausedAt = null;

        var putRequest = new PutEventRequest
        {
            Location = _sessionState.Location,
            Rules = _sessionState.ActiveRules,
            Description = _sessionState.Description,
            BoosterKey = BoosterKeyForRequest()
        };
        await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        SaveSessionSnapshot();
    }

    public async Task StopSessionAsync()
    {
        if (!_sessionState.IsActive)
            return;

        _sessionState.LoopCts?.Cancel();

        await _client.DeleteEventAsync(_sessionState.CharacterName, _sessionState.SessionToken);

        _sessionState.Clear();
        ClearSessionSnapshot();

        QueueDiscordWebhook(async () =>
        {
            await _discordWebhook.PublishIdleEmbedsAsync();
        });
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        if (!_sessionState.IsActive)
            return;

        _ = PushLocationAsync();
    }

    private async Task PushLocationAsync()
    {
        var location = await _framework.RunOnFrameworkThread(() => _playerInfo.GetCurrentLocation());
        if (location == null)
            return;

        var putRequest = new PutEventRequest { Location = location, BoosterKey = BoosterKeyForRequest() };
        await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        _sessionState.Location = location;
    }

    private async Task RunAutoEndAsync(DateTime endAt, CancellationToken ct)
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
                    .AddText("Your hosting session will automatically end in 1 minute.")
                    .Build();
                _chatGui.Print(msg, "GambaWhere");

                var remaining = endAt - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, ct);
            }

            if (!ct.IsCancellationRequested)
                await StopSessionAsync();
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error in auto-end task.");
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);

                if (ct.IsCancellationRequested)
                    break;

                await SendHeartbeatPutAsync();
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error in location heartbeat loop.");
        }
    }

    private async Task SendHeartbeatPutAsync()
    {
        var location = await _framework.RunOnFrameworkThread(() => _playerInfo.GetCurrentLocation());
        if (location == null)
        {
            _log.Warning("Heartbeat location update skipped: could not resolve current territory.");
            return;
        }

        if (ShouldRefreshRulesFromIpc())
        {
            var refreshed = await _framework.RunOnFrameworkThread(() =>
                RefreshAutomaticRulesFromIpc!.Invoke(_sessionState.GameType));
            if (refreshed != null)
                _sessionState.ActiveRules = refreshed;
        }

        var putRequest = new PutEventRequest
        {
            Location = location,
            Rules = _sessionState.ActiveRules,
            Description = _sessionState.IsPaused ? SessionConstants.BreakMessage : _sessionState.Description,
            BoosterKey = BoosterKeyForRequest()
        };
        var putResponse =
            await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        _sessionState.Location = location;

        if (putResponse != null)
        {
            if (!string.IsNullOrWhiteSpace(putResponse.DiscordUrl))
                _sessionState.DiscordUrl = putResponse.DiscordUrl;

            if (!string.IsNullOrWhiteSpace(putResponse.ImageUrl))
                _sessionState.ImageUrl = putResponse.ImageUrl;
        }

        SaveSessionSnapshot();

        QueueDiscordWebhook(async () =>
        {
            await _discordWebhook.SyncActiveSessionEmbedsAsync();
        });
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
                _log.Error(ex, "Discord webhook update task failed unexpectedly.");
            }
        });
    }

    private string? BoosterKeyForRequest() =>
        string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim();

    private bool ShouldRefreshRulesFromIpc()
    {
        return _sessionState.UsesAutomaticHostRules
               && RefreshAutomaticRulesFromIpc != null
               && (_sessionState.GameType == "Bingo"
                   || _sessionState.GameType == "Roulette"
                   || _sessionState.GameType == "Chocobo Racing"
                   || _sessionState.GameType == "Mini Games");
    }

    public void Dispose()
    {
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _clientState.Login -= OnLogin;
        _sessionState.LoopCts?.Cancel();
        _sessionState.LoopCts?.Dispose();
    }

    private void OnLogin()
    {
        if (!string.IsNullOrEmpty(_config.ActiveSessionToken))
            _ = Task.Run(TryRecoverSessionAsync);
    }

    public async Task TryRecoverSessionAsync()
    {
        if (_sessionState.IsActive)
            return;

        if (string.IsNullOrEmpty(_config.ActiveSessionToken) || string.IsNullOrEmpty(_config.ActiveCharacterName))
            return;

        var events = await _client.GetEventsAsync();
        var sessionStillLive = events != null && Array.Exists(events, e =>
            string.Equals(e.CharacterName, _config.ActiveCharacterName, StringComparison.OrdinalIgnoreCase));

        if (!sessionStillLive)
        {
            ClearSessionSnapshot();
            return;
        }

        var rules = TryDeserializeRules(_config.ActiveRulesJson);
        var description = _config.ActiveIsPaused ? SessionConstants.BreakMessage : (_config.ActiveDescription ?? string.Empty);

        var putRequest = new PutEventRequest
        {
            Rules = rules,
            Description = description,
            BoosterKey = BoosterKeyForRequest()
        };

        var response = await _client.PutEventAsync(_config.ActiveCharacterName, _config.ActiveSessionToken, putRequest);

        if (response == null)
        {
            ClearSessionSnapshot();
            return;
        }

        _sessionState.IsActive = true;
        _sessionState.SessionToken = _config.ActiveSessionToken;
        _sessionState.CharacterName = _config.ActiveCharacterName;
        _sessionState.GameType = _config.ActiveGameType ?? string.Empty;
        _sessionState.VenueName = _config.ActiveVenueName;
        _sessionState.ActiveRules = rules;
        _sessionState.Description = _config.ActiveDescription ?? string.Empty;
        _sessionState.Location = _config.ActiveLocation ?? response.Location ?? string.Empty;
        _sessionState.StartedAt = _config.ActiveStartedAt;
        _sessionState.AutoEndAt = _config.ActiveAutoEndAt;
        _sessionState.IsPaused = _config.ActiveIsPaused;
        _sessionState.PausedAt = _config.ActivePausedAt;
        _sessionState.TotalPausedDuration = TimeSpan.FromTicks(_config.ActiveTotalPausedDurationTicks);
        _sessionState.UsesAutomaticHostRules = _config.ActiveUsesAutomaticHostRules;
        _sessionState.DiscordUrl = response.DiscordUrl ?? _config.ActiveDiscordUrl;
        _sessionState.ImageUrl = response.ImageUrl ?? _config.ActiveImageUrl;

        var cts = new CancellationTokenSource();
        _sessionState.LoopCts = cts;
        _ = Task.Run(() => RunHeartbeatLoopAsync(cts.Token));

        if (_config.ActiveAutoEndAt.HasValue && _config.ActiveAutoEndAt.Value > DateTime.UtcNow)
            _ = Task.Run(() => RunAutoEndAsync(_config.ActiveAutoEndAt.Value, cts.Token));

        var msg = new SeStringBuilder()
            .AddText("Your previous hosting session has been recovered.")
            .Build();
        _chatGui.Print(msg, "GambaWhere");

        QueueDiscordWebhook(async () =>
        {
            await _discordWebhook.SyncActiveSessionEmbedsAsync();
        });
    }

    private void SaveSessionSnapshot()
    {
        _config.ActiveSessionToken = _sessionState.SessionToken;
        _config.ActiveCharacterName = _sessionState.CharacterName;
        _config.ActiveGameType = _sessionState.GameType;
        _config.ActiveVenueName = _sessionState.VenueName;
        _config.ActiveRulesJson = _sessionState.ActiveRules != null
            ? JsonSerializer.Serialize(_sessionState.ActiveRules)
            : null;
        _config.ActiveDescription = _sessionState.Description;
        _config.ActiveLocation = _sessionState.Location;
        _config.ActiveStartedAt = _sessionState.StartedAt;
        _config.ActiveAutoEndAt = _sessionState.AutoEndAt;
        _config.ActiveIsPaused = _sessionState.IsPaused;
        _config.ActivePausedAt = _sessionState.PausedAt;
        _config.ActiveTotalPausedDurationTicks = _sessionState.TotalPausedDuration.Ticks;
        _config.ActiveUsesAutomaticHostRules = _sessionState.UsesAutomaticHostRules;
        _config.ActiveDiscordUrl = _sessionState.DiscordUrl;
        _config.ActiveImageUrl = _sessionState.ImageUrl;
        _config.Save();
    }

    private void ClearSessionSnapshot()
    {
        _config.ActiveSessionToken = null;
        _config.ActiveCharacterName = null;
        _config.ActiveGameType = null;
        _config.ActiveVenueName = null;
        _config.ActiveRulesJson = null;
        _config.ActiveDescription = null;
        _config.ActiveLocation = null;
        _config.ActiveStartedAt = null;
        _config.ActiveAutoEndAt = null;
        _config.ActiveIsPaused = false;
        _config.ActivePausedAt = null;
        _config.ActiveTotalPausedDurationTicks = 0;
        _config.ActiveUsesAutomaticHostRules = false;
        _config.ActiveDiscordUrl = null;
        _config.ActiveImageUrl = null;
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
}
