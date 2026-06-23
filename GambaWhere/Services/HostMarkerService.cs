using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using GambaWhere.Models;
using GambaWhere.Config;

namespace GambaWhere.Services;

/// <summary>Tracks which active hosts are nearby in the object table so they can be drawn on the minimap.</summary>
public sealed class HostMarkerService : IDisposable
{
    private const int ScanIntervalFrames = 15;

    private readonly IObjectTable _objectTable;
    private readonly PlayerInfoService _playerInfo;
    private readonly Configuration _config;

    private sealed record HostEntry(string Game, IReadOnlyDictionary<string, object> Rules);

    private volatile Dictionary<string, HostEntry> _activeHosts = new();
    private volatile IReadOnlyList<HostMarker> _markers = Array.Empty<HostMarker>();
    private int _frameCounter;

    public HostMarkerService(IObjectTable objectTable, PlayerInfoService playerInfo, Configuration config)
    {
        _objectTable = objectTable;
        _playerInfo = playerInfo;
        _config = config;
    }

    public IReadOnlyList<HostMarker> Markers => _markers;

    public void OnEventsRefreshed(IReadOnlyList<EventResponse> events)
    {
        var next = new Dictionary<string, HostEntry>(StringComparer.Ordinal);
        foreach (var ev in events)
        {
            if (ev.IsActive && !string.IsNullOrWhiteSpace(ev.CharacterName))
                next[ev.CharacterName] = new HostEntry(ev.Game, ev.Rules);
        }

        _activeHosts = next;
    }

    public void Tick()
    {
        var hosts = _activeHosts;
        if (!_config.MinimapHostIconsEnabled || !_playerInfo.IsLoggedIn || hosts.Count == 0)
        {
            _markers = Array.Empty<HostMarker>();
            return;
        }

        if (++_frameCounter < ScanIntervalFrames)
            return;

        _frameCounter = 0;
        Scan(hosts);
    }

    private void Scan(Dictionary<string, HostEntry> hosts)
    {
        var localId = _playerInfo.GetObjectId();
        var found = new List<HostMarker>();

        foreach (var obj in _objectTable)
        {
            if (obj is not IPlayerCharacter pc || pc.ObjectKind != ObjectKind.Pc || pc.GameObjectId == localId)
                continue;

            var world = pc.HomeWorld.Value.Name.ToString();
            if (string.IsNullOrEmpty(world))
                continue;

            var key = $"{pc.Name.TextValue} {world}";
            if (hosts.TryGetValue(key, out var entry) && _config.IsMinimapGameTypeEnabled(entry.Game))
                found.Add(new HostMarker(key, entry.Game, entry.Rules, pc.Position));
        }

        _markers = found;
    }

    public void Dispose()
    {
        _activeHosts = new Dictionary<string, HostEntry>();
        _markers = Array.Empty<HostMarker>();
    }
}

public readonly record struct HostMarker(
    string DisplayName, string Game, IReadOnlyDictionary<string, object> Rules, Vector3 Position);
