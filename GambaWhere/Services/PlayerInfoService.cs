using System;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace GambaWhere.Services;

/// <summary>Reads the local player's character and location details from the game, and finds the nearest aetheryte to a point.</summary>
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

        var english = _dataManager.GetExcelSheet<PlaceName>(ClientLanguage.English);
        if (english != null && english.TryGetRow(territory.PlaceName.RowId, out var englishPlace))
            return FormatPlaceAreaName(englishPlace.Name.ToString());

        return FormatPlaceAreaName(territory.PlaceName.Value.Name.ToString());
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
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return 0f;

        var map = CurrentMap();
        return RawToMapCoord(player.Position.X, map.SizeFactor, map.OffsetX);
    }

    public float GetCurrentY()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return 0f;

        var map = CurrentMap();
        return RawToMapCoord(player.Position.Z, map.SizeFactor, map.OffsetY);
    }

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

        var ward = GetCurrentWard();
        if (ward > 0)
        {
            var plot = GetCurrentPlot();
            return plot > 0
                ? $"{dataCentre} • {world} • {area} • Ward {ward} • Plot {plot}"
                : $"{dataCentre} • {world} • {area} • Ward {ward}";
        }

        return $"{dataCentre} • {world} • {area} • X: {GetCurrentX():F1}, Y: {GetCurrentY():F1}";
    }

    public Vector3? GetWorldPosition() => _objectTable.LocalPlayer?.Position;

    public ulong GetObjectId() => _objectTable.LocalPlayer?.GameObjectId ?? 0;

    public string? GetClosestAetheryte(string area, float mapX, float mapY)
    {
        area = area.Trim();
        if (area.Length == 0)
            return null;

        Map zoneMap = default;

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

        if (zoneMap.RowId == 0)
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

    private static float MarkerToMapCoord(int markerRaw, ushort sizeFactor)
        => (markerRaw / 2048f) * (4100f / sizeFactor) + 1f;

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

        if (!_dataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var wardTerritory))
            return null;

        var english = _dataManager.GetExcelSheet<PlaceName>(ClientLanguage.English);
        if (english != null && english.TryGetRow(wardTerritory.PlaceName.RowId, out var englishPlace))
        {
            var raw = englishPlace.Name.ToString();
            return string.IsNullOrWhiteSpace(raw) ? null : FormatPlaceAreaName(raw);
        }

        var name = wardTerritory.PlaceName.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : FormatPlaceAreaName(name);
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

    private static float RawToMapCoord(float rawPos, ushort sizeFactor, short offset)
    {
        return MathF.Round((0.02f * offset) + (2048f / sizeFactor) + (0.02f * rawPos) + 1.0f, 1);
    }
}
