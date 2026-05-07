using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using GambaWhere.API.Models;

namespace GambaWhere.Utility;

public static class EventTravelLocation
{
    private static readonly Regex RxWardSpelled =
        new(@"\bWard\D{0,4}(\d{1,2})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxPlotSpelled =
        new(@"\bPlot\D{0,4}(\d{1,2})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxWardLetter =
        new(@"\b[wW]\s*[-#.]\s*(\d{1,2})\b(?!\d)", RegexOptions.CultureInvariant);

    private static readonly Regex RxPlotLetter =
        new(@"\b[pP]\s*[-#.]\s*(\d{1,2})\b(?!\d)", RegexOptions.CultureInvariant);

    private static readonly Regex RxMist = new(@"\b(?:the\s*)?mist\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxLavender =
        new(@"\b(?:the\s*)?(?:lavender\s+beds|lavender\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxGoblet =
        new(@"\b(?:the\s*)?goblet\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxShiro =
        new(@"\bshiro(?:gane)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxEmpy =
        new(@"\bempy(?:reum)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] WorldRuleKeys =
    {
        "world", "home_world", "target_world", "server", "realm", "homeworld",
    };

    private static readonly string[] AreaRuleKeys =
    {
        "area", "housing_area", "district", "residential_area", "housing_district", "city", "housing",
    };

    private static readonly string[] WardRuleKeys =
    {
        "ward", "housing_ward", "ward_number", "ward_no", "Ward",
    };

    private static readonly string[] PlotRuleKeys =
    {
        "plot", "housing_plot", "plot_number", "plot_no", "Plot",
        "apartment_number", "flat",
    };

    private static readonly string[] CompositeRuleStringKeys =
    {
        "location_notes", "full_address", "address", "housing_address", "venue_address", "where", "instructions",
    };

    public readonly record struct Resolved(
        string World,
        string Area,
        int Ward,
        int Plot,
        bool HasSpecificPlot,
        bool IsApartment,
        bool Subdivision);

    private static readonly Regex RxOpenWorldMapCoords = new(
        @"\bX:\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*Y:\s*([0-9]+(?:\.[0-9]+)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public readonly record struct OpenWorldResolved(
        string World,
        string ZoneDisplayName,
        uint TerritoryTypeRowId,
        uint AetheryteRowId,
        float MapX,
        float MapY);

    public static bool TryResolveOpenWorld(EventResponse ev, IDataManager data, out OpenWorldResolved resolved,
        out string error)
    {
        resolved = default;
        error = "";

        var haystack = BuildHaystack(ev);
        var m = RxOpenWorldMapCoords.Match(haystack);

        if (!m.Success || m.Groups.Count < 3)
            return false;

        if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mapX)
            || !float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mapY))
            return false;

        if (PreferHousingPathOverCoordinates(ev, haystack))
            return false;

        var world = FirstNonEmpty(ev.World, TryRulesString(ev.Rules, WorldRuleKeys));
        if (string.IsNullOrWhiteSpace(world))
            world = TryTaggedWorld(haystack) ?? TryWorldPipeHeuristic(haystack) ?? "";

        if (string.IsNullOrWhiteSpace(world))
        {
            error =
                "Open-world teleport needs a server/world name in the listing or in text such as \"World: Phantom\".";

            return false;
        }

        if (!ExtractOpenWorldZoneLabel(haystack, m.Index, m.Length, world.Trim(), out var zoneLabel))
        {
            error =
                "Could not read a zone name beside the map coordinates. Use a separator like • between DC, world, and zone.";
            return false;
        }

        if (!OpenWorldNearestAetheryte.TryPickTerritoryByPlaceName(data, zoneLabel, out var territoryRowId,
                out error))
            return false;

        if (!OpenWorldNearestAetheryte.TryFindNearestTeleportAetheryte(data, territoryRowId, zoneLabel, mapX, mapY,
                out var aetheryteRowId, out error))
            return false;

        resolved =
            new OpenWorldResolved(world.Trim(), zoneLabel, territoryRowId, aetheryteRowId, mapX, mapY);
        error = "";

        return true;
    }

    private static bool PreferHousingPathOverCoordinates(EventResponse ev, string haystack)
    {
        if (InferDistrictHaystack(haystack) == null)
            return false;

        if (RxWardSpelled.IsMatch(haystack) || RxWardLetter.IsMatch(haystack))
            return true;

        if (ev.Ward is >= 1 and <= 30)
            return true;

        if (TryRulesInt(ev.Rules, WardRuleKeys) is { } rw && rw is >= 1 and <= 30)
            return true;

        if (ev.Plot is >= 1 and <= 60)
            return true;

        if (TryRulesInt(ev.Rules, PlotRuleKeys) is { } rp && rp is >= 1 and <= 60)
            return true;

        return false;
    }

    private static bool ExtractOpenWorldZoneLabel(string haystack, int coordIndex, int coordLen, string worldToken,
        out string zoneLabel)
    {
        zoneLabel = "";

        var stripped = string.Concat(haystack.AsSpan(0, coordIndex), haystack.AsSpan(coordIndex + coordLen)).Trim();

        if (stripped.Length == 0)
            return false;

        stripped = RxVenueBulletSeparators.Replace(stripped, "|");

        var pieces = new List<string>();

        foreach (var line in stripped.Split(HaystackLines, StringSplitOptions.RemoveEmptyEntries |
                                                              StringSplitOptions.TrimEntries))
        {
            foreach (var seg in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (seg.Length == 0)
                    continue;

                if (KnownDcPipelineTokens.Contains(seg))
                    continue;

                if (worldToken.Length > 0
                    && seg.Equals(worldToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                pieces.Add(seg);
            }
        }

        if (pieces.Count == 0)
            return false;

        zoneLabel = string.Join(' ', pieces).Trim();
        return zoneLabel.Length > 0;
    }

    public static bool TryResolve(EventResponse ev, out Resolved resolved, out string error)
    {
        resolved = default;
        error = "";

        var haystack = BuildHaystack(ev);

        var world = FirstNonEmpty(ev.World, TryRulesString(ev.Rules, WorldRuleKeys));
        if (string.IsNullOrWhiteSpace(world))
            world = TryTaggedWorld(haystack) ?? TryWorldPipeHeuristic(haystack) ?? "";

        var area = FirstNonEmpty(ev.Area, TryRulesString(ev.Rules, AreaRuleKeys));
        if (string.IsNullOrWhiteSpace(area))
            area = InferDistrictHaystack(haystack) ?? "";

        if (!TryResolveWard(ev, haystack, out var ward))
        {
            error =
                "Could not read a ward for this listing. Ask the host to include ward in event fields, or add text such as \"Ward 12\" or \"w.12\" in the description or venue line.";
            return false;
        }

        var subdivisionFromRules = TryRulesBool(ev.Rules,
            new[] { "subdivision", "housing_subdivision", "is_subdivision" });
        var subdivision = FirstBool(ev.Subdivision, subdivisionFromRules, false)
                          || haystack.Contains("subdivision", StringComparison.OrdinalIgnoreCase);

        var apartmentCandidate = FirstNullableBool(
            ev.IsApartment,
            TryRulesBool(ev.Rules, new[] { "is_apartment", "apartment", "flat_type" }));

        var provisionalApartment = apartmentCandidate
                                   ?? (InferApartment(area, subdivision) || HasApartmentCue(haystack));

        var hasSpecificPlot = TryResolvePlot(ev, haystack, out var plot);

        if (!hasSpecificPlot)
        {
            if (provisionalApartment)
            {
                error =
                    "Apartment venues need an apartment number in the listing. Add structured data or wording such as \"Apartment ########\".";
                return false;
            }

            if (ward is < 1 or > 30)
            {
                error = "Ward value is outside the valid range.";
                return false;
            }

            resolved = new Resolved(world, area, ward, Plot: 0, HasSpecificPlot: false,
                IsApartment: false, subdivision);
            return Validate(resolved, ev, out error);
        }

        if (ward is < 1 or > 30 || plot is < 1 or > 60)
        {
            error = "Ward or plot values are outside the valid range.";
            return false;
        }

        var isApartment = provisionalApartment;

        resolved = new Resolved(world, area, ward, plot, HasSpecificPlot: true, isApartment, subdivision);
        return Validate(resolved, ev, out error);
    }

    private static readonly char[] HaystackLines = ['\r', '\n'];

    private static readonly HashSet<string> KnownDcPipelineTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Aether", "Crystal", "Dynamis", "Primal", "Chaos", "Light", "Elemental", "Gaia", "Mana", "Meteor",
            "Materia",
        };

    private static readonly Regex[] WardHaystackMatchers = [RxWardSpelled, RxWardLetter];

    private static readonly Regex[] PlotHaystackMatchers = [RxPlotSpelled, RxPlotLetter];

    private static readonly Regex RxTaggedWorld = new(
        @"\b(?:Server|Realm|Homeworld|World)\b[\s:/]+([\w'-]{3,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string BuildHaystack(EventResponse ev)
    {
        var parts = new List<string>(12);
        AppendLine(ev.Location);
        AppendLine(ev.VenueName);
        AppendLine(ev.Description);

        foreach (var k in CompositeRuleStringKeys)
            if (ev.Rules.TryGetValue(k, out var raw) && TryString(raw, out var snippet))
                parts.Add(snippet);

        return string.Join(Environment.NewLine, parts);

        void AppendLine(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s))
                parts.Add(s.Trim());
        }
    }

    private static bool TryResolveWard(EventResponse ev, string haystack, out int ward)
        => TryInt(ev.Ward, TryRulesInt(ev.Rules, WardRuleKeys), out ward)
           || TryHaystackInt(haystack, WardHaystackMatchers, 30, out ward);

    private static bool TryResolvePlot(EventResponse ev, string haystack, out int plot)
        => TryInt(ev.Plot, TryRulesInt(ev.Rules, PlotRuleKeys), out plot)
           || TryHaystackInt(haystack, PlotHaystackMatchers, 60, out plot);

    private static bool TryHaystackInt(string haystack, Regex[] matchers, int maxInclusive, out int value)
    {
        value = 0;
        foreach (var rx in matchers)
        {
            var m = rx.Match(haystack);
            if (!m.Success || m.Groups.Count < 2)
                continue;

            if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n >= 1 && n <= maxInclusive)
            {
                value = n;
                return true;
            }
        }

        return false;
    }

    private static string? InferDistrictHaystack(string haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack))
            return null;

        if (RxEmpy.IsMatch(haystack))
            return "Empyreum";
        if (RxShiro.IsMatch(haystack))
            return "Shirogane";
        if (RxGoblet.IsMatch(haystack))
            return "The Goblet";
        if (RxLavender.IsMatch(haystack))
            return "The Lavender Beds";
        if (RxMist.IsMatch(haystack))
            return "The Mist";

        return null;
    }

    private static string? TryTaggedWorld(string haystack)
    {
        var m = RxTaggedWorld.Match(haystack);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static readonly Regex RxVenueBulletSeparators =
        new(@"[\u2022\u2023\u00b7]", RegexOptions.CultureInvariant);

    private static string CanonicalizeVenueDelimiters(string line) =>
        RxVenueBulletSeparators.Replace(line, "|");

    private static string? TryWorldPipeHeuristic(string haystack)
    {
        foreach (var line in haystack.Split(HaystackLines, StringSplitOptions.RemoveEmptyEntries |
                                                         StringSplitOptions.TrimEntries))
        {
            var normalized = CanonicalizeVenueDelimiters(line);
            if (!normalized.Contains('|', StringComparison.Ordinal))
                continue;

            foreach (var slice in normalized.Split('|', StringSplitOptions.TrimEntries |
                                                          StringSplitOptions.RemoveEmptyEntries))
            {
                if (slice.Length < 2 || KnownDcPipelineTokens.Contains(slice))
                    continue;
                if (SliceLooksEmbeddedHousing(slice))
                    continue;

                return slice.Trim();
            }
        }

        return null;
    }

    private static bool SliceLooksEmbeddedHousing(string slice) =>
        RxWardSpelled.IsMatch(slice) || RxPlotSpelled.IsMatch(slice) || InferDistrictHaystack(slice) != null;

    private static bool Validate(Resolved r, EventResponse ev, out string error)
    {
        error = "";

        var dcTail = ListingDcTail(ev.DataCentre);

        if (string.IsNullOrWhiteSpace(r.World))
        {
            error = HintDcTail("No world name is available for teleporting.", dcTail);
            return false;
        }

        if (string.IsNullOrWhiteSpace(r.Area))
        {
            error = HintDcTail("No residential district (area) is available for teleporting.", dcTail);
            return false;
        }

        return true;
    }

    private static string? ListingDcTail(string? dc)
        => string.IsNullOrWhiteSpace(dc) ? null : $" Listing data centre: {dc.Trim()}.";

    private static string HintDcTail(string core, string? tail) => $"{core}{tail ?? ""}";

    private static bool InferApartment(string area, bool subdivision)
        => subdivision || HasApartmentCue(area);

    private static bool HasApartmentCue(string s)
        => s.Contains("flat", StringComparison.OrdinalIgnoreCase)
           || s.Contains("apartment", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a!.Trim() : (!string.IsNullOrWhiteSpace(b) ? b!.Trim() : "");

    private static bool TryInt(int? primary, int? fallback, out int value)
    {
        if (primary is { } p)
        {
            value = p;
            return true;
        }

        if (fallback is { } f)
        {
            value = f;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool FirstBool(bool? a, bool? b, bool defaultValue) => a ?? b ?? defaultValue;

    private static bool? FirstNullableBool(bool? a, bool? b) => a ?? b;

    private static string? TryRulesString(Dictionary<string, object> rules, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!rules.TryGetValue(key, out var raw))
                continue;
            if (TryString(raw, out var s))
                return s;
        }

        return null;
    }

    private static int? TryRulesInt(Dictionary<string, object> rules, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!rules.TryGetValue(key, out var raw))
                continue;
            if (TryIntRaw(raw, out var i))
                return i;
        }

        return null;
    }

    private static bool? TryRulesBool(Dictionary<string, object> rules, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!rules.TryGetValue(key, out var raw))
                continue;
            if (TryBool(raw, out var b))
                return b;
        }

