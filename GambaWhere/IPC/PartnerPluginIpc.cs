using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere.IPC;

/// <summary>Generic IPC bridge driven by a Game: the auto-session prompt plus declarative automatic rules.</summary>
public sealed class PartnerPluginIpc : IDisposable
{
    private const long ValidCacheMs = 30_000;
    private const long EmptyCacheMs = 1_000;
    private const long WindowOpenedDebounceMs = 2_000;

    private readonly Game _game;
    private readonly uint _linkId;
    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Configuration _config;

    private readonly DalamudLinkPayload _linkPayload;
    private readonly ICallGateSubscriber<object> _windowOpened;
    private readonly List<ICallGateSubscriber<object>> _invalidationSubscribers = new();
    private readonly List<ICallGateSubscriber<object>> _shapeSubscribers = new();

    private Dictionary<string, object>? _cachedRules;
    private long _lastCheckTick;
    private long _lastWindowOpenedTick;

    public PartnerPluginIpc(
        Game game,
        uint linkId,
        IDalamudPluginInterface pluginInterface,
        MainWindow mainWindow,
        HostGambaTab hostTab,
        IChatGui chatGui,
        Configuration config,
        IPluginLog log)
    {
        _game = game;
        _linkId = linkId;
        _mainWindow = mainWindow;
        _hostTab = hostTab;
        _chatGui = chatGui;
        _config = config;
        _log = log;

        _linkPayload = _chatGui.AddChatLinkHandler(_linkId, OnStartLinkClicked);

        _windowOpened = pluginInterface.GetIpcSubscriber<object>(_game.WindowOpenedKey);
        _windowOpened.Subscribe(OnWindowOpened);

        foreach (var shape in _game.RuleShapes)
            _shapeSubscribers.Add(pluginInterface.GetIpcSubscriber<object>(shape.GetInfoKey));

        foreach (var key in _game.InvalidationKeys)
        {
            var sub = pluginInterface.GetIpcSubscriber<object>(key);
            sub.Subscribe(OnRulesUpdated);
            _invalidationSubscribers.Add(sub);
        }
    }

    public string RuleSourceName => _game.CompanionPlugin;

    public bool HasRules => _game.HasAutomaticRules;

    public Dictionary<string, object>? GetRules(bool forceRefresh = false)
    {
        if (!_game.HasAutomaticRules)
            return null;

        var currentTick = Environment.TickCount64;
        var ttl = _cachedRules == null ? EmptyCacheMs : ValidCacheMs;
        if (!forceRefresh && currentTick - _lastCheckTick < ttl)
            return _cachedRules;

        _lastCheckTick = currentTick;
        _cachedRules = FetchRules();
        return _cachedRules;
    }

    private Dictionary<string, object>? FetchRules()
    {
        var shapes = _game.RuleShapes;
        for (var i = 0; i < _shapeSubscribers.Count; i++)
        {
            object? raw;
            try
            {
                raw = _shapeSubscribers[i].InvokeFunc();
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("not registered"))
                    _log.Warning($"[GambaWhere/{_game.CompanionPlugin}] GetInfo IPC failed: {ex.Message}");
                continue;
            }

            var mapped = AutomaticRuleReader.Map(AutomaticRuleReader.ToJObject(raw), shapes[i].Fields);
            if (mapped != null)
                return mapped;
        }

        return null;
    }

    private void OnWindowOpened()
    {
        if (!_config.AutoSessionDetection)
            return;

        var currentTick = Environment.TickCount64;
        if (currentTick - _lastWindowOpenedTick < WindowOpenedDebounceMs)
            return;

        _lastWindowOpenedTick = currentTick;
        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, _game.CompanionPlugin);
    }

    private void OnRulesUpdated() => _lastCheckTick = 0;

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectGame(_game.Category);
        _mainWindow.OpenHostGambaTab();
    }

    public void Dispose()
    {
        _windowOpened.Unsubscribe(OnWindowOpened);
        foreach (var sub in _invalidationSubscribers)
            sub.Unsubscribe(OnRulesUpdated);
        _chatGui.RemoveChatLinkHandler(_linkId);
    }
}
