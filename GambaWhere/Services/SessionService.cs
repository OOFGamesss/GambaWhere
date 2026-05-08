using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.State;

namespace GambaWhere.Services;

public class SessionService : IDisposable
{
    private readonly GambaWhereClient _client;
    private readonly PlayerInfoService _playerInfo;
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;

    public Func<string, Dictionary<string, object>?>? RefreshAutomaticRulesFromIpc { get; set; }

    public SessionService(
        GambaWhereClient client,
        PlayerInfoService playerInfo,
        SessionState sessionState,
        Configuration config,
        IClientState clientState,
        IFramework framework,
        IPluginLog log)
    {
        _client = client;
        _playerInfo = playerInfo;
        _sessionState = sessionState;
        _config = config;
        _clientState = clientState;
        _framework = framework;
        _log = log;

        _clientState.TerritoryChanged += OnTerritoryChanged;
    }

    public async Task<string?> StartSessionAsync(PostEventRequest request)
    {
        var response = await _client.PostEventAsync(request);
        if (response == null)
            return "Failed to create session. Check the log for details.";

        _sessionState.IsActive = true;
        _sessionState.SessionToken = response.SessionToken;
        _sessionState.CharacterName = response.CharacterName;
        _sessionState.Location = response.Location;

        _config.ActiveSessionToken = response.SessionToken;
        _config.ActiveCharacterName = response.CharacterName;
        _config.Save();

        var cts = new CancellationTokenSource();
        _sessionState.LoopCts = cts;

        _ = Task.Run(() => RunHeartbeatLoopAsync(cts.Token));

        return null;
    }

    public async Task StopSessionAsync()
    {
        if (!_sessionState.IsActive)
            return;

        _sessionState.LoopCts?.Cancel();

        await _client.DeleteEventAsync(_sessionState.CharacterName, _sessionState.SessionToken);

        _sessionState.Clear();
        _config.ActiveSessionToken = null;
        _config.ActiveCharacterName = null;
        _config.Save();
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

        var putRequest = new PutEventRequest { Location = location };
        await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        _sessionState.Location = location;
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
            Rules = _sessionState.ActiveRules
        };
        await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

        _sessionState.Location = location;
    }

    private bool ShouldRefreshRulesFromIpc()
    {
        return _sessionState.UsesAutomaticHostRules
               && RefreshAutomaticRulesFromIpc != null
               && (_sessionState.GameType == "Bingo"
                   || _sessionState.GameType == "Roulette"
                   || _sessionState.GameType == "Chocobo Racing");
    }

    public void Dispose()
    {
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _sessionState.LoopCts?.Cancel();
        _sessionState.LoopCts?.Dispose();
    }
}
