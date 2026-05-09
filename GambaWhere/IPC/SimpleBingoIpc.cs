using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;
using SimpleBingo.Data;

namespace GambaWhere.IPC;

public sealed class SimpleBingoIpc : IDisposable
{
    private const string IpcKey = "SimpleBingo.WindowOpened";
    private const string GameInfoIpcKey = "SimpleBingo.GetGameInfo";
    private const string GameJoinedIpcKey = "SimpleBingo.GameJoined";
    private const uint LinkId = 2;

    private readonly ICallGateSubscriber<Action> _subscriber;
    private readonly ICallGateSubscriber<Action> _gameJoinedSubscriber;
    private readonly ICallGateSubscriber<object> _gameInfoSubscriber;

    private GameInfoIPC? _cachedGameInfo;
    private long _lastCheckTick = 0;

    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Configuration _config;

    private readonly DalamudLinkPayload _linkPayload;

    public SimpleBingoIpc(
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

        _gameJoinedSubscriber = pluginInterface.GetIpcSubscriber<Action>(GameJoinedIpcKey);
        _gameJoinedSubscriber.Subscribe(OnGameJoined);

        _gameInfoSubscriber = pluginInterface.GetIpcSubscriber<object>(GameInfoIpcKey);
    }

    private void OnGameJoined()
    {
        _log.Information("[GambaWhere/Bingo] OnGameJoined fired; invalidating GetGameInfo cache.");
        _lastCheckTick = 0;
    }

    private const long ValidCacheMs = 30_000;
    private const long EmptyCacheMs = 1_000;

    public GameInfoIPC? GetGameInfo(bool forceRefresh = false)
    {
        var currentTick = Environment.TickCount64;
        var ttl = _cachedGameInfo == null ? EmptyCacheMs : ValidCacheMs;
        if (!forceRefresh && currentTick - _lastCheckTick < ttl)
        {
            return _cachedGameInfo;
        }

        _lastCheckTick = currentTick;

        try
        {
            var rawData = _gameInfoSubscriber.InvokeFunc();
            if (rawData == null)
            {
                _log.Information("[GambaWhere/Bingo] GetGameInfo IPC returned null.");
                _cachedGameInfo = null;
                return null;
            }

            var jsonStr = rawData.ToString();
            if (string.IsNullOrEmpty(jsonStr))
            {
                _log.Information("[GambaWhere/Bingo] GetGameInfo IPC returned empty string.");
                _cachedGameInfo = null;
                return null;
            }

            _log.Information($"[GambaWhere/Bingo] GetGameInfo IPC returned: {jsonStr}");

            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<GameInfoIPC>(jsonStr);

            _cachedGameInfo = data;
            return data;
        }
        catch (Exception ex)
        {
            _log.Warning($"SimpleBingo IPC GetGameInfo failed to parse data: {ex.Message}");
            _cachedGameInfo = null;
            return null;
        }
    }

    public void Dispose()
    {
        _subscriber.Unsubscribe(OnWindowOpened);
        _gameJoinedSubscriber.Unsubscribe(OnGameJoined);
        _chatGui.RemoveChatLinkHandler(LinkId);
    }

    private void OnWindowOpened()
    {
        if (!_config.AutoSessionDetection)
            return;

        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, "SimpleBingo");
    }

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectBingo();
        _mainWindow.OpenHostGambaTab();
    }
}
