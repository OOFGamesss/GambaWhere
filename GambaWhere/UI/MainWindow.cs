using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GambaWhere.Config;
using GambaWhere.UI.Tabs;
using GambaWhere.Utility;

namespace GambaWhere.UI;

/// <summary>Primary plugin window. Hosts an icon sidebar that switches between feature tabs and applies the user's theme colours.</summary>
public class MainWindow : Window, IDisposable
{
    private enum Tab
    {
        Events,
        Host,
        Profiles,
        GameList,
        Recruitment,
        Alerts,
        Settings,
        Support,
    }

    private enum SettingsSection
    {
        Ui,
        Chat,
        Discord,
        Booster,
        Other,
    }

    private enum RecruitmentSection
    {
        FindVenue,
        FindHost,
    }

    private readonly GambaEventsTab _eventsTab;
    private readonly HostGambaTab _hostTab;
    private readonly ProfilesTab _profilesTab;
    private readonly GameListTab _gameListTab;
    private readonly RecruitmentTab _recruitmentTab;
    private readonly DiscordWebhookTab _discordWebhookTab;
    private readonly SettingsTab _settingsTab;
    private readonly SupportTab _supportTab;
    private readonly AlertsTab _alertsTab;
    private readonly Configuration _config;

    private Tab _selected = Tab.Events;
    private SettingsSection _settingsSection = SettingsSection.Ui;
    private bool _settingsExpanded;
    private RecruitmentSection _recruitmentSection = RecruitmentSection.FindVenue;
    private bool _recruitmentExpanded;
    private int _pushedColours;

    public MainWindow(
        GambaEventsTab eventsTab,
        HostGambaTab hostTab,
        ProfilesTab profilesTab,
        GameListTab gameListTab,
        RecruitmentTab recruitmentTab,
        SettingsTab settingsTab,
        SupportTab supportTab,
        DiscordWebhookTab discordWebhookTab,
        AlertsTab alertsTab,
        Configuration config)
        : base("Gamba Where##MainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _eventsTab = eventsTab;
        _hostTab = hostTab;
        _profilesTab = profilesTab;
        _gameListTab = gameListTab;
        _recruitmentTab = recruitmentTab;
        _settingsTab = settingsTab;
        _supportTab = supportTab;
        _discordWebhookTab = discordWebhookTab;
        _alertsTab = alertsTab;
        _config = config;
    }

    public void OpenSettingsTab()
    {
        IsOpen = true;
        _selected = Tab.Settings;
        _settingsExpanded = true;
        _settingsSection = SettingsSection.Ui;
    }

    public void OpenHostGambaTab()
    {
        IsOpen = true;
        _selected = Tab.Host;
    }

    public void OpenEventsTabExpanded(string characterName)
    {
        IsOpen = true;
        _selected = Tab.Events;
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
        var sidebarWidth = 170f * ImGuiHelpers.GlobalScale;

        using (var sidebar = ImRaii.Child("##GambaWhereSidebar", new Vector2(sidebarWidth, 0f), true))
        {
            if (sidebar.Success)
                DrawSidebar();
        }

        ImGui.SameLine();

        using var content = ImRaii.Child("##GambaWhereContent", Vector2.Zero, false);
        if (!content.Success)
            return;

        DrawSelectedTab();
    }

    private void DrawSidebar()
    {
        ImGuiHelpers.ScaledDummy(4f);
        DrawNavItem(Tab.Events,    FontAwesomeIcon.Dice,           "Gamba Events");
        DrawNavItem(Tab.Host,      FontAwesomeIcon.HandHoldingUsd, "Host Gamba");
        DrawNavItem(Tab.GameList,  FontAwesomeIcon.ListUl,         "Game List");
        DrawRecruitmentNav();

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawNavItem(Tab.Profiles,  FontAwesomeIcon.Users,          "Profiles");
        DrawNavItem(Tab.Alerts,    FontAwesomeIcon.Bell,           "Alerts");

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawSettingsNav();
        DrawNavItem(Tab.Support,   FontAwesomeIcon.QuestionCircle, "Support");
    }

    private void DrawNavItem(Tab tab, FontAwesomeIcon icon, string label)
    {
        if (DrawSidebarRow($"tab_{tab}", icon, label, _selected == tab))
        {
            _selected = tab;
            _settingsExpanded = false;
            _recruitmentExpanded = false;
        }
    }

    private void DrawRecruitmentNav()
    {
        var chevron = _recruitmentExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;

        if (DrawSidebarRow("tab_Recruitment", FontAwesomeIcon.Handshake, "Recruitment", _selected == Tab.Recruitment, 0f, chevron))
        {
            _recruitmentExpanded = !_recruitmentExpanded;
            if (_recruitmentExpanded)
            {
                _selected = Tab.Recruitment;
                _recruitmentSection = RecruitmentSection.FindVenue;
                _settingsExpanded = false;
            }
        }

        if (!_recruitmentExpanded)
            return;

        var indent = 18f * ImGuiHelpers.GlobalScale;
        DrawRecruitmentSubItem(RecruitmentSection.FindVenue, FontAwesomeIcon.Store,   "Find a Venue", indent);
        DrawRecruitmentSubItem(RecruitmentSection.FindHost,  FontAwesomeIcon.UserTie, "Find a Host",  indent);
    }

