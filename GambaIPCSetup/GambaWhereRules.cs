using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

// GambaWhere IPC v2 - rules template.
//
// Drop this file into your plugin, change the namespace, fill in the TODOs, construct it once during
// plugin start up and dispose it on unload. It pushes your live game settings to GambaWhere every 30
// seconds so a host can pick them up automatically. The payload classes at the bottom are part of the
// contract; keep their property names exactly as written.
//
// See README.md for the full contract.

namespace YourPlugin.IPC;

public sealed class GambaWhereRules : IDisposable
{
    // TODO: must match the PluginName used in your window opened class.
    private const string PluginName = "Your Plugin Name";

    // TODO: one of GambaWhere's categories (see README.md). Must match exactly, case sensitive.
    private const string Category = "Mini Games";

    private const string Gate = "GambaWhere.SubmitRules";

    // GambaWhere keeps your rules for 45 seconds after the last push, so a 30 second cadence stays live
    // with headroom for the occasional late push.
    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(30);

    private readonly ICallGateSubscriber<string, string, object, bool> _submitRules;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;

    private DateTime _nextPushUtc;

    public GambaWhereRules(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog log)
    {
        _framework = framework;
        _log = log;
        _submitRules = pluginInterface.GetIpcSubscriber<string, string, object, bool>(Gate);

        _framework.Update += OnFrameworkUpdate;
        _nextPushUtc = DateTime.UtcNow;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (DateTime.UtcNow < _nextPushUtc) return;

        _nextPushUtc = DateTime.UtcNow + PushInterval;
        PushRules();
    }

    private void PushRules()
    {
        var payload = BuildPayload();

        // A null payload means "no live session", so we do not push and GambaWhere shows "No Session".
        if (payload == null) return;

        try
        {
            // Returns false if GambaWhere rejected the payload (unknown category, no valid rules,
            // more than ten rules, oversized or disallowed text, and so on).
            _submitRules.InvokeFunc(PluginName, Category, payload);
        }
        catch (Exception ex)
        {
            // GambaWhere is not installed or is an older version. Safe to ignore and retry next time.
            _log.Debug($"GambaWhere SubmitRules IPC unavailable: {ex.Message}");
        }
    }

    private GambaWhereRulesPayload? BuildPayload()
    {
        // TODO: return null when there is no active session so GambaWhere reverts to "No Session".
        // if (!HasActiveSession) return null;

        var payload = new GambaWhereRulesPayload();

        // TODO: add up to ten rules describing your current game. Each Value must be a string, bool,
        //       int, long or double. Labels are shown as is, so use readable text.
        //       Tip: if a label contains the word "odds" and the value is a double, GambaWhere appends
        //       an "x" (for example 5.0 shows as "5.00x"). Money and counts read best as long or int.
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Buy In", Value = 100000L });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Players", Value = 8 });
        payload.Rules.Add(new GambaWhereRuleEntry { Label = "Allow Rebuys", Value = true });

        return payload;
    }
}

// Keep these property names (Rules, Label, Value) exactly;
// GambaWhere reads the object you pass by reflection.
public sealed class GambaWhereRulesPayload
{
    public List<GambaWhereRuleEntry> Rules { get; set; } = new();
}

public sealed class GambaWhereRuleEntry
{
    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }
}
