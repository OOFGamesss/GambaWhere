using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;
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

    public GameInfoIPC? GetGameInfo()
    {
        var currentTick = Environment.TickCount64;
        if (currentTick - _lastCheckTick < 60000)
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

            var jsonStr = rawData.ToString();
            if (string.IsNullOrEmpty(jsonStr))
            {
                _cachedGameInfo = null;
                return null;
            }

            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<GameInfoIPC>(jsonStr);
            _cachedGameInfo = data;
            return data;
        }
        catch (Exception ex)
        {
            _log.Warning($"SimpleRoulette IPC GetGameInfo failed to parse data: {ex.Message}");
            _cachedGameInfo = null;
            return null;
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
