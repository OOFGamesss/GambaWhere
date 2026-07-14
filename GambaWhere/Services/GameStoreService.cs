using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Models;

namespace GambaWhere.Services;

/// <summary>Fetches the game store listings on plugin load and whenever the Game Store tab is selected, caching the last good result in the config so the store still works offline.</summary>
public sealed class GameStoreService : IDisposable
{
    private readonly GambaWhereClient _client;
    private readonly Configuration _config;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();

    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;

    public GameStoreService(GambaWhereClient client, Configuration config, IFramework framework, IPluginLog log)
    {
        _client = client;
        _config = config;
        _framework = framework;
        _log = log;

        Games = _config.CachedStoreGames ?? new List<GameStoreGame>();
        RequestRefresh();
    }

    public IReadOnlyList<GameStoreGame> Games { get; private set; }

    public bool IsRefreshing => _isRefreshing;

    public bool LastRefreshFailed => _lastRefreshFailed;

    public DateTime? LastUpdatedUtc => _config.StoreGamesUpdatedAt;

    public void RequestRefresh()
    {
        if (_isRefreshing || _cts.IsCancellationRequested)
            return;

        _isRefreshing = true;
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _client.GetGamesAsync(ct);
                if (ct.IsCancellationRequested)
                    return;

                if (results == null)
                {
                    _lastRefreshFailed = true;
                    return;
                }

                await _framework.RunOnFrameworkThread(() =>
                {
                    Games = results;
                    _config.CachedStoreGames = new List<GameStoreGame>(results);
                    _config.StoreGamesUpdatedAt = DateTime.UtcNow;
                    _config.Save();
                });
                _lastRefreshFailed = false;
                _log.Debug("GET /games -> {Count} store games.", results.Length);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _lastRefreshFailed = true;
                _log.Warning(ex, "Game store refresh failed.");
            }
            finally
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
}
