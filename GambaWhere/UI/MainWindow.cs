using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GambaWhere.UI.Tabs;

namespace GambaWhere.UI;

public class MainWindow : Window, IDisposable
{
    private readonly GambaEventsTab _eventsTab;
    private readonly HostGambaTab _hostTab;
    private readonly GameListTab _gameListTab;
    private readonly DiscordWebhookTab _discordWebhookTab;
    private readonly SettingsTab _settingsTab;
    private readonly SupportTab _supportTab;
    private readonly AlertsTab _alertsTab;

    private string? _pendingTab;

    public MainWindow(
        GambaEventsTab eventsTab,
        HostGambaTab hostTab,
        GameListTab gameListTab,
        SettingsTab settingsTab,
        SupportTab supportTab,
        DiscordWebhookTab discordWebhookTab,
        AlertsTab alertsTab)
        : base("Gamba Where##MainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _eventsTab = eventsTab;
        _hostTab = hostTab;
        _gameListTab = gameListTab;
        _settingsTab = settingsTab;
        _supportTab = supportTab;

        _discordWebhookTab = discordWebhookTab;
        _alertsTab = alertsTab;

    }

    public void OpenSettingsTab()
    {
        IsOpen = true;
        _pendingTab = "Settings";
    }

    public void OpenHostGambaTab()
    {
        IsOpen = true;
        _pendingTab = "Host Gamba";
    }

    public void OpenEventsTabExpanded(string characterName)
    {
        IsOpen = true;
        _pendingTab = "Gamba Events";
        _eventsTab.ExpandAndScrollTo(characterName);
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##GambaWhereTabs");
        if (!tabBar.Success)
            return;

        DrawTab("Gamba Events", _eventsTab.Draw);
        DrawTab("Host Gamba", _hostTab.Draw);
        DrawTab("Game List", _gameListTab.Draw);

        DrawTab("Discord Webhook", _discordWebhookTab.Draw);

        DrawTab("Alerts", _alertsTab.Draw);

        DrawTab("Settings", _settingsTab.Draw);
        DrawTab("Support", _supportTab.Draw);

        _pendingTab = null;
    }

    private void DrawTab(string labelId, Action drawContent)
    {
        var flags =
            (_pendingTab == labelId ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);

        var tabItemLabel = $"{labelId}##GambaWhereTab_{labelId.Replace(" ", "")}";

        using var tab = ImRaii.TabItem(tabItemLabel, flags);
        if (!tab.Success)
            return;

        ImGui.Spacing();

        var childId = $"##GambaWhereTabContent_{labelId.Replace(" ", "")}";
        using var child = ImRaii.Child(childId, Vector2.Zero, false, ImGuiWindowFlags.None);
        if (!child.Success)
            return;

        drawContent();
    }

    public void Dispose() { }
}
