using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using GambaWhere.API.Models;
using GambaWhere.Config;

namespace GambaWhere.Services;

/// <summary>
/// Resolves which active hosts are present in the local object table so they can be drawn on the minimap.
/// </summary>
public sealed class HostMarkerService : IDisposable
{
    private const int ScanIntervalFrames = 15;

    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly Configuration _config;

    private sealed record HostEntry(string Game, IReadOnlyDictionary<string, object> Rules);

    private volatile Dictionary<string, HostEntry> _activeHosts = new();
    private volatile IReadOnlyList<HostMarker> _markers = Array.Empty<HostMarker>();

    private int _frameCounter;

    public HostMarkerService(IObjectTable objectTable, IClientState clientState, Configuration config)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _config = config;
    }

    public IReadOnlyList<HostMarker> Markers => _markers;

    public void OnEventsRefreshed(IReadOnlyList<EventResponse> events)
    {
        var next = new Dictionary<string, HostEntry>(StringComparer.Ordinal);
        foreach (var ev in events)
        {
            if (!ev.IsActive || string.IsNullOrWhiteSpace(ev.CharacterName))
                continue;

            next[ev.CharacterName] = new HostEntry(ev.Game, ev.Rules);
        }

        _activeHosts = next;
    }

    public void Tick()
    {
        if (!_config.MinimapHostIconsEnabled || !_clientState.IsLoggedIn)
        {
            if (_markers.Count > 0)
                _markers = Array.Empty<HostMarker>();
            return;
        }

        var hosts = _activeHosts;
        if (hosts.Count == 0)
        {
            if (_markers.Count > 0)
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
        var localId = _objectTable.LocalPlayer?.GameObjectId ?? 0;
        List<HostMarker>? found = null;

        foreach (var obj in _objectTable)
        {
            if (obj is not IPlayerCharacter pc || pc.ObjectKind != ObjectKind.Pc)
                continue;
            if (pc.GameObjectId == localId)
                continue;

            var world = pc.HomeWorld.Value.Name.ToString();
            if (string.IsNullOrEmpty(world))
                continue;

            var key = $"{pc.Name.TextValue} {world}";
            if (!hosts.TryGetValue(key, out var entry))
                continue;
            if (!_config.IsMinimapGameTypeEnabled(entry.Game))
                continue;

            (found ??= new List<HostMarker>()).Add(
                new HostMarker(key, entry.Game, entry.Rules, pc.Position));
        }

        _markers = (IReadOnlyList<HostMarker>?)found ?? Array.Empty<HostMarker>();
    }

    public void Dispose()
    {
        _activeHosts = new Dictionary<string, HostEntry>();
        _markers = Array.Empty<HostMarker>();
    }
}

public readonly struct HostMarker
{
    public HostMarker(string displayName, string game, IReadOnlyDictionary<string, object> rules, Vector3 position)
    {
        DisplayName = displayName;
        Game = game;
        Rules = rules;
        Position = position;
    }

    public string DisplayName { get; }
    public string Game { get; }
    public IReadOnlyDictionary<string, object> Rules { get; }
    public Vector3 Position { get; }
}
