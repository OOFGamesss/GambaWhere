using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GambaWhere.API.Models;
using GambaWhere.IPC;
using GambaWhere.Utility;

namespace GambaWhere.Services;

/// <summary>Teleports the player towards an event's venue using Lifestream and aetheryte travel.</summary>
public sealed class EventLocationTeleportService
{
    internal const string ErrorPrefix = "GambaWhere teleport";

    private static readonly string BusyMessage =
        "Lifestream is already teleporting elsewhere; wait until that finishes.";

    private static readonly string LifestreamDownMessage =
        "Install NightmareXIV Lifestream, enable it on this character, reload plugins, then try again.";

    private static readonly string WardParseMessage =
        "That address could not be converted by Lifestream. Confirm world spelling, residential district wording (for example Mist, Goblet, Lavender Beds, Shirogane, Empyreum), plus ward and plot.";

    private static readonly string CannotReachWorldMessage =
        "That venue’s world was not recognized for travel on this character (check spelling). Enable DC visits in Nightmare Lifestream if crossing data centers.";

    private static readonly string OpenWorldNoCrystalNameMessage =
        "Could not derive a teleport name for the chosen crystal; try teleporting manually or update the listing’s world/coordinates.";

    private static readonly string WardSubdivisionEnqueueFailed =
        "Could not queue a teleport into that housing ward (outside a plot); try targeting the subdivision manually in Lifestream if this keeps happening.";

    private static readonly string WardTeleportFallbackHint =
        "If nothing starts, finish cutscenes or blocking UI and try again. Lifestream address line (paste in chat):";

    private static readonly string OpenWorldTeleportFailed =
        "Could not queue a paid teleport to the nearest aetheryte in that zone through Lifestream (try being interactable, or teleport manually).";

    private readonly IChatGui _chat;
    private readonly IDataManager _data;
    private readonly IObjectTable _objects;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;

    public bool IsLifestreamAvailable =>
        _pluginInterface.InstalledPlugins.Any(static p =>
            p.InternalName == LifestreamHouseIpc.RequiredPluginInternalName && p.IsLoaded);

    public EventLocationTeleportService(
        IDalamudPluginInterface pluginInterface,
        IDataManager dataManager,
        IObjectTable objectTable,
        IChatGui chatGui,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _data = dataManager;
        _objects = objectTable;
        _chat = chatGui;
        _log = log;
    }

    private bool IsOnResolvedWorld(string destinationWorld)
    {
        var w = destinationWorld.Trim();
        if (w.Length == 0)
            return false;

        var player = _objects.LocalPlayer;
        if (player == null)
            return false;

        var current = player.CurrentWorld.Value.Name.ToString();
        return current.Equals(w, StringComparison.OrdinalIgnoreCase);
    }

