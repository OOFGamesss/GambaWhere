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

public sealed class MiniGamesEmporiumIpc : IDisposable
{
    private const string WindowOpenedIpcKey = "MiniGamesEmporium.WindowOpened";
    private const string GameInfoIpcKey = "MiniGamesEmporium.Bar777.GetInfo";
    private const uint LinkId = 9;

    private readonly ICallGateSubscriber<object> _windowOpenedSubscriber;
    private readonly ICallGateSubscriber<object> _gameInfoSubscriber;

    private MiniGamesEmporiumData? _cachedGameInfo;
    private long _lastCheckTick;

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

        _gameInfoSubscriber = pluginInterface.GetIpcSubscriber<object>(GameInfoIpcKey);
        _gameInfoSubscriber.Subscribe(OnSessionUpdated);
    }

    private const long ValidCacheMs = 30_000;
    private const long EmptyCacheMs = 1_000;

    public MiniGamesEmporiumData? GetGameInfo(bool forceRefresh = false)
    {
        var currentTick = Environment.TickCount64;
        var ttl = _cachedGameInfo == null ? EmptyCacheMs : ValidCacheMs;
        if (!forceRefresh && currentTick - _lastCheckTick < ttl)
            return _cachedGameInfo;

        _lastCheckTick = currentTick;

        try
        {
            var rawData = _gameInfoSubscriber.InvokeFunc();
            if (rawData == null)
            {
                _log.Verbose("[GambaWhere/MiniGames] GetBar777Info IPC returned null.");
                _cachedGameInfo = null;
                return null;
            }

            _log.Verbose($"[GambaWhere/MiniGames] GetBar777Info IPC returned: {rawData}");
            _cachedGameInfo = DeserializeGameInfo(rawData);
            return _cachedGameInfo;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not registered"))
                _log.Verbose($"[GambaWhere/MiniGames] MiniGamesEmporium not loaded: {ex.Message}");
            else
                _log.Warning($"[GambaWhere/MiniGames] GetBar777Info failed: {ex.Message}");
            _cachedGameInfo = null;
            return null;
        }
    }

    public void Dispose()
    {
        _windowOpenedSubscriber.Unsubscribe(OnWindowOpened);
        _gameInfoSubscriber.Unsubscribe(OnSessionUpdated);
        _chatGui.RemoveChatLinkHandler(LinkId);
    }

    private void OnWindowOpened()
    {
        if (!_config.AutoSessionDetection)
            return;

        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, "MiniGamesEmporium");
    }

    private void OnSessionUpdated()
    {
        _log.Verbose("[GambaWhere/MiniGames] Session updated; invalidating GetBar777Info cache.");
        _lastCheckTick = 0;
    }

    private MiniGamesEmporiumData? DeserializeGameInfo(object raw)
    {
        switch (raw)
        {
            case MiniGamesEmporiumData d:
                return d;
            case string s when !string.IsNullOrWhiteSpace(s):
                return JsonConvert.DeserializeObject<MiniGamesEmporiumData>(s);
            case JObject jo:
                return jo.ToObject<MiniGamesEmporiumData>();
            case JToken token:
                return token.ToObject<MiniGamesEmporiumData>();
            case JsonElement je:
                return JsonConvert.DeserializeObject<MiniGamesEmporiumData>(je.GetRawText());
            default:
                var json = JsonConvert.SerializeObject(raw);
                if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "null")
                    return null;
                return JsonConvert.DeserializeObject<MiniGamesEmporiumData>(json);
        }
    }

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectMiniGames();
        _mainWindow.OpenHostGambaTab();
    }
}
