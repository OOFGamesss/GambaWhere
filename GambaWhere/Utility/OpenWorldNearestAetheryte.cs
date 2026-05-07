using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace GambaWhere.Utility;

internal static class OpenWorldNearestAetheryte
{
    internal static bool TryPickTerritoryByPlaceName(IDataManager data, string zoneQuery, out uint territoryRowId,
        out string error)
    {
        territoryRowId = 0;
        error = "";
        zoneQuery = zoneQuery.Trim();

        if (zoneQuery.Length == 0)
        {
            error = "No zone name was found next to those map coordinates.";
            return false;
        }

        ExcelSheet<TerritoryType> sheet;

        try
        {
            sheet = data.GetExcelSheet<TerritoryType>();
        }
        catch (Exception)
        {
            error = "Game data sheets are not available.";
            return false;
        }

        TerritoryType? bestRow = null;
        var bestScore = 0;

        foreach (var row in sheet)
        {
            if (row.RowId == 0 || !row.PlaceName.IsValid)
                continue;

            var name = TerritoryPlaceDisplayName(row.PlaceName.Value.Name.ToString());
            if (name.Length == 0)
                continue;

            var score = ZoneMatchScore(name, zoneQuery);
            if (score > bestScore)
            {
                bestScore = score;
                bestRow = row;
            }
        }

        if (bestScore < 50 || bestRow is null)
        {
            error =
                $"Could not match zone \"{zoneQuery}\" to a territory. Check spelling (for example «South Shroud»).";

            return false;
        }

        territoryRowId = bestRow.Value.RowId;
        return true;
    }

    internal static bool TryGetLifestreamTeleportSearchLabel(IDataManager data, uint aetheryteRowId,
        out string label)
    {
        label = "";

        if (!data.GetExcelSheet<Aetheryte>().TryGetRow(aetheryteRowId, out var row))
            return false;

        if (row.PlaceName.IsValid)
        {
            var name = row.PlaceName.Value.Name.ToString().Trim();
            if (name.Length > 0)
                label = name;
        }

        if (label.Length == 0 && row.AethernetName.IsValid)
        {
            var name = row.AethernetName.Value.Name.ToString().Trim();
            if (name.Length > 0)
                label = name;
        }

        label = label.Replace(',', ' ').Trim();
        return label.Length > 0;
    }

    internal static bool TryFindNearestTeleportAetheryte(
        IDataManager data,
        uint territoryTypeRowId,
        string zonePlaceNameQuery,
        float mapX,
        float mapY,
        out uint aetheryteRowId,
        out string error)
    {
        aetheryteRowId = 0;
        error = "";

        try
        {
            if (!data.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeRowId, out var territoryType))
            {
                error = "Matched territory became invalid.";
                return false;
            }

            var defaultMapFromPickedTerritory =
                territoryType.Map.IsValid ? territoryType.Map.Value : default;

            var aetheryteSheet = data.GetExcelSheet<Aetheryte>();
            var mapsWithTerritoryFallback = data.GetExcelSheet<Map>();
            var territorySheet = data.GetExcelSheet<TerritoryType>();
            var crystalMarkersByRow = TryIndexTeleportCrystalMapMarkers(data);

            var bestId = 0u;
            var bestDist = float.MaxValue;

            void ConsiderRow(in Aetheryte row, in Map mapForMath)
            {
                if (mapForMath.RowId == 0)
                    return;

                if (!TryGetAetheryteWorldXZ(row, mapForMath, crystalMarkersByRow, out var wx, out var wz))
                    return;

                var ax = RawToMapCoord(wx, mapForMath.SizeFactor, mapForMath.OffsetX);
                var ay = RawToMapCoord(wz, mapForMath.SizeFactor, mapForMath.OffsetY);

                var d = (ax - mapX) * (ax - mapX) + (ay - mapY) * (ay - mapY);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestId = row.RowId;
                }
            }

            bool IsCandidateTeleportRow(in Aetheryte row) =>
                row.RowId != 0
                && row.Territory.IsValid
                && row.IsAetheryte;

            bool TryMapForRow(in Aetheryte row, out Map map)
            {
                if (row.Map.IsValid && row.Map.Value.RowId != 0)
                {
                    map = row.Map.Value;
                    return true;
                }

                foreach (var levelRef in row.Level)
                {
                    if (!levelRef.IsValid)
                        continue;

                    var lvl = levelRef.Value;
                    if (lvl.Map.IsValid && lvl.Map.Value.RowId != 0)
                    {
                        map = lvl.Map.Value;
                        return true;
                    }
                }

                var rowTerritoryId = row.Territory.Value.RowId;
                if (territorySheet.TryGetRow(rowTerritoryId, out var tt)
                    && tt.Map.IsValid
                    && tt.Map.Value.RowId != 0)
                {
                    map = tt.Map.Value;
                    return true;
                }

                foreach (var mr in mapsWithTerritoryFallback)
                {
                    if (mr.RowId == 0 || !mr.TerritoryType.IsValid)
                        continue;

                    if (mr.TerritoryType.Value.RowId != rowTerritoryId)
                        continue;

                    map = mr;
                    return true;
                }

                map = default;
                return false;
            }

