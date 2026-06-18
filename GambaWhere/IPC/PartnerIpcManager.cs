using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GambaWhere.Alerting;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.State;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere.IPC;

/// <summary>Legacy IPC v1: auto-creates and owns one PartnerPluginIpc channel per catalog game that declares a Partner, exposing its automatic rules by category for the host draw and session heartbeat.</summary>
public sealed class PartnerIpcManager : IDisposable
{
    private readonly List<PartnerPluginIpc> _channels = new();
    private readonly Dictionary<string, PartnerPluginIpc> _byCategory = new();

    public PartnerIpcManager(
        IDalamudPluginInterface pluginInterface,
        MainWindow mainWindow,
        HostGambaTab hostTab,
        IChatGui chatGui,
        Configuration config,
        IPluginLog log)
    {
        var reserved = new HashSet<uint> { LifestreamHouseIpc.LinkId, AlertingService.LinkId };

        uint linkId = 1;
        foreach (var game in GameCatalog.IpcGames)
        {
            while (reserved.Contains(linkId))
                linkId++;

            var channel = new PartnerPluginIpc(game, linkId++, pluginInterface, mainWindow, hostTab, chatGui, config, log);
            _channels.Add(channel);
            _byCategory[game.Category] = channel;
        }
    }

    public IReadOnlyList<HostRuleSource> GetRuleSources(string category) =>
        _byCategory.TryGetValue(category, out var ipc) && ipc.HasRules
            ? new[] { new HostRuleSource(ipc.RuleSourceName, () => ipc.GetRules()) }
            : Array.Empty<HostRuleSource>();

    public Dictionary<string, object>? GetRules(string category) =>
        _byCategory.TryGetValue(category, out var ipc) ? ipc.GetRules(true) : null;

    public void Dispose()
    {
        foreach (var channel in _channels)
            channel.Dispose();
    }
}
