using System;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Discord;
using GambaWhere.IPC;
using GambaWhere.Images;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.Alerting;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere;

public sealed class GambaWhere : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IToastGui Toasts { get; private set; } = null!;

    private const string MainCommand = "/gambawhere";
    private const string AliasCommand = "/gw";
    private const string ConfigCommand = "/gambawhereconfig";

    internal Configuration Configuration { get; }

    private readonly WindowSystem _windowSystem = new("GambaWhere");
    private readonly MainWindow _mainWindow;
    private readonly SessionPillOverlay _pillOverlay;
    private readonly GambaEventsTab _eventsTab;
    private readonly SessionState _sessionState;

    private readonly GambaWhereClient _client;
    private readonly SessionService _sessionService;
    private readonly DiscordWebhookService _discordWebhook;
    private readonly ImageCache _imageCache;
    private readonly ChocoboRacingGambaIpc _chocoboIpc;
    private readonly SimpleBingoIpc _bingoIpc;
    private readonly SimpleRouletteIpc _rouletteIpc;
    private readonly SimpleBlackjackIpc _blackjackIpc;
    private readonly SimpleWheelIpc _wheelIpc;
    private readonly SimplePokerIpc _pokerIpc;
    private readonly SimpleScratchIpc _scratchIpc;
    private readonly MiniGamesEmporiumIpc _miniGamesIpc;
    private readonly AlertingService _alertingService;

    public GambaWhere()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.EnsureDefaultPresets();

        _client = new GambaWhereClient(Log);
        _imageCache = new ImageCache(PluginInterface, TextureProvider);

        var playerInfo = new PlayerInfoService(ClientState, ObjectTable, DataManager, Log);
        _sessionState = new SessionState();
        var hostFormState = new HostFormState();

        var pluginDirectory =
            PluginInterface.AssemblyLocation.DirectoryName ?? PluginInterface.AssemblyLocation.FullName;

        _discordWebhook = new DiscordWebhookService(Log, Configuration, _sessionState, pluginDirectory);

        _sessionService = new SessionService(_client, playerInfo, _sessionState, Configuration, ClientState, Framework,
            Log,
            _discordWebhook);

        var eventTeleport = new EventLocationTeleportService(PluginInterface, DataManager, ObjectTable, ChatGui, Log);
        _eventsTab = new GambaEventsTab(_client, _imageCache, eventTeleport);
        var hostTab = new HostGambaTab(_sessionService, playerInfo, _client, _sessionState, Configuration, hostFormState);
        var gameListTab = new GameListTab(_imageCache);

        _pillOverlay = new SessionPillOverlay(_sessionState, Configuration, _sessionService);

        var settingsTab = new SettingsTab(Configuration, _imageCache, Log, _pillOverlay);
        var supportTab = new SupportTab(_imageCache);
        var discordTab = new DiscordWebhookTab(Configuration, _discordWebhook, _imageCache, Log);
        var alertsTab = new AlertsTab(Configuration, _client);

        _mainWindow =
            new MainWindow(_eventsTab, hostTab, gameListTab, settingsTab, supportTab, discordTab, alertsTab);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_pillOverlay);

        _alertingService = new AlertingService(
            Configuration,
            ChatGui,
            Toasts,
            Framework,
            Log,
            _mainWindow.OpenEventsTabExpanded);
        _eventsTab.OnEventsRefreshed = _alertingService.OnEventsRefreshed;

        _chocoboIpc = new ChocoboRacingGambaIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _bingoIpc = new SimpleBingoIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _rouletteIpc = new SimpleRouletteIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _blackjackIpc = new SimpleBlackjackIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _wheelIpc = new SimpleWheelIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _pokerIpc = new SimplePokerIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _scratchIpc = new SimpleScratchIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _miniGamesIpc = new MiniGamesEmporiumIpc(PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);

        _sessionService.RefreshAutomaticRulesFromIpc =
            new AutomaticRulesIpcRefresher(_bingoIpc, _rouletteIpc, _chocoboIpc, _miniGamesIpc).TryRefresh;

        hostTab.GetHostAutomaticRuleContext = () => hostTab.GetSelectedGameType() switch
        {
            "Bingo" => _bingoIpc.GetGameInfo(),
            "Roulette" => _rouletteIpc.GetGameInfo(),
            "Chocobo Racing" => _chocoboIpc.GetGameInfo(),
            "Mini Games" => _miniGamesIpc.GetGameInfo(),
            _ => null
        };

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the GambaWhere window."
        });
        CommandManager.AddHandler(AliasCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /gambawhere."
        });
        CommandManager.AddHandler(ConfigCommand, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the GambaWhere settings tab."
        });

        PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("GambaWhere loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        _alertingService.Dispose();
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _pillOverlay.Dispose();

        _chocoboIpc.Dispose();
        _bingoIpc.Dispose();
        _rouletteIpc.Dispose();
        _blackjackIpc.Dispose();
        _wheelIpc.Dispose();
        _pokerIpc.Dispose();
        _scratchIpc.Dispose();
        _miniGamesIpc.Dispose();

        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(AliasCommand);
        CommandManager.RemoveHandler(ConfigCommand);

        _sessionService.Dispose();
        _discordWebhook.Dispose();
        _client.Dispose();
        _imageCache.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _eventsTab.Tick();
        _pillOverlay.IsOpen = (_sessionState.IsActive || _pillOverlay.IsMoving) && Configuration.PillOverlayEnabled;
    }

    private void OnCommand(string command, string args) => _mainWindow.Toggle();

    private void OnConfigCommand(string command, string args) => OpenConfigUi();

    private void ToggleMainUi() => _mainWindow.Toggle();

    private void OpenConfigUi() => _mainWindow.OpenSettingsTab();
}
