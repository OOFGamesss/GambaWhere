using System;
using System.Text.Json;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GambaWhere.IPC;

/// <summary>IPC bridge to the Mini Games Emporium plugin.</summary>
public sealed class MiniGamesEmporiumIpc : IDisposable
{
    private const string WindowOpenedIpcKey = "MiniGamesEmporium.WindowOpened";
    private const string Bar777IpcKey = "MiniGamesEmporium.Bar777.GetInfo";
    private const string DeathrollTournamentIpcKey = "MiniGamesEmporium.DeathrollTournament.GetInfo";
    private const uint LinkId = 9;

    private readonly ICallGateSubscriber<object> _windowOpenedSubscriber;
    private readonly ICallGateSubscriber<object> _bar777Subscriber;
    private readonly ICallGateSubscriber<object> _deathrollSubscriber;

    private BAR777Data? _cachedBar777Info;
    private long _lastBar777CheckTick;

    private DeathrollTournamentData? _cachedDeathrollInfo;
    private long _lastDeathrollCheckTick;

    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Configuration _config;

    private readonly DalamudLinkPayload _linkPayload;

    public MiniGamesEmporiumIpc(
        IDalamudPluginInterface pluginInterface,
        MainWindow mainWindow,
        HostGambaTab hostTab,
        IChatGui chatGui,
        Configuration config,
        IPluginLog log)
    {
        _mainWindow = mainWindow;
        _hostTab = hostTab;
        _chatGui = chatGui;
        _config = config;
        _log = log;

        _linkPayload = _chatGui.AddChatLinkHandler(LinkId, OnStartLinkClicked);

        _windowOpenedSubscriber = pluginInterface.GetIpcSubscriber<object>(WindowOpenedIpcKey);
        _windowOpenedSubscriber.Subscribe(OnWindowOpened);

        _bar777Subscriber = pluginInterface.GetIpcSubscriber<object>(Bar777IpcKey);
        _bar777Subscriber.Subscribe(OnBar777SessionUpdated);

        _deathrollSubscriber = pluginInterface.GetIpcSubscriber<object>(DeathrollTournamentIpcKey);
        _deathrollSubscriber.Subscribe(OnDeathrollSessionUpdated);
    }

    private const long ValidCacheMs = 30_000;
    private const long EmptyCacheMs = 1_000;

    public object? GetGameInfo(bool forceRefresh = false)
    {
        var bar777 = GetBar777Info(forceRefresh);
        if (bar777 != null) return bar777;
        return GetDeathrollInfo(forceRefresh);
    }

    public void Dispose()
    {
        _windowOpenedSubscriber.Unsubscribe(OnWindowOpened);
        _bar777Subscriber.Unsubscribe(OnBar777SessionUpdated);
        _deathrollSubscriber.Unsubscribe(OnDeathrollSessionUpdated);
        _chatGui.RemoveChatLinkHandler(LinkId);
    }

    private BAR777Data? GetBar777Info(bool forceRefresh = false)
    {
        var currentTick = Environment.TickCount64;
        var ttl = _cachedBar777Info == null ? EmptyCacheMs : ValidCacheMs;
        if (!forceRefresh && currentTick - _lastBar777CheckTick < ttl)
            return _cachedBar777Info;

        _lastBar777CheckTick = currentTick;

        try
        {
            var rawData = _bar777Subscriber.InvokeFunc();
            if (rawData == null)
            {
                _log.Verbose("[GambaWhere/MiniGames] Bar777 GetInfo IPC returned null.");
                _cachedBar777Info = null;
                return null;
            }

            _log.Verbose($"[GambaWhere/MiniGames] Bar777 GetInfo IPC returned: {rawData}");
            _cachedBar777Info = DeserializeBar777Info(rawData);
            return _cachedBar777Info;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not registered"))
                _log.Verbose($"[GambaWhere/MiniGames] MiniGamesEmporium not loaded: {ex.Message}");
            else
                _log.Warning($"[GambaWhere/MiniGames] Bar777 GetInfo failed: {ex.Message}");
            _cachedBar777Info = null;
            return null;
        }
    }

    private DeathrollTournamentData? GetDeathrollInfo(bool forceRefresh = false)
    {
        var currentTick = Environment.TickCount64;
        var ttl = _cachedDeathrollInfo == null ? EmptyCacheMs : ValidCacheMs;
        if (!forceRefresh && currentTick - _lastDeathrollCheckTick < ttl)
            return _cachedDeathrollInfo;

        _lastDeathrollCheckTick = currentTick;

        try
        {
            var rawData = _deathrollSubscriber.InvokeFunc();
            if (rawData == null)
            {
                _log.Verbose("[GambaWhere/MiniGames] DeathrollTournament GetInfo IPC returned null.");
                _cachedDeathrollInfo = null;
                return null;
            }

            _log.Verbose($"[GambaWhere/MiniGames] DeathrollTournament GetInfo IPC returned: {rawData}");
            _cachedDeathrollInfo = DeserializeDeathrollInfo(rawData);
            return _cachedDeathrollInfo;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not registered"))
                _log.Verbose($"[GambaWhere/MiniGames] MiniGamesEmporium not loaded: {ex.Message}");
            else
                _log.Warning($"[GambaWhere/MiniGames] DeathrollTournament GetInfo failed: {ex.Message}");
            _cachedDeathrollInfo = null;
            return null;
        }
    }

    private void OnWindowOpened()
    {
        if (!_config.AutoSessionDetection)
            return;

        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, "Mini Games Emporium");
    }

    private void OnBar777SessionUpdated()
    {
        _log.Verbose("[GambaWhere/MiniGames] Bar777 session updated; invalidating cache.");
        _lastBar777CheckTick = 0;
    }

    private void OnDeathrollSessionUpdated()
    {
        _log.Verbose("[GambaWhere/MiniGames] DeathrollTournament session updated; invalidating cache.");
        _lastDeathrollCheckTick = 0;
    }

    private BAR777Data? DeserializeBar777Info(object raw)
    {
        switch (raw)
        {
            case BAR777Data d:
                return d;
            case string s when !string.IsNullOrWhiteSpace(s):
                return JsonConvert.DeserializeObject<BAR777Data>(s);
            case JObject jo:
                return jo.ToObject<BAR777Data>();
            case JToken token:
                return token.ToObject<BAR777Data>();
            case JsonElement je:
                return JsonConvert.DeserializeObject<BAR777Data>(je.GetRawText());
            default:
                var json = JsonConvert.SerializeObject(raw);
                if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "null")
                    return null;
                return JsonConvert.DeserializeObject<BAR777Data>(json);
        }
    }

    private DeathrollTournamentData? DeserializeDeathrollInfo(object raw)
    {
        switch (raw)
        {
            case DeathrollTournamentData d:
                return d;
            case string s when !string.IsNullOrWhiteSpace(s):
                return JsonConvert.DeserializeObject<DeathrollTournamentData>(s);
            case JObject jo:
                return jo.ToObject<DeathrollTournamentData>();
            case JToken token:
                return token.ToObject<DeathrollTournamentData>();
            case JsonElement je:
                return JsonConvert.DeserializeObject<DeathrollTournamentData>(je.GetRawText());
            default:
                var json = JsonConvert.SerializeObject(raw);
                if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "null")
                    return null;
                return JsonConvert.DeserializeObject<DeathrollTournamentData>(json);
        }
    }

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectMiniGames();
        _mainWindow.OpenHostGambaTab();
    }
}
