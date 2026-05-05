using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere.IPC;

public sealed class SimpleRouletteIpc : IDisposable
{
    private const string IpcKey = "SimpleRoulette.WindowOpened";
    private const uint LinkId = 3;

    private readonly ICallGateSubscriber<Action> _subscriber;
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
