using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GambaWhere.Alerting;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere.IPC;

/// <summary>Legacy IPC v1: auto-creates and owns one PartnerPluginIpc channel per catalog game that declares a Partner, exposing its automatic rules by category for the host draw and session heartbeat.</summary>
public sealed class PartnerIpcManager : IDisposable
{
    private readonly List<PartnerPluginIpc> _channels = new();
    private readonly Dictionary<string, PartnerPluginIpc> _byCategory = new();
    private readonly Configuration _config;

    public PartnerIpcManager(
        IDalamudPluginInterface pluginInterface,
        MainWindow mainWindow,
        HostGambaTab hostTab,
        IChatGui chatGui,
        Configuration config,
        IPluginLog log)
    {
        _config = config;

        var reserved = new HashSet<uint> { LifestreamService.LinkId, AlertingService.LinkId };

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

    public IReadOnlyList<HostRuleSource> GetRuleSources(string category)
    {
        if (!_byCategory.TryGetValue(category, out var ipc) || !ipc.HasRules)
            return Array.Empty<HostRuleSource>();

        var live = ipc.GetRules() != null;
        if (live)
            _config.RememberRuleSource(ipc.RuleSourceName, category);
        else if (!_config.IsKnownRuleSource(ipc.RuleSourceName, category))
            return Array.Empty<HostRuleSource>();

        return new[] { new HostRuleSource(ipc.RuleSourceName, () => ipc.GetRules(), live) };
    }

    public Dictionary<string, object>? GetRules(string category, string sourceName) =>
        _byCategory.TryGetValue(category, out var ipc)
        && string.Equals(ipc.RuleSourceName, sourceName, StringComparison.Ordinal)
            ? ipc.GetRules(true)
            : null;

    public void Dispose()
    {
        foreach (var channel in _channels)
            channel.Dispose();
    }
}
