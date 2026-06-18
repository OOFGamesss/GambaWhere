using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Partyfinder;
using GambaWhere.Discord;
using GambaWhere.IPC;
using GambaWhere.Images;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.Alerting;
using GambaWhere.UI;
using GambaWhere.UI.Tabs;

namespace GambaWhere;

/// <summary>Plugin entry point; wires up all services, IPC handlers, UI windows, and framework hooks.</summary>
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
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IPartyFinderGui PartyFinderGui { get; private set; } = null!;

    private const string MainCommand = "/gambawhere";
    private const string AliasCommand = "/gw";
    private const string ConfigCommand = "/gambawhereconfig";

    internal Configuration Configuration { get; }

    private readonly WindowSystem _windowSystem = new("GambaWhere");
    private readonly MainWindow _mainWindow;
    private readonly SessionPillOverlay _pillOverlay;
    private readonly GambaEventsTab _eventsTab;
    private readonly RecruitmentTab _recruitmentTab;
    private readonly SessionState _sessionState;

    private readonly GambaWhereClient _client;
    private readonly SessionService _sessionService;
    private readonly DiscordWebhookService _discordWebhook;
    private readonly ImageCache _imageCache;
    private readonly PartnerIpcManager _partnerIpcManager;
    private readonly PartnerIpcV2Manager _partnerIpcV2Manager;
    private readonly AlertingService _alertingService;
    private readonly EventAlertFeed _alertFeed;
    private readonly HostMarkerService _hostMarkerService;
    private readonly MinimapHostOverlay _minimapHostOverlay;
    private readonly PartyFinderCreator _partyFinderCreator;
    private readonly PartyFinderLocator _partyFinderLocator;

    public GambaWhere()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.EnsureDefaultPresets();

        _client = new GambaWhereClient(Log);
        _imageCache = new ImageCache(PluginInterface, TextureProvider, Log);

        var playerInfo = new PlayerInfoService(ClientState, ObjectTable, DataManager, Log);
        _sessionState = new SessionState();
        var hostFormState = new HostFormState();

        var pluginDirectory =
            PluginInterface.AssemblyLocation.DirectoryName ?? PluginInterface.AssemblyLocation.FullName;

        var customBanners = new CustomBannerStore(PluginInterface.ConfigDirectory.FullName, Log);
        var profileImages = new ProfileImageStore(PluginInterface.ConfigDirectory.FullName, Log);

        _discordWebhook = new DiscordWebhookService(Log, Configuration, _sessionState, pluginDirectory, customBanners);

        _sessionService = new SessionService(_client, playerInfo, _sessionState, Configuration, ClientState, Framework,
            Log,
            _discordWebhook, ChatGui);

        var eventTeleport = new EventLocationTeleportService(PluginInterface, DataManager, ObjectTable, ChatGui, Log);
        var pfInterop = new LookingForGroupInterop(Log);
        _partyFinderCreator = new PartyFinderCreator(AddonLifecycle, Framework, GameGui, Log, pfInterop, Condition, PartyList, ObjectTable);
        _partyFinderLocator = new PartyFinderLocator(PartyFinderGui, Framework, ChatGui, Log, pfInterop, Condition);
        _eventsTab = new GambaEventsTab(_client, _imageCache, eventTeleport, Configuration, playerInfo, _partyFinderLocator);

        var hostTab = new HostGambaTab(_sessionService, playerInfo, _client, _sessionState, Configuration, hostFormState, _imageCache, profileImages, _partyFinderCreator);
        var gameListTab = new GameListTab(_imageCache, Configuration);
        var profilesTab = new ProfilesTab(Configuration, _imageCache, profileImages);
        _recruitmentTab = new RecruitmentTab(_client, _imageCache, Configuration, playerInfo, profileImages, ChatGui);

        _pillOverlay = new SessionPillOverlay(_sessionState, Configuration, _sessionService);

        var settingsTab = new SettingsTab(Configuration, _imageCache, Log, _pillOverlay);
        var supportTab = new SupportTab(_imageCache, Configuration);
        var discordTab = new DiscordWebhookTab(Configuration, _discordWebhook, _imageCache, Log, customBanners);
        var alertsTab = new AlertsTab(Configuration, _client);

        _mainWindow =
            new MainWindow(_eventsTab, hostTab, profilesTab, gameListTab, _recruitmentTab, settingsTab, supportTab, discordTab, alertsTab, Configuration);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_pillOverlay);

        _alertingService = new AlertingService(
            Configuration,
            ChatGui,
            Toasts,
            Framework,
            Log,
            _mainWindow.OpenEventsTabExpanded);

        _hostMarkerService = new HostMarkerService(ObjectTable, ClientState, Configuration);
        _minimapHostOverlay = new MinimapHostOverlay(_hostMarkerService, GameGui, ClientState, ObjectTable, DataManager, Configuration);
        _windowSystem.AddWindow(_minimapHostOverlay);

        _alertFeed = new EventAlertFeed(_client)
        {
            OnEventsRefreshed = events =>
            {
                _alertingService.OnEventsRefreshed(events);
                _hostMarkerService.OnEventsRefreshed(events);
            }
        };

        _partnerIpcManager = new PartnerIpcManager(
            PluginInterface, _mainWindow, hostTab, ChatGui, Configuration, Log);
        _partnerIpcV2Manager = new PartnerIpcV2Manager(
            PluginInterface, _mainWindow, hostTab, ChatGui, Framework, Configuration, Log);

        hostTab.GetHostAutomaticRuleSources = () => BuildHostRuleSources(hostTab.GetSelectedGameType());

        _sessionService.RefreshAutomaticRulesFromIpc = category =>
            _partnerIpcV2Manager.GetRules(category) ?? _partnerIpcManager.GetRules(category);

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
        _eventsTab.Dispose();
        _recruitmentTab.Dispose();
        _alertFeed.Dispose();
        _alertingService.Dispose();
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _pillOverlay.Dispose();
        _minimapHostOverlay.Dispose();
        _hostMarkerService.Dispose();
        _partyFinderCreator.Dispose();
        _partyFinderLocator.Dispose();

        _partnerIpcManager.Dispose();
        _partnerIpcV2Manager.Dispose();

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
        _recruitmentTab.Tick();
        _alertFeed.Tick();
        _hostMarkerService.Tick();
        _pillOverlay.IsOpen = (_sessionState.IsActive || _pillOverlay.IsMoving) && Configuration.PillOverlayEnabled;
        _minimapHostOverlay.IsOpen =
            Configuration.MinimapHostIconsEnabled && ClientState.IsLoggedIn && _hostMarkerService.Markers.Count > 0;
    }

    private List<HostRuleSource> BuildHostRuleSources(string category)
    {
        var v2Sources = _partnerIpcV2Manager.GetRuleSources(category);
        var v2Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in v2Sources)
            v2Names.Add(source.Name);

        var sources = new List<HostRuleSource>();
        foreach (var v1Source in _partnerIpcManager.GetRuleSources(category))
            if (!v2Names.Contains(v1Source.Name))
                sources.Add(v1Source);

        sources.AddRange(v2Sources);
        return sources;
    }

    private void OnCommand(string command, string args) => _mainWindow.Toggle();

    private void OnConfigCommand(string command, string args) => OpenConfigUi();

    private void ToggleMainUi() => _mainWindow.Toggle();

    private void OpenConfigUi() => _mainWindow.OpenSettingsTab();
}
