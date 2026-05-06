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
    private readonly SettingsTab _settingsTab;

    private string? _pendingTab;

    public MainWindow(GambaEventsTab eventsTab, HostGambaTab hostTab, GameListTab gameListTab, SettingsTab settingsTab)
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

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##GambaWhereTabs");
        if (!tabBar.Success)
            return;

        DrawTab("Gamba Events", _eventsTab.Draw);
        DrawTab("Host Gamba", _hostTab.Draw);
        DrawTab("Game List", _gameListTab.Draw);
        DrawTab("Settings", _settingsTab.Draw);

        _pendingTab = null;
    }

    private void DrawTab(string label, Action drawContent)
    {
        var flags = _pendingTab == label ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        using var tab = ImRaii.TabItem(label, flags);
        if (!tab.Success)
            return;

        ImGui.Spacing();
        drawContent();
    }

    public void Dispose() { }
}