            static void CollectMapsForPlaceQuery(
                ExcelSheet<TerritoryType> territories,
                ExcelSheet<Map>? mapsMaybe,
                string zoneQueryTrimmed,
                HashSet<uint> sink)
            {
                var threshold = 50;
                foreach (var tt in territories)
                {
                    if (tt.RowId == 0 || !tt.PlaceName.IsValid)
                        continue;

                    var name = TerritoryPlaceDisplayName(tt.PlaceName.Value.Name.ToString());

                    if (ZoneMatchScore(name, zoneQueryTrimmed) < threshold)
                        continue;

                    if (tt.Map.IsValid && tt.Map.Value.RowId != 0)
                        sink.Add(tt.Map.Value.RowId);
                }

                if (mapsMaybe == null)
                    return;

                foreach (var mr in mapsMaybe)
                {
                    if (mr.RowId == 0 || !mr.PlaceName.IsValid)
                        continue;

                    var mn = TerritoryPlaceDisplayName(mr.PlaceName.Value.Name.ToString());
                    if (ZoneMatchScore(mn, zoneQueryTrimmed) < threshold)
                        continue;

                    sink.Add(mr.RowId);
                }
            }

            var q = zonePlaceNameQuery.Trim();
            var mapIdsForZone = new HashSet<uint>();
            try
            {
                CollectMapsForPlaceQuery(
                    territorySheet,
                    data.GetExcelSheet<Map>(),
                    q,
                    mapIdsForZone);
            }
            catch (Exception)
            {
                CollectMapsForPlaceQuery(territorySheet, null, q, mapIdsForZone);
            }

            if (defaultMapFromPickedTerritory.RowId != 0)
                mapIdsForZone.Add(defaultMapFromPickedTerritory.RowId);

            foreach (var row in aetheryteSheet)
            {
                if (!IsCandidateTeleportRow(row))
                    continue;

                if (row.Territory.Value.RowId != territoryTypeRowId)
                    continue;

                if (!TryMapForRow(row, out var map))
                    continue;

                ConsiderRow(row, map);
            }

            if (bestId == 0)
            {
                foreach (var row in aetheryteSheet)
                {
                    if (!IsCandidateTeleportRow(row))
                        continue;

                    if (!territorySheet.TryGetRow(row.Territory.Value.RowId, out var tt) || !tt.PlaceName.IsValid)
                        continue;

                    var pname = TerritoryPlaceDisplayName(tt.PlaceName.Value.Name.ToString());
                    if (ZoneMatchScore(pname, q) < 50)
                        continue;

                    if (!TryMapForRow(row, out var map))
                        continue;

                    ConsiderRow(row, map);
                }
            }

            if (bestId == 0 && mapIdsForZone.Count > 0)
            {
                foreach (var row in aetheryteSheet)
                {
                    if (!IsCandidateTeleportRow(row))
                        continue;

                    if (!TryMapForRow(row, out var map))
                        continue;

                    if (!mapIdsForZone.Contains(map.RowId))
                        continue;

                    ConsiderRow(row, map);
                }
            }

            if (bestId == 0)
            {
                error = "No teleport aetheryte was found in that zone’s data.";
                return false;
            }

            aetheryteRowId = bestId;
            return true;
        }
        catch (Exception)
        {
            error = "Game data sheets are not available.";
            return false;
        }
    }

    private static bool TryGetAetheryteWorldXZ(
        in Aetheryte row,
        in Map mapHint,
        IReadOnlyDictionary<uint, (int X, int Y)> crystalMarkersOnMapByAetheryteId,
        out float wx,
        out float wz)
    {
        wx = wz = 0;

        foreach (var levelRef in row.Level)
        {
            if (!levelRef.IsValid)
                continue;

            var l = levelRef.Value;
            wx = LevelScalarToWorld(l.X);
            wz = LevelScalarToWorld(l.Z);
            return true;
        }

        if (!row.IsAetheryte || mapHint.RowId == 0)
            return false;

        if (!crystalMarkersOnMapByAetheryteId.TryGetValue(row.RowId, out var mapMarker))
            return false;

        wx = MapMarkerAxisToRawWorld(mapMarker.X, mapHint.SizeFactor);
        wz = MapMarkerAxisToRawWorld(mapMarker.Y, mapHint.SizeFactor);
        return true;
    }

    private static Dictionary<uint, (int X, int Y)> TryIndexTeleportCrystalMapMarkers(IDataManager data)
    {
        var result = new Dictionary<uint, (int X, int Y)>();

        try
        {
            foreach (var m in GenericHelpers.AllRows(data.GetSubrowExcelSheet<MapMarker>()))
            {
                if (m.DataType != 3)
                    continue;

                result[m.DataKey.RowId] = (m.X, m.Y);
            }
        }
        catch (Exception)
        {
        }

        return result;
    }

    private static float MapMarkerAxisToRawWorld(int markerPos, ushort mapSizeFactor)
    {
        var num = mapSizeFactor / 100f;
        return (markerPos - 1024f) / num;
    }

    private static float LevelScalarToWorld(float v) => v;

    private static float LevelScalarToWorld(int v) => v / 1000f;

    private static int ZoneMatchScore(string territoryPlaceName, string zoneQuery)
    {
        if (territoryPlaceName.Length == 0 || zoneQuery.Length == 0)
            return 0;

        if (string.Equals(territoryPlaceName, zoneQuery, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (territoryPlaceName.StartsWith(zoneQuery, StringComparison.OrdinalIgnoreCase))
            return 90;

        if (territoryPlaceName.Contains(zoneQuery, StringComparison.OrdinalIgnoreCase))
            return 80;

        if (zoneQuery.Contains(territoryPlaceName, StringComparison.OrdinalIgnoreCase))
            return 70;

        return 0;
    }

    private static string TerritoryPlaceDisplayName(string rawArea)
    {
        var dashIndex = rawArea.LastIndexOf(" - ", StringComparison.Ordinal);
        return dashIndex >= 0 ? rawArea[(dashIndex + 3)..].Trim() : rawArea.Trim();
    }

    private static float RawToMapCoord(float rawPos, ushort sizeFactor, short offset)
    {
        var c = sizeFactor / 100f;
        return MathF.Round(((rawPos + offset) / c + 1024f) / 2048f * 41f + 1f, 1);
    }
}
