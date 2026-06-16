using System;
using System.Globalization;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GambaWhere.IPC;

using HousingAddrTuple =
    (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment,
        bool ApartmentSubdivision, bool AliasEnabled, string Alias);

/// <summary>IPC wrapper for Lifestream house and travel commands.</summary>
internal static class LifestreamHouseIpc
{
    internal const string RequiredPluginInternalName = "Lifestream";

    internal const string GateBuildAddress = RequiredPluginInternalName + ".BuildAddressBookEntry";
    internal const string GateGoToAddress = RequiredPluginInternalName + ".GoToHousingAddress";
    internal const string GateIsBusy = RequiredPluginInternalName + ".IsBusy";
    internal const string GateCanVisitSameDc = RequiredPluginInternalName + ".CanVisitSameDC";
    internal const string GateCanVisitCrossDc = RequiredPluginInternalName + ".CanVisitCrossDC";
    internal const string GateExecuteCommand = RequiredPluginInternalName + ".ExecuteCommand";
    internal const string GateTeleportToAetheryte = RequiredPluginInternalName + ".Teleport";

    internal static bool TryQueryBusy(IDalamudPluginInterface pi, IPluginLog log, out bool busy)
    {
        busy = false;

        try
        {
            var sub = pi.GetIpcSubscriber<bool>(GateIsBusy);
            busy = sub.InvokeFunc();
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "{Gate} could not run (likely Lifestream missing).", GateIsBusy);
            return false;
        }
    }

    internal static bool TryBuildAddressTuple(
        IDalamudPluginInterface pi,
        string world,
        string city,
        int ward,
        int plotOrApartment,
        bool apartment,
        bool subdivision,
        IPluginLog log,
        out HousingAddrTuple tuple)
    {
        tuple = default;

        try
        {
            var sub =
                pi.GetIpcSubscriber<string, string, string, string, bool, bool, HousingAddrTuple>(
                    GateBuildAddress);
            tuple = sub.InvokeFunc(world, city, ward.ToString(CultureInfo.InvariantCulture),
                plotOrApartment.ToString(CultureInfo.InvariantCulture), apartment, subdivision);
            return tuple.World != 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex,
                "{Gate} could not marshal that street address.",
                GateBuildAddress);
            return false;
        }
    }

    internal static HousingAddrTuple WithHousingAddressLabel(in HousingAddrTuple tuple, string label)
    {
        var labelled = tuple;
        labelled.Name = label;
        return labelled;
    }

    internal static bool TryEnqueueGo(IDalamudPluginInterface pi, in HousingAddrTuple tuple, IPluginLog log)
    {
        try
        {
            var sub = pi.GetIpcSubscriber<HousingAddrTuple, object>(GateGoToAddress);
            sub.InvokeAction(tuple);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "{Gate} rejected the teleport request.", GateGoToAddress);
            return false;
        }
    }

    internal static bool TryQueryCanReachWorldViaLifestream(IDalamudPluginInterface pi, string worldName,
        IPluginLog log,
        out bool canReach)
    {
        canReach = false;

        try
        {
            var trimmed = worldName.Trim();
            if (trimmed.Length == 0)
                return false;

            var same = pi.GetIpcSubscriber<string, bool>(GateCanVisitSameDc).InvokeFunc(trimmed);
            var cross = pi.GetIpcSubscriber<string, bool>(GateCanVisitCrossDc).InvokeFunc(trimmed);
            canReach = same || cross;
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not query Lifestream DC/world eligibility.");
            return false;
        }
    }

    internal static bool TryTeleportToAetheryte(
        IDalamudPluginInterface pi,
        uint aetheryteRowId,
        byte subIndex,
        IPluginLog log)
    {
        try
        {
            var sub = pi.GetIpcSubscriber<uint, byte, bool>(GateTeleportToAetheryte);
            return sub.InvokeFunc(aetheryteRowId, subIndex);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "{Gate} threw whilst requesting a paid teleport.", GateTeleportToAetheryte);
            return false;
        }
    }

    internal static bool TryEnqueueLiSlashArgs(IDalamudPluginInterface pi, string arguments, IPluginLog log)
    {
        try
        {
            var sub = pi.GetIpcSubscriber<string, object>(GateExecuteCommand);
            sub.InvokeAction(arguments);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "{Gate} threw whilst dispatching a Lifestream command.", GateExecuteCommand);
            return false;
        }
    }
}
