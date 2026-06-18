using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.State;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;
using GambaWhere.Utility;

namespace GambaWhere.IPC;

/// <summary>IPC v2: public provider gates that let any plugin announce its window opened and push live automatic rules, registering a per-partner channel that feeds the host rules draw and the auto-session prompt. Runs alongside the legacy PartnerIpcManager until partners migrate.</summary>
public sealed class PartnerIpcV2Manager : IDisposable
{
    public const string WindowOpenedGate = "GambaWhere.WindowOpened";
    public const string SubmitRulesGate = "GambaWhere.SubmitRules";

    private const int MaxPluginNameLength = 32;
    private const long WindowOpenedPromptDebounceMs = 2_000;
    private const uint LinkIdBase = 5000;

    private readonly object _gate = new();
    private readonly Dictionary<string, PartnerChannelV2> _channels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DalamudLinkPayload> _linkPayloads = new(StringComparer.Ordinal);

    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IFramework _framework;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private readonly ICallGateProvider<string, string, bool> _windowOpened;
    private readonly ICallGateProvider<string, string, object, bool> _submitRules;

    private uint _nextLinkId = LinkIdBase;

    public PartnerIpcV2Manager(
        IDalamudPluginInterface pluginInterface,
        MainWindow mainWindow,
        HostGambaTab hostTab,
        IChatGui chatGui,
        IFramework framework,
        Configuration config,
        IPluginLog log)
    {
        _mainWindow = mainWindow;
        _hostTab = hostTab;
        _chatGui = chatGui;
        _framework = framework;
        _config = config;
        _log = log;

        _windowOpened = pluginInterface.GetIpcProvider<string, string, bool>(WindowOpenedGate);
        _windowOpened.RegisterFunc(OnWindowOpened);

        _submitRules = pluginInterface.GetIpcProvider<string, string, object, bool>(SubmitRulesGate);
        _submitRules.RegisterFunc(OnSubmitRules);
    }

    public IReadOnlyList<HostRuleSource> GetRuleSources(string category)
    {
        lock (_gate)
        {
            var sources = new List<HostRuleSource>();
            foreach (var channel in _channels.Values)
            {
                if (!channel.IsLive || !string.Equals(channel.Category, category, StringComparison.Ordinal))
                    continue;

                var captured = channel;
                sources.Add(new HostRuleSource(captured.PluginName, () => GetRulesForChannel(captured)));
            }

            return sources;
        }
    }

    public Dictionary<string, object>? GetRules(string category)
    {
        lock (_gate)
        {
            foreach (var channel in _channels.Values)
            {
                if (!channel.IsLive || !string.Equals(channel.Category, category, StringComparison.Ordinal))
                    continue;

                var rules = channel.GetRules();
                if (rules != null)
                    return rules;
            }

            return null;
        }
    }

    private Dictionary<string, object>? GetRulesForChannel(PartnerChannelV2 channel)
    {
        lock (_gate)
            return channel.GetRules();
    }

    private bool OnWindowOpened(string pluginName, string category)
    {
        var name = CleanPluginName(pluginName);
        if (name == null || GameCategories.Find(category) == null)
        {
            _log.Debug($"[GambaWhere IPC v2] WindowOpened rejected (name '{pluginName}', category '{category}').");
            return false;
        }

        bool shouldPrompt;
        lock (_gate)
        {
            var channel = GetOrCreate(name, category);
            channel.Category = category;
            channel.Touch();
            shouldPrompt = channel.TryMarkPrompted(WindowOpenedPromptDebounceMs);
        }

        if (shouldPrompt)
            PromptAutoSession(name);

        return true;
    }

    private bool OnSubmitRules(string pluginName, string category, object payload)
    {
        var name = CleanPluginName(pluginName);
        if (name == null || GameCategories.Find(category) == null)
        {
            _log.Debug($"[GambaWhere IPC v2] SubmitRules rejected (name '{pluginName}', category '{category}').");
            return false;
        }

        var rules = AutomaticRulesValidator.Validate(payload);
        if (rules == null)
        {
            _log.Debug($"[GambaWhere IPC v2] SubmitRules from '{name}' rejected: invalid rules payload.");
            return false;
        }

        lock (_gate)
        {
            var channel = GetOrCreate(name, category);
            channel.Category = category;
            channel.SetRules(rules);
        }

        return true;
    }

    private PartnerChannelV2 GetOrCreate(string name, string category)
    {
        if (_channels.TryGetValue(name, out var channel))
            return channel;

        var linkId = _nextLinkId++;
        channel = new PartnerChannelV2(name, category, linkId);
        _channels[name] = channel;
        _linkPayloads[name] = _chatGui.AddChatLinkHandler(linkId, (_, _) => OnStartLinkClicked(name));
        return channel;
    }

    private void OnStartLinkClicked(string name)
    {
        string? category;
        lock (_gate)
            category = _channels.TryGetValue(name, out var channel) ? channel.Category : null;

        if (category == null)
            return;

        _hostTab.SelectGame(category);
        _mainWindow.OpenHostGambaTab();
    }

    private void PromptAutoSession(string name)
    {
        if (!_config.AutoSessionDetection)
            return;

        DalamudLinkPayload? payload;
        lock (_gate)
            _linkPayloads.TryGetValue(name, out payload);

        if (payload == null)
            return;

        _ = _framework.RunOnFrameworkThread(() => IpcAutoSessionPrompt.Print(_chatGui, payload, name));
    }

    private static string? CleanPluginName(string? pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            return null;

        var name = pluginName.Trim();
        if (name.Length > MaxPluginNameLength || UserTextGuard.ContainsDisallowedContent(name))
            return null;

        return name;
    }

    public void Dispose()
    {
        _windowOpened.UnregisterFunc();
        _submitRules.UnregisterFunc();

        lock (_gate)
        {
            foreach (var channel in _channels.Values)
                _chatGui.RemoveChatLinkHandler(channel.LinkId);

            _channels.Clear();
            _linkPayloads.Clear();
        }
    }
}