    private void DrawRecruitmentSubItem(RecruitmentSection section, FontAwesomeIcon icon, string label, float indent)
    {
        var selected = _selected == Tab.Recruitment && _recruitmentSection == section;
        if (DrawSidebarRow($"recruitment_{section}", icon, label, selected, indent))
        {
            _selected = Tab.Recruitment;
            _recruitmentSection = section;
        }
    }

    private void DrawSettingsNav()
    {
        var chevron = _settingsExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;

        if (DrawSidebarRow("tab_Settings", FontAwesomeIcon.Cog, "Settings", _selected == Tab.Settings, 0f, chevron))
        {
            _settingsExpanded = !_settingsExpanded;
            if (_settingsExpanded)
            {
                _selected = Tab.Settings;
                _settingsSection = SettingsSection.Ui;
                _recruitmentExpanded = false;
            }
        }

        if (!_settingsExpanded)
            return;

        var indent = 18f * ImGuiHelpers.GlobalScale;
        DrawSettingsSubItem(SettingsSection.Ui,      FontAwesomeIcon.Palette,     "UI",              indent);
        DrawSettingsSubItem(SettingsSection.Chat,    FontAwesomeIcon.CommentDots, "Chat",            indent);
        DrawSettingsSubItem(SettingsSection.Discord, FontAwesomeIcon.Comments,    "Discord Webhook", indent);
        DrawSettingsSubItem(SettingsSection.Booster, FontAwesomeIcon.Gem,         "Booster Key",     indent);
        DrawSettingsSubItem(SettingsSection.Other,   FontAwesomeIcon.EllipsisH,   "Other",           indent);
    }

    private void DrawSettingsSubItem(SettingsSection section, FontAwesomeIcon icon, string label, float indent)
    {
        var selected = _selected == Tab.Settings && _settingsSection == section;
        if (DrawSidebarRow($"settings_{section}", icon, label, selected, indent))
        {
            _selected = Tab.Settings;
            _settingsSection = section;
        }
    }

    private bool DrawSidebarRow(string id, FontAwesomeIcon icon, string label, bool selected, float indent = 0f, FontAwesomeIcon? trailingIcon = null)
    {
        var style = ImGui.GetStyle();
        var scale = ImGuiHelpers.GlobalScale;

        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetTextLineHeight() + style.FramePadding.Y * 2f + 6f * scale;

        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.Selectable($"##sidebar_{id}", selected, ImGuiSelectableFlags.None, new Vector2(width, height));

        var dl = ImGui.GetWindowDrawList();
        var iconFont = UiBuilder.IconFont;
        var fontSize = ImGui.GetFontSize();
        var iconStr = icon.ToIconString();

        ImGui.PushFont(iconFont);
        var iconSize = ImGui.CalcTextSize(iconStr);
        ImGui.PopFont();

        var contentColour = selected
            ? ImGui.GetColorU32(_config.SecondaryColour)
            : ImGui.GetColorU32(ImGuiCol.Text);

        var iconBoxWidth = 26f * scale;
        var padX = 8f * scale + indent;

        var iconPos = new Vector2(
            origin.X + padX + (iconBoxWidth - iconSize.X) * 0.5f,
            origin.Y + (height - iconSize.Y) * 0.5f);
        dl.AddText(iconFont, fontSize, iconPos, contentColour, iconStr);

        var labelSize = ImGui.CalcTextSize(label);
        var labelPos = new Vector2(
            origin.X + padX + iconBoxWidth + style.ItemInnerSpacing.X,
            origin.Y + (height - labelSize.Y) * 0.5f);
        dl.AddText(labelPos, contentColour, label);

        if (trailingIcon.HasValue)
        {
            var trailingStr = trailingIcon.Value.ToIconString();
            ImGui.PushFont(iconFont);
            var trailingSize = ImGui.CalcTextSize(trailingStr);
            ImGui.PopFont();

            var trailingPos = new Vector2(
                origin.X + width - trailingSize.X - 8f * scale,
                origin.Y + (height - trailingSize.Y) * 0.5f);
            dl.AddText(iconFont, fontSize, trailingPos, contentColour, trailingStr);
        }

        return clicked;
    }

    private void DrawSelectedTab()
    {
        switch (_selected)
        {
            case Tab.Events:   _eventsTab.Draw(); break;
            case Tab.Host:     _hostTab.Draw(); break;
            case Tab.Profiles: _profilesTab.Draw(); break;
            case Tab.GameList: _gameListTab.Draw(); break;
            case Tab.Recruitment: DrawRecruitmentContent(); break;
            case Tab.Alerts:   _alertsTab.Draw(); break;
            case Tab.Settings: DrawSettingsContent(); break;
            case Tab.Support:  _supportTab.Draw(); break;
        }
    }

    private void DrawRecruitmentContent()
    {
        switch (_recruitmentSection)
        {
            case RecruitmentSection.FindVenue: _recruitmentTab.DrawFindVenueSection(); break;
            case RecruitmentSection.FindHost:  _recruitmentTab.DrawFindHostSection(); break;
        }
    }

    private void DrawSettingsContent()
    {
        switch (_settingsSection)
        {
            case SettingsSection.Ui:      _settingsTab.DrawUiSection(); break;
            case SettingsSection.Chat:    _settingsTab.DrawChatSection(); break;
            case SettingsSection.Discord: _discordWebhookTab.Draw(); break;
            case SettingsSection.Booster: _settingsTab.DrawBoosterSection(); break;
            case SettingsSection.Other:   _settingsTab.DrawOtherSection(); break;
        }
    }

    public void Dispose() { }
}
