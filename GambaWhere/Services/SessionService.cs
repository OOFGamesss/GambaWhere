using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
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
    private readonly IPluginLog _log;

    public SessionService(
        GambaWhereClient client,
        PlayerInfoService playerInfo,
        SessionState sessionState,
        Configuration config,
        IPluginLog log)
    {
        _client = client;
        _playerInfo = playerInfo;
        _sessionState = sessionState;
        _config = config;
        _log = log;
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

        _ = Task.Run(() => RunLocationUpdateLoopAsync(cts.Token));

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

    private async Task RunLocationUpdateLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(15), ct);

                if (ct.IsCancellationRequested)
                    break;

                var location = _playerInfo.GetCurrentLocation();
                if (location == null)
                {
                    _log.Warning("Location update skipped: could not resolve current territory.");
                    continue;
                }

                var putRequest = new PutEventRequest { Location = location };
                await _client.PutEventAsync(_sessionState.CharacterName, _sessionState.SessionToken, putRequest);

                _sessionState.Location = location;

            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error in location update loop.");
        }
    }

    public void Dispose()
    {
        _sessionState.LoopCts?.Cancel();
        _sessionState.LoopCts?.Dispose();
    }
}
