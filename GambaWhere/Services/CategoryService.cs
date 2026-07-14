using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Models;

namespace GambaWhere.Services;

/// <summary>Fetches game types on plugin load and Host Gamba tab selection, caching the last good result for offline use.</summary>
public sealed class CategoryService : IDisposable
{
    private readonly GambaWhereClient _client;
    private readonly Configuration _config;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();

    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;

    public CategoryService(GambaWhereClient client, Configuration config, IFramework framework, IPluginLog log)
    {
        _client = client;
        _config = config;
        _framework = framework;
        _log = log;

        ApplyCachedOrFallback();
        RequestRefresh();
    }

    public IReadOnlyList<GameCategory> Categories => GameCategories.All;

    public bool IsRefreshing => _isRefreshing;

    public bool LastRefreshFailed => _lastRefreshFailed;

    public DateTime? LastUpdatedUtc => _config.CategoriesUpdatedAt;

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
                var results = await _client.GetGameTypesAsync(ct);
                if (ct.IsCancellationRequested)
                    return;

                if (results == null || results.Length == 0)
                {
                    _lastRefreshFailed = true;
                    return;
                }

                await _framework.RunOnFrameworkThread(() =>
                {
                    CategoryMapper.NormalizeDefaults(results);
                    var categories = CategoryMapper.ToCategories(results);
                    GameCategories.ReplaceAll(categories);
                    _config.CachedGameTypes = new List<GameTypeDto>(results);
                    _config.CategoriesUpdatedAt = DateTime.UtcNow;
                    _config.EnsureDefaultPresets();
                    _config.Save();
                });
                _lastRefreshFailed = false;
                _log.Debug("GET /games/types -> {Count} game types.", results.Length);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _lastRefreshFailed = true;
                _log.Warning(ex, "Game types refresh failed.");
            }
            finally
            {
                _isRefreshing = false;
            }
        }, ct);
    }

    private void ApplyCachedOrFallback()
    {
        if (_config.CachedGameTypes is { Count: > 0 } cached)
        {
            CategoryMapper.NormalizeDefaults(cached);
            GameCategories.ReplaceAll(CategoryMapper.ToCategories(cached));
            return;
        }

        GameCategories.ReplaceAll(GameCategories.Fallbacks);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
