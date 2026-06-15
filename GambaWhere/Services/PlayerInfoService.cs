using System;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace GambaWhere.Services;

/// <summary>Reads the local player's character name, current world, and map coordinates from the game state.</summary>
public class PlayerInfoService
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;

    public PlayerInfoService(IClientState clientState, IObjectTable objectTable, IDataManager dataManager, IPluginLog log)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _dataManager = dataManager;
        _log = log;
    }

    public bool IsLoggedIn => _clientState.IsLoggedIn;

    public string? GetCharacterName()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _log.Warning("Attempted to read character name while not logged in.");
            return null;
        }

        var name = player.Name.TextValue;
        var world = player.HomeWorld.Value.Name.ToString();
        return $"{name} {world}";
    }

    public string? GetHomeDataCentre()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return null;

        var dc = player.HomeWorld.Value.DataCenter.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(dc) ? null : dc;
    }

    public string? GetCurrentLocation()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
            return null;

        var world = player.CurrentWorld.Value.Name.ToString();
        var dc = player.CurrentWorld.Value.DataCenter.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(world))
        {
            world = player.HomeWorld.Value.Name.ToString();
            dc = player.HomeWorld.Value.DataCenter.Value.Name.ToString();
        }

        var territoryId = _clientState.TerritoryType;
        if (!_dataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            _log.Warning("Could not resolve territory ID {Id} to a place name.", territoryId);
            return $"{world} [{dc}]";
        }

        var area = ResolveAreaName(territory);

        var mapSheet = _dataManager.GetExcelSheet<Map>();
        var map = mapSheet.TryGetRow(_clientState.MapId, out var activeMap)
            ? activeMap
            : territory.Map.Value;

        var mapX = RawToMapCoord(player.Position.X, map.SizeFactor, map.OffsetX);
        var mapY = RawToMapCoord(player.Position.Z, map.SizeFactor, map.OffsetY);

        var housing = TryGetHousingInfo();

        if (housing.HasValue)
        {
            var (ward, plot) = housing.Value;
            return plot > 0
                ? $"{dc} • {world} • {area} • Ward {ward} • Plot {plot}"
                : $"{dc} • {world} • {area} • Ward {ward}";
        }

        return $"{dc} • {world} • {area} • X: {mapX:F1}, Y: {mapY:F1}";
    }

    private static float RawToMapCoord(float rawPos, ushort sizeFactor, short offset)
    {
        return MathF.Round((0.02f * offset) + (2048f / sizeFactor) + (0.02f * rawPos) + 1.0f, 1);
    }

    private static string FormatPlaceAreaName(string rawArea)
    {
        var dashIndex = rawArea.LastIndexOf(" - ", StringComparison.Ordinal);
        return dashIndex >= 0 ? rawArea[(dashIndex + 3)..] : rawArea;
    }

    private unsafe string ResolveAreaName(TerritoryType territory)
    {
        var hm = HousingManager.Instance();
        if (hm != null && hm->IsInside())
        {
            var resolved = ResolveHousingDistrictAreaName();
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        var englishPlaceNames = _dataManager.GetExcelSheet<PlaceName>(ClientLanguage.English);
        if (englishPlaceNames != null && englishPlaceNames.TryGetRow(territory.PlaceName.RowId, out var englishPlace))
            return FormatPlaceAreaName(englishPlace.Name.ToString());

        return FormatPlaceAreaName(territory.PlaceName.Value.Name.ToString());
    }

    private string? ResolveHousingDistrictAreaName()
    {
        uint territoryTypeId = HousingManager.GetOriginalHouseTerritoryTypeId();

        if (territoryTypeId == 0)
        {
            unsafe
            {
                var hm = HousingManager.Instance();
                if (hm != null)
                    territoryTypeId = hm->GetCurrentIndoorHouseId().TerritoryTypeId;
            }
        }

        if (territoryTypeId == 0)
            return null;

        if (!_dataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var wardTerritory))
            return null;

        var englishPlaceNames = _dataManager.GetExcelSheet<PlaceName>(ClientLanguage.English);
        if (englishPlaceNames != null && englishPlaceNames.TryGetRow(wardTerritory.PlaceName.RowId, out var englishPlace))
        {
            var englishRaw = englishPlace.Name.ToString();
            return string.IsNullOrWhiteSpace(englishRaw) ? null : FormatPlaceAreaName(englishRaw);
        }

        var raw = wardTerritory.PlaceName.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : FormatPlaceAreaName(raw);
    }

    private unsafe (int ward, int plot)? TryGetHousingInfo()
    {
        try
        {
            var hm = HousingManager.Instance();
            if (hm == null) return null;

            var ward = hm->GetCurrentWard();
            if (ward < 0) return null;

            var plot = hm->GetCurrentPlot();
            return (ward + 1, plot >= 0 ? plot + 1 : 0);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