    public void RequestTravel(EventResponse listing)
    {
        if (!IsLifestreamAvailable)
        {
            PrintFault(LifestreamDownMessage);
            return;
        }

        if (EventTravelLocation.TryResolveOpenWorld(listing, _data, out var openWorld, out var openWorldError))
        {
            if (!LifestreamHouseIpc.TryQueryBusy(_pluginInterface, _log, out var lifestreamBusy))
            {
                PrintFault(LifestreamDownMessage);
                return;
            }

            if (lifestreamBusy)
            {
                PrintFault(BusyMessage);
                return;
            }

            if (IsOnResolvedWorld(openWorld.World.Trim()))
            {
                _log.Information("[Teleport] Lifestream.Teleport aetheryte {AetheryteRowId} (world: {World}).",
                    openWorld.AetheryteRowId, openWorld.World.Trim());
                if (!LifestreamHouseIpc.TryTeleportToAetheryte(_pluginInterface, openWorld.AetheryteRowId, 0, _log))
                    PrintFault(OpenWorldTeleportFailed);

                return;
            }

            if (!LifestreamHouseIpc.TryQueryCanReachWorldViaLifestream(_pluginInterface, openWorld.World, _log,
                    out var canReachDestinationWorld))
            {
                PrintFault(LifestreamDownMessage);
                return;
            }

            if (!canReachDestinationWorld)
            {
                PrintFault(CannotReachWorldMessage);
                return;
            }

            if (!OpenWorldNearestAetheryte.TryGetLifestreamTeleportSearchLabel(_data, openWorld.AetheryteRowId,
                    out var tpLabel))
            {
                PrintFault(OpenWorldNoCrystalNameMessage);
                return;
            }

            var openWorldLiAddress = $"{openWorld.World.Trim()}, tp {tpLabel.Trim()}";
            _log.Information("[Teleport] Lifestream.ExecuteCommand /li {Address}.", openWorldLiAddress);
            if (!LifestreamHouseIpc.TryEnqueueLiSlashArgs(_pluginInterface, openWorldLiAddress, _log))
                PrintFault(OpenWorldTeleportFailed);

            return;
        }

        if (!string.IsNullOrEmpty(openWorldError))
        {
            PrintFault(openWorldError);
            return;
        }

        if (!EventTravelLocation.TryResolve(listing, out var resolved, out var fault))
        {
            PrintFault(fault);
            return;
        }

        if (!LifestreamHouseIpc.TryQueryBusy(_pluginInterface, _log, out var busyFlag))
        {
            PrintFault(LifestreamDownMessage);
            return;
        }

        if (busyFlag)
        {
            PrintFault(BusyMessage);
            return;
        }

        if (!resolved.HasSpecificPlot)
        {
            var liAddressLine = EventTravelLocation.FormatLifestreamHousingAddress(resolved);

            if (!IsOnResolvedWorld(resolved.World))
            {
                if (!LifestreamHouseIpc.TryQueryCanReachWorldViaLifestream(_pluginInterface, resolved.World, _log,
                        out var housingWorldOk))
                {
                    PrintFault(LifestreamDownMessage);
                    return;
                }

                if (!housingWorldOk)
                {
                    PrintFault(CannotReachWorldMessage);
                    return;
                }
            }

            _log.Information("[Teleport] Lifestream.ExecuteCommand /li {Address}.", liAddressLine);
            if (!LifestreamHouseIpc.TryEnqueueLiSlashArgs(_pluginInterface, liAddressLine, _log))
            {
                PrintFault(WardSubdivisionEnqueueFailed);
                return;
            }

            PrintWardTeleportHint(liAddressLine);
            return;
        }

        if (!IsOnResolvedWorld(resolved.World))
        {
            if (!LifestreamHouseIpc.TryQueryCanReachWorldViaLifestream(_pluginInterface, resolved.World, _log,
                    out var plotWorldOk))
            {
                PrintFault(LifestreamDownMessage);
                return;
            }

            if (!plotWorldOk)
            {
                PrintFault(CannotReachWorldMessage);
                return;
            }
        }

        if (!LifestreamHouseIpc.TryBuildAddressTuple(_pluginInterface, resolved.World, resolved.Area, resolved.Ward,
                resolved.Plot, resolved.IsApartment, resolved.Subdivision, _log, out var tuple))
        {
            PrintFault(WardParseMessage);
            return;
        }

        var housingAddress = EventTravelLocation.FormatLifestreamHousingAddress(resolved);
        tuple = LifestreamHouseIpc.WithHousingAddressLabel(tuple, housingAddress);

        _log.Information("[Teleport] Lifestream.GoToHousingAddress {Address}.", housingAddress);
        if (!LifestreamHouseIpc.TryEnqueueGo(_pluginInterface, tuple, _log))
            PrintFault(LifestreamDownMessage);
    }

    private void PrintWardTeleportHint(string liFragmentNoSlash)
    {
        _chat.Print($"{ErrorPrefix}: {WardTeleportFallbackHint} /li {liFragmentNoSlash}");
    }

    private void PrintFault(string sentence)
    {
        var trimmed = sentence.Trim();
        if (!trimmed.EndsWith('.'))
            trimmed += '.';

        _chat.PrintError($"{ErrorPrefix}: {trimmed}");
    }
}
