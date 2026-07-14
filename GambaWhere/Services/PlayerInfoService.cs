using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

/// <summary>
/// Reads the local player's identity and location, and resolves map coordinates and the nearest aetheryte.
/// </summary>
namespace GambaWhere.Services;

public class PlayerInfoService
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly IDataManager _dataManager;

    public PlayerInfoService(IClientState clientState, IObjectTable objectTable, IDataManager dataManager)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _dataManager = dataManager;
    }

    public bool IsLoggedIn => _clientState.IsLoggedIn;

    public string? GetName() => _objectTable.LocalPlayer?.Name.TextValue;

    public string? GetHomeWorld() => _objectTable.LocalPlayer?.HomeWorld.Value.Name.ToString();

    public string? GetHomeDataCentre()
    {
        var dc = _objectTable.LocalPlayer?.HomeWorld.Value.DataCenter.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(dc) ? null : dc;
    }

    public string? GetCurrentWorld()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return null;

        var world = player.CurrentWorld.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(world) ? player.HomeWorld.Value.Name.ToString() : world;
    }

    public string? GetCurrentDataCentre()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return null;

        var dc = player.CurrentWorld.Value.DataCenter.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(dc))
            dc = player.HomeWorld.Value.DataCenter.Value.Name.ToString();

        return string.IsNullOrWhiteSpace(dc) ? null : dc;
    }

    public unsafe string? GetCurrentArea()
    {
        if (!_dataManager.GetExcelSheet<TerritoryType>().TryGetRow(_clientState.TerritoryType, out var territory))
            return null;

        var housing = HousingManager.Instance();
        if (housing != null && housing->IsInside())
        {
            var district = ResolveHousingDistrictAreaName();
            if (!string.IsNullOrWhiteSpace(district))
                return district;
        }

        return ResolvePlaceAreaName(territory);
    }

    public unsafe int GetCurrentWard()
    {
        var housing = HousingManager.Instance();
        if (housing == null)
            return 0;

        var ward = housing->GetCurrentWard();
        return ward < 0 ? 0 : ward + 1;
    }

    public unsafe int GetCurrentPlot()
    {
        var housing = HousingManager.Instance();
        if (housing == null)
            return 0;

        var plot = housing->GetCurrentPlot();
        return plot < 0 ? 0 : plot + 1;
    }

    public float GetCurrentX()
        => TryGetPlayerMap(out var position, out var map)
            ? RawToMapCoord(position.X, map.SizeFactor, map.OffsetX)
            : 0f;

    public float GetCurrentY()
        => TryGetPlayerMap(out var position, out var map)
            ? RawToMapCoord(position.Z, map.SizeFactor, map.OffsetY)
            : 0f;

    public string? GetCharacterName()
    {
        var name = GetName();
        var world = GetHomeWorld();
        return string.IsNullOrEmpty(name) || string.IsNullOrEmpty(world) ? null : $"{name} {world}";
    }

    public string? GetCurrentLocation()
    {
        var dataCentre = GetCurrentDataCentre();
        var world = GetCurrentWorld();
        var area = GetCurrentArea();
        if (dataCentre == null || world == null || area == null)
            return null;

        var location = $"{dataCentre} • {world} • {area}";

        var ward = GetCurrentWard();
        if (ward > 0)
        {
            location += $" • Ward {ward}";
            var plot = GetCurrentPlot();
            if (plot > 0)
                location += $" • Plot {plot}";
        }

        location += $" • X: {FormatMapCoord(GetCurrentX())}, Y: {FormatMapCoord(GetCurrentY())}";

        return location;
    }

    public Vector3? GetWorldPosition() => _objectTable.LocalPlayer?.Position;

    public ulong GetObjectId() => _objectTable.LocalPlayer?.GameObjectId ?? 0;

    public string? GetClosestAetheryte(string area, float mapX, float mapY)
    {
        if (!TryResolveZoneMap(area, out var zoneMap))
            return null;

        if (!_dataManager.GetSubrowExcelSheet<MapMarker>().TryGetRow(zoneMap.MapMarkerRange, out var markers))
            return null;

        var aetherytes = _dataManager.GetExcelSheet<Aetheryte>();

        string? closest = null;
        var bestDistance = float.MaxValue;

        foreach (var marker in markers)
        {
            if (marker.DataType != 3)
                continue;

            if (!aetherytes.TryGetRow(marker.DataKey.RowId, out var aetheryte)
                || !aetheryte.IsAetheryte || !aetheryte.PlaceName.IsValid)
                continue;

            var aetheryteX = MarkerToMapCoord(marker.X, zoneMap.SizeFactor);
            var aetheryteY = MarkerToMapCoord(marker.Y, zoneMap.SizeFactor);
            var distance = ((aetheryteX - mapX) * (aetheryteX - mapX)) + ((aetheryteY - mapY) * (aetheryteY - mapY));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = aetheryte.PlaceName.Value.Name.ToString().Trim();
            }
        }

        return string.IsNullOrEmpty(closest) ? null : closest;
    }

    private bool TryGetPlayerMap(out Vector3 position, out Map map)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            position = default;
            map = default;
            return false;
        }

        position = player.Position;
        map = CurrentMap();
        return true;
    }

    private string? ResolvePlaceAreaName(TerritoryType territory)
    {
        var english = _dataManager.GetExcelSheet<PlaceName>(ClientLanguage.English);
        var raw = english != null && english.TryGetRow(territory.PlaceName.RowId, out var englishPlace)
            ? englishPlace.Name.ToString()
            : territory.PlaceName.Value.Name.ToString();

        return string.IsNullOrWhiteSpace(raw) ? null : FormatPlaceAreaName(raw);
    }

    private unsafe string? ResolveHousingDistrictAreaName()
    {
        var territoryTypeId = HousingManager.GetOriginalHouseTerritoryTypeId();
        if (territoryTypeId == 0)
        {
            var housing = HousingManager.Instance();
            if (housing != null)
                territoryTypeId = housing->GetCurrentIndoorHouseId().TerritoryTypeId;
        }

        if (territoryTypeId == 0)
            return null;

        return _dataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var wardTerritory)
            ? ResolvePlaceAreaName(wardTerritory)
            : null;
    }

    private bool TryResolveZoneMap(string area, out Map zoneMap)
    {
        zoneMap = default;

        area = area.Trim();
        if (area.Length == 0)
            return false;

        foreach (var territory in _dataManager.GetExcelSheet<TerritoryType>())
        {
            if (territory.RowId == 0 || !territory.PlaceName.IsValid)
                continue;

            if (!FormatPlaceAreaName(territory.PlaceName.Value.Name.ToString())
                    .Equals(area, StringComparison.OrdinalIgnoreCase))
                continue;

            if (territory.Map.IsValid)
                zoneMap = territory.Map.Value;

            break;
        }

        return zoneMap.RowId != 0;
    }

    private Map CurrentMap()
    {
        var maps = _dataManager.GetExcelSheet<Map>();
        if (maps.TryGetRow(_clientState.MapId, out var active))
            return active;

        return _dataManager.GetExcelSheet<TerritoryType>().TryGetRow(_clientState.TerritoryType, out var territory)
            ? territory.Map.Value
            : default;
    }

    private static string FormatPlaceAreaName(string rawArea)
    {
        var dashIndex = rawArea.LastIndexOf(" - ", StringComparison.Ordinal);
        return dashIndex >= 0 ? rawArea[(dashIndex + 3)..] : rawArea;
    }

    private static string FormatMapCoord(float value)
        => value.ToString("F1", CultureInfo.InvariantCulture);

    private static float MarkerToMapCoord(int markerRaw, ushort sizeFactor)
        => (markerRaw / 2048f) * (4100f / sizeFactor) + 1f;

    private static float RawToMapCoord(float rawPos, ushort sizeFactor, short offset)
        => MathF.Round((0.02f * offset) + (2048f / sizeFactor) + (0.02f * rawPos) + 1.0f, 1);
}
