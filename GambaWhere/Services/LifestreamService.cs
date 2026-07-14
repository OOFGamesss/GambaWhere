using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Models;

/// <summary>
/// Teleports to an event's venue via Lifestream's /li command, using the residential address when available
/// and otherwise the nearest aetheryte to the listing's open-world coordinates.
/// </summary>

namespace GambaWhere.Services;

public sealed partial class LifestreamService
{
    public const uint LinkId = 1;

    private const string LifestreamPlugin = "Lifestream";
    private const string ErrorPrefix = "[GambaWhere Teleport]";
    private const string LocationSeparator = " • ";

    private const string LifestreamMissing =
        "Install NightmareXIV Lifestream, enable it on this character, reload plugins, then try again.";
    private const string Busy = "Lifestream is already teleporting elsewhere; wait until that finishes.";
    private const string NoLocation = "This listing has no usable location to travel to.";
    private const string NoAetheryte = "Could not find an aetheryte near that open-world location; teleport manually.";

    private readonly IDalamudPluginInterface _pi;
    private readonly PlayerInfoService _playerInfo;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;

    private readonly ICallGateSubscriber<string, object?> _executeCommand;
    private readonly ICallGateSubscriber<bool> _isBusy;

    public LifestreamService(
        IDalamudPluginInterface pluginInterface,
        PlayerInfoService playerInfo,
        IChatGui chatGui,
        IPluginLog log)
    {
        _pi = pluginInterface;
        _playerInfo = playerInfo;
        _chat = chatGui;
        _log = log;

        _executeCommand = pluginInterface.GetIpcSubscriber<string, object?>("Lifestream.ExecuteCommand");
        _isBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
    }

    public bool IsLifestreamAvailable =>
        _pi.InstalledPlugins.Any(static p => p.InternalName == LifestreamPlugin && p.IsLoaded);

    public void RequestTravel(EventResponse listing)
    {
        if (!IsLifestreamAvailable)
        {
            PrintFault(LifestreamMissing);
            return;
        }

        if (IsBusy())
        {
            PrintFault(Busy);
            return;
        }

        if (!TryBuildDestination(listing, out var destination, out var error))
        {
            PrintFault(error);
            return;
        }

        _log.Information("[Teleport] /li {Destination}", destination);
        ExecuteCommand(destination);
    }

    private bool TryBuildDestination(EventResponse listing, out string destination, out string error)
    {
        destination = string.Empty;
        error = string.Empty;

        if (TryBuildResidentialCode(listing, out destination))
            return true;

        if (TryBuildAetheryte(listing, out destination, out error))
            return true;

        if (string.IsNullOrEmpty(error))
            error = NoLocation;
        return false;
    }

    private bool TryBuildResidentialCode(EventResponse listing, out string code)
    {
        code = string.Empty;

        if (!TryResolveAddress(listing, out var world, out var district, out var ward, out var plot, out var isApartment)
            || ward <= 0)
            return false;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(world) && !IsSameWorld(world))
            parts.Add(world);
        if (!string.IsNullOrWhiteSpace(district))
            parts.Add(district);

        parts.Add($"W{ward}");
        parts.Add(plot > 0 ? (isApartment ? $"A{plot}" : $"P{plot}") : "P0");

        code = string.Join(" ", parts);
        return true;
    }

    private bool TryBuildAetheryte(EventResponse listing, out string destination, out string error)
    {
        destination = string.Empty;
        error = string.Empty;

        if (!TrySplitLocation(listing.Location, out var segments)
            || !TryFindMapCoords(segments, out var index, out var mapX, out var mapY))
            return false;

        var area = index > 0 ? segments[index - 1] : string.Empty;
        var aetheryte = _playerInfo.GetClosestAetheryte(area, mapX, mapY);
        if (string.IsNullOrEmpty(aetheryte))
        {
            error = NoAetheryte;
            return false;
        }

        var world = !string.IsNullOrWhiteSpace(listing.World) ? listing.World!
            : index > 1 ? segments[index - 2] : string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(world) && !IsSameWorld(world))
            parts.Add(world);
        if (!string.IsNullOrWhiteSpace(area) && !area.Equals(aetheryte, StringComparison.OrdinalIgnoreCase))
            parts.Add(area);
        parts.Add(aetheryte);

        destination = string.Join(", ", parts);
        return true;
    }

    private bool IsSameWorld(string world)
    {
        var current = _playerInfo.GetCurrentWorld();
        return !string.IsNullOrWhiteSpace(current)
            && current.Equals(world, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveAddress(
        EventResponse listing, out string world, out string district, out int ward, out int plot, out bool isApartment)
    {
        world = listing.World ?? string.Empty;
        district = listing.Area ?? string.Empty;
        ward = listing.Ward ?? 0;
        plot = listing.Plot ?? 0;
        isApartment = listing.IsApartment ?? false;

        if (!string.IsNullOrWhiteSpace(world) && ward > 0)
            return true;

        return TryParseLocation(listing.Location, ref world, ref district, ref ward, ref plot);
    }

    private static bool TryParseLocation(string location, ref string world, ref string district, ref int ward, ref int plot)
    {
        if (!TrySplitLocation(location, out var segments))
            return false;

        foreach (var segment in segments)
        {
            var wardMatch = RxWard().Match(segment);
            if (wardMatch.Success && int.TryParse(wardMatch.Groups[1].Value, out var parsedWard))
            {
                ward = parsedWard;
                continue;
            }

            var plotMatch = RxPlot().Match(segment);
            if (plotMatch.Success && int.TryParse(plotMatch.Groups[1].Value, out var parsedPlot))
                plot = parsedPlot;
        }

        if (string.IsNullOrWhiteSpace(world) && segments.Length >= 2)
            world = segments[1];
        if (string.IsNullOrWhiteSpace(district) && segments.Length >= 3)
            district = segments[2];

        return ward > 0 && !string.IsNullOrWhiteSpace(world);
    }

    private static bool TrySplitLocation(string? location, out string[] segments)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            segments = Array.Empty<string>();
            return false;
        }

        segments = location.Split(LocationSeparator, StringSplitOptions.TrimEntries);
        return true;
    }

    private static bool TryFindMapCoords(string[] segments, out int index, out float mapX, out float mapY)
    {
        for (index = 0; index < segments.Length; index++)
        {
            var match = RxMapCoords().Match(segments[index]);
            if (match.Success
                && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mapX)
                && float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mapY))
                return true;
        }

        index = -1;
        mapX = 0f;
        mapY = 0f;
        return false;
    }

    private bool IsBusy()
    {
        try
        {
            return _isBusy.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Lifestream IsBusy query failed.");
            return false;
        }
    }

    private void ExecuteCommand(string arguments)
    {
        try
        {
            _executeCommand.InvokeAction(arguments);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Lifestream ExecuteCommand failed for /li {Args}.", arguments);
        }
    }

    private void PrintFault(string message)
    {
        var text = message.Trim();
        if (!text.EndsWith('.'))
            text += '.';

        _chat.PrintError($"{ErrorPrefix} {text}");
    }

    [GeneratedRegex(@"X:\s*(-?[0-9]+(?:\.[0-9]+)?)\s*,\s*Y:\s*(-?[0-9]+(?:\.[0-9]+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxMapCoords();

    [GeneratedRegex(@"\bWard\s+([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxWard();

    [GeneratedRegex(@"\bPlot\s+([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxPlot();
}