        return null;
    }

    private static bool TryString(object? raw, out string value)
    {
        value = "";
        switch (raw)
        {
            case string s:
                value = s.Trim();
                return value.Length > 0;
            case JsonElement el when el.ValueKind == JsonValueKind.String:
                value = el.GetString()?.Trim() ?? "";
                return value.Length > 0;
            default:
                return false;
        }
    }

    private static bool TryIntRaw(object? raw, out int value)
    {
        value = 0;
        switch (raw)
        {
            case int i:
                value = i;
                return true;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                value = (int)l;
                return true;
            case double d when Math.Abs(d - Math.Round(d)) < 0.0000001:
                value = (int)Math.Round(d);
                return true;
            case float f when Math.Abs(f - MathF.Round(f)) < 0.0001f:
                value = (int)MathF.Round(f);
                return true;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var ji):
                value = ji;
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.String
                                    && int.TryParse(el.GetString(), NumberStyles.Integer,
                                        CultureInfo.InvariantCulture, out var jp):
                value = jp;
                return true;
            default:
                return false;
        }
    }

    private static bool TryBool(object? raw, out bool value)
    {
        value = false;
        switch (raw)
        {
            case bool b:
                value = b;
                return true;
            case string s when bool.TryParse(s, out var pb):
                value = pb;
                return true;
            case JsonElement el when el.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = el.GetBoolean();
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.String
                                    && bool.TryParse(el.GetString(), out var ps):
                value = ps;
                return true;
            default:
                return false;
        }
    }
}
