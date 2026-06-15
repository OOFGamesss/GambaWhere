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

/// <summary>IPC bridge to the Chocobo Racing Gamba plugin.</summary>
public sealed class ChocoboRacingGambaIpc : IDisposable
{
    private const string IpcKey = "ChocoboRacingGamba.WindowOpened";
    private const string GameInfoIpcKey = "ChocoboRacingGamba.GetGameInfo";
    private const uint LinkId = 1;

    private readonly ICallGateSubscriber<Action> _subscriber;
    private readonly ICallGateSubscriber<object> _gameInfoSubscriber;

    private ChocoboRacingGambaData? _cachedGameInfo;
    private long _lastCheckTick;

    private readonly MainWindow _mainWindow;
    private readonly HostGambaTab _hostTab;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly Configuration _config;

    private readonly DalamudLinkPayload _linkPayload;

    public ChocoboRacingGambaIpc(
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

    public ChocoboRacingGambaData? GetGameInfo(bool forceRefresh = false)
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
            _log.Warning($"ChocoboRacingGamba IPC GetGameInfo failed: {ex.Message}");
            _cachedGameInfo = null;
            return null;
        }
    }

    private static ChocoboRacingGambaData? DeserializeGameInfo(object raw)
    {
        switch (raw)
        {
            case ChocoboRacingGambaData r:
                return r;
            case string s when !string.IsNullOrWhiteSpace(s):
                return JsonConvert.DeserializeObject<ChocoboRacingGambaData>(s);
            case JObject jo:
                return jo.ToObject<ChocoboRacingGambaData>();
            case JToken token:
                return token.ToObject<ChocoboRacingGambaData>();
            case JsonElement je:
                return JsonConvert.DeserializeObject<ChocoboRacingGambaData>(je.GetRawText());
            default:
                var json = JsonConvert.SerializeObject(raw);
                if (string.IsNullOrWhiteSpace(json) || json == "{}")
                    return null;
                return JsonConvert.DeserializeObject<ChocoboRacingGambaData>(json);
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

        IpcAutoSessionPrompt.Print(_chatGui, _linkPayload, "Chocobo Racing");
    }

    private void OnStartLinkClicked(uint id, SeString message)
    {
        _hostTab.SelectChocoboRacing();
        _mainWindow.OpenHostGambaTab();
    }
}
