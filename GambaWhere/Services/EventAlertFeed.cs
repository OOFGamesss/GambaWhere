using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.API.Models;

namespace GambaWhere.Services;

/// <summary>Polls the API for events and feeds them to the alerting service.</summary>
public sealed class EventAlertFeed : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly GambaWhereClient _client;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();

    private volatile bool _isRefreshing;
    private DateTime _nextRefreshUtc = DateTime.MinValue;

    public EventAlertFeed(GambaWhereClient client, IPluginLog log)
    {
        _client = client;
        _log = log;
    }

    public Action<IReadOnlyList<EventResponse>>? OnEventsRefreshed { get; set; }

    public void Tick()
    {
        if (_isRefreshing || _cts.IsCancellationRequested)
            return;

        if (DateTime.UtcNow < _nextRefreshUtc)
            return;

        _isRefreshing = true;
        _nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _client.GetEventsAsync(ct);
                if (ct.IsCancellationRequested)
                    return;

                if (results != null)
                {
                    // _log.Information("[GambaWhere/AlertFeed] GET /events -> {Count} events.", results.Length);
                    OnEventsRefreshed?.Invoke(results);
                }
            }
            catch (OperationCanceledException)
            {
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
