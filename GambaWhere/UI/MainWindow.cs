using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GambaWhere.Config;
using GambaWhere.UI.Tabs;
using GambaWhere.Utility;

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
    private readonly Configuration _config;

    private string? _pendingTab;
    private int _pushedColours;

    public MainWindow(
        GambaEventsTab eventsTab,
        HostGambaTab hostTab,
        GameListTab gameListTab,
        SettingsTab settingsTab,
        SupportTab supportTab,
        DiscordWebhookTab discordWebhookTab,
        AlertsTab alertsTab,
        Configuration config)
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
        _config = config;
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

    public override void PreDraw()
    {
        _pushedColours = 0;
        var p = _config.PrimaryColour;
        var s = _config.SecondaryColour;

        ImGui.PushStyleColor(ImGuiCol.WindowBg,              ThemeColours.TintedWindowBg(p));
        ImGui.PushStyleColor(ImGuiCol.PopupBg,               ThemeColours.TintedPopupBg(p));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,               ThemeColours.ActiveFrameBg(p));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,        ThemeColours.ActiveFrameBgHovered(p));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,         ThemeColours.ActiveFrameBgActive(p));
        ImGui.PushStyleColor(ImGuiCol.Tab,                   ThemeColours.TabNormal(p));
        ImGui.PushStyleColor(ImGuiCol.TabHovered,            ThemeColours.TabHovered(p));
        ImGui.PushStyleColor(ImGuiCol.TabActive,             ThemeColours.TabSelected(p));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused,          ThemeColours.TabUnfocused(p));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive,    ThemeColours.TabSelected(p));
        ImGui.PushStyleColor(ImGuiCol.Button,                ThemeColours.ButtonNormal(p));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,         ThemeColours.ButtonHovered(p));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,          ThemeColours.ButtonPressed(p));
        ImGui.PushStyleColor(ImGuiCol.Header,                ThemeColours.CardBackground(p));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,         ThemeColours.FaqHeaderHovered(p));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,          ThemeColours.FaqHeaderActive(p));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,         ThemeColours.ScrollbarGrab(p));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered,  ThemeColours.ScrollbarGrabHovered(p));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive,   ThemeColours.ScrollbarGrabActive(p));
        ImGui.PushStyleColor(ImGuiCol.Border,                ThemeColours.InactiveBorder(p));
        ImGui.PushStyleColor(ImGuiCol.Separator,             ThemeColours.SectionSeparator(p));
        ImGui.PushStyleColor(ImGuiCol.TitleBg,               ThemeColours.TitleBg(p));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,         ThemeColours.TitleBgActive(p));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed,      ThemeColours.TitleBg(p));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,             ThemeColours.ActiveCheckMark(s));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab,            s);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive,      s);
        _pushedColours = 27;
    }

    public override void PostDraw()
    {
        if (_pushedColours > 0)
            ImGui.PopStyleColor(_pushedColours);
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
