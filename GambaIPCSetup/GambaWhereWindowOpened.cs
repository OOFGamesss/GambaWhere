using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

// GambaWhere IPC v2 - window opened template.
//
// Drop this file into your plugin, change the namespace, fill in the TODOs, construct it once during
// plugin start up and dispose it on unload. It tells GambaWhere, in real time, that your plugin window
// has opened, so GambaWhere can list you and offer the host a one click "start a session" prompt.
//
// See README.md for the full contract.

namespace YourPlugin.IPC;

public sealed class GambaWhereWindowOpened : IDisposable
{
    // TODO: the name shown to the host. If your game already ships in GambaWhere's built in catalogue,
    //       use the exact same companion plugin name so GambaWhere replaces the old entry rather than
    //       listing you twice. 1 to 32 characters, no URLs or HTML.
    private const string PluginName = "Your Plugin Name";

    // TODO: one of GambaWhere's categories (see README.md). Must match exactly, case sensitive.
    private const string Category = "Mini Games";

    private const string Gate = "GambaWhere.WindowOpened";

    private readonly ICallGateSubscriber<string, string, bool> _windowOpened;
    private readonly IPluginLog _log;

    public GambaWhereWindowOpened(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        _windowOpened = pluginInterface.GetIpcSubscriber<string, string, bool>(Gate);

        // TODO: hook whatever fires when your window opens, then call NotifyWindowOpened().
        //       For a Dalamud Window you can override OnOpen() to raise an event you subscribe to here,
        //       or just call NotifyWindowOpened() directly from OnOpen().
    }

    public void Dispose()
    {
        // TODO: unsubscribe from your window opened event here, mirroring the constructor.
    }

    // Call this the instant your window opens. It is safe to call repeatedly; GambaWhere coalesces
    // repeat opens within two seconds, so you do not need to throttle it.
    public void NotifyWindowOpened()
    {
        try
        {
            // Returns false if GambaWhere rejected the call (unknown category or invalid plugin name).
            _windowOpened.InvokeFunc(PluginName, Category);
        }
        catch (Exception ex)
        {
            // GambaWhere is not installed or is an older version. Safe to ignore and retry next time.
            _log.Debug($"GambaWhere WindowOpened IPC unavailable: {ex.Message}");
        }
    }
}
