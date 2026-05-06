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
using SimpleRoulette.Data;

namespace GambaWhere.IPC;

public sealed class SimpleRouletteIpc : IDisposable
{
    private const string IpcKey = "SimpleRoulette.WindowOpened";
    private const string GameInfoIpcKey = "SimpleRoulette.GetGameInfo";
    private const uint LinkId = 3;

    private readonly ICallGateSubscriber<Action> _subscriber;
    private readonly ICallGateSubscriber<object> _gameInfoSubscriber;

    private GameInfoIPC? _cachedGameInfo;
    private long _lastCheckTick;

    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Configuration _config;

    private readonly DalamudLinkPayload _linkPayload;

    public SimpleRouletteIpc(
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

        _subscriber = pluginInterface.GetIpcSubscriber<Action>(IpcKey);
        _subscriber.Subscribe(OnWindowOpened);

        _gameInfoSubscriber = pluginInterface.GetIpcSubscriber<object>(GameInfoIpcKey);
    }

    public GameInfoIPC? GetGameInfo(bool forceRefresh = false)
    {
        var currentTick = Environment.TickCount64;
        if (!forceRefresh && currentTick - _lastCheckTick < 30000)
        {
            return _cachedGameInfo;
        }

        _lastCheckTick = currentTick;

        try
        {
            var rawData = _gameInfoSubscriber.InvokeFunc();
            if (rawData == null)
            {
                _cachedGameInfo = null;
                return null;
            }

            var data = DeserializeGameInfo(rawData);
            _cachedGameInfo = data;
            return data;
        }
        catch (Exception ex)
        {
            _log.Warning($"SimpleRoulette IPC GetGameInfo failed: {ex.Message}");
            _cachedGameInfo = null;
            return null;
        }
    }

    private GameInfoIPC? DeserializeGameInfo(object raw)
    {
        switch (raw)
        {
            case GameInfoIPC g:
                return g;
            case string s when !string.IsNullOrWhiteSpace(s):
                return JsonConvert.DeserializeObject<GameInfoIPC>(s);
            case JObject jo:
                return jo.ToObject<GameInfoIPC>();
            case JToken token:
                return token.ToObject<GameInfoIPC>();
            case JsonElement je:
                return JsonConvert.DeserializeObject<GameInfoIPC>(je.GetRawText());
            default:
                var json = JsonConvert.SerializeObject(raw);
                if (string.IsNullOrWhiteSpace(json) || json == "{}")
                    return null;
                return JsonConvert.DeserializeObject<GameInfoIPC>(json);
        }
    }

    public void Dispose()
    {
        _subscriber.Unsubscribe(OnWindowOpened);
        _chatGui.RemoveChatLinkHandler(LinkId);
    }

    private void OnWindowOpened()
    {
        if (!_config.AutoSessionDetection)
            return;

        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, "SimpleRoulette");
    }

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectRoulette();
        _mainWindow.OpenHostGambaTab();
    }
}
