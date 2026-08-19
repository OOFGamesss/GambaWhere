using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.Models;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Partyfinder;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.Utility;
using GambaWhere.UI.Components;

namespace GambaWhere.UI.Tabs;

/// <summary>Tab for starting, monitoring, and stopping a hosting session, including game rule and preset management.</summary>
public class HostGambaTab
{
    private readonly SessionService _sessionService;
    private readonly PlayerInfoService _playerInfo;
    private readonly GambaWhereClient _client;
    private readonly HostSessions _sessions;
    private readonly Configuration _config;
    private readonly HostFormState _form;
    private readonly ImageService _imageService;
    private readonly PartyFinderCreator _partyFinderCreator;
    private readonly CategoryService _categoryService;
    private readonly ScheduledSessionService _scheduledSessions;

    private string[] GameTypes => GameCategories.Keys;

    private static readonly Vector4 Yellow = new(1f, 1f, 0f, 1f);
    private static readonly Vector4 SoftRed = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 SoftAmber = new(1f, 0.78f, 0.42f, 1f);

    private const string PendingUploadHint =
        "Your profile picture uploads when you start, so this session may take a few seconds longer to begin.";
    private const float PendingUploadHintWrap = 380f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private IRuleConfig[] _ruleConfigs;
    private string _selectedGameKey = string.Empty;

    private volatile bool _isFetchingVenues;

    private string _newPresetNameBuffer = string.Empty;
    private bool _showAddPresetInput;
    private bool _showRenamePresetInput;
    private string _renameBuffer = string.Empty;

    private string _saveNotificationName = string.Empty;
    private DateTime _saveNotificationUntil;

    private string _clipboardNotificationName = string.Empty;
    private DateTime _clipboardNotificationUntil;

    private string _importNameBuffer = string.Empty;
    private string _importKeyBuffer = string.Empty;
    private string _importError = string.Empty;

    private bool _showNewSessionForm;
    private string? _stoppingEventId;

    private readonly DateTimePicker _schedulePicker = new();
    private string _scheduleLocation = string.Empty;
    private int _scheduleRecurrenceIndex;
    private volatile bool _isScheduling;
    private string _scheduleError = string.Empty;
    private DateTime _scheduledConfirmFor;
    private string _scheduledConfirmGame = string.Empty;
    private string _scheduledConfirmVenue = string.Empty;
    private volatile bool _openScheduledConfirm;
    private volatile bool _showScheduleForm;
    private int _scheduleScrollFrames;

    private const string ScheduledConfirmPopupId = "Session Scheduled###gwScheduleConfirm";
    private const int ScheduleLocationMaxLength = 128;
    private const float ScheduleErrorWrap = 420f;

    private readonly ThemedCard _card = new();

    public Func<IReadOnlyList<HostRuleSource>>? GetHostAutomaticRuleSources { get; set; }

    public HostGambaTab(
        SessionService sessionService,
        PlayerInfoService playerInfo,
        GambaWhereClient client,
        HostSessions sessions,
        Configuration config,
        HostFormState form,
        ImageService imageService,
        PartyFinderCreator partyFinderCreator,
        CategoryService categoryService,
        ScheduledSessionService scheduledSessions)
    {
        _sessionService = sessionService;
        _playerInfo = playerInfo;
        _client = client;
        _sessions = sessions;
        _config = config;
        _form = form;
        _imageService = imageService;
        _partyFinderCreator = partyFinderCreator;
        _categoryService = categoryService;
        _scheduledSessions = scheduledSessions;

        _ruleConfigs = GameCatalog.CreateRuleConfigs();
        ClampSelectedGameIndex();
        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        _selectedGameKey = GetSelectedGameType();
        LoadSelectedPreset();
        FetchVenues();

        GameCategories.Changed += OnCategoriesChanged;
    }

    public string GetSelectedGameType() => GameTypes[_form.SelectedGameIndex];

    public void SelectGame(string gameType)
    {
        var index = Array.IndexOf(GameTypes, gameType);
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectRuleSourceByName(string name)
    {
        var sources = GetSources();
        for (var i = 0; i < sources.Count; i++)
        {
            if (string.Equals(sources[i].Name, name, StringComparison.Ordinal))
            {
                _form.SelectedRuleSourceIndex = i + 1;
                return;
            }
        }
    }

    public void OnSelected()
    {
        _categoryService.RequestRefresh();
        FetchVenues();
    }

    private void OnCategoriesChanged()
    {
        var previous = _selectedGameKey;
        _ruleConfigs = GameCatalog.CreateRuleConfigs();
        ClampSelectedGameIndex();

        if (!string.IsNullOrEmpty(previous))
        {
            var index = Array.IndexOf(GameTypes, previous);
            if (index >= 0)
                _form.SelectedGameIndex = index;
        }

        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        _selectedGameKey = GetSelectedGameType();
        LoadSelectedPreset();
        _config.EnsureDefaultPresets();
    }

    private void ClampSelectedGameIndex()
    {
        if (GameTypes.Length == 0)
        {
            _form.SelectedGameIndex = 0;
            return;
        }

        if (_form.SelectedGameIndex < 0 || _form.SelectedGameIndex >= GameTypes.Length)
            _form.SelectedGameIndex = 0;
    }

    public void Dispose()
    {
        GameCategories.Changed -= OnCategoriesChanged;
    }

    public void Draw()
    {
        HostFieldTheme.Primary = _config.PrimaryColour;
        HostFieldTheme.Secondary = _config.SecondaryColour;

        var sessions = _sessions.Snapshot();
        var showingSchedule = _showScheduleForm;
        var showingForm = !showingSchedule && (sessions.Count == 0 || _showNewSessionForm);

        var scale = ImGuiHelpers.GlobalScale;
        var footerHeight = 76f * scale;

        if (showingForm && HasPendingImageUpload())
        {
            var wrapW = Math.Min(ImGui.GetContentRegionAvail().X, PendingUploadHintWrap * scale);
            footerHeight += ImGui.CalcTextSize(PendingUploadHint, false, wrapW).Y
                + ImGui.GetStyle().ItemSpacing.Y + 4f * scale;
        }

        if (showingSchedule && !string.IsNullOrEmpty(_scheduleError))
        {
            var wrapW = Math.Min(ImGui.GetContentRegionAvail().X, ScheduleErrorWrap * scale);
            footerHeight += ImGui.CalcTextSize(_scheduleError, false, wrapW).Y
                + ImGui.GetStyle().ItemSpacing.Y + 4f * scale;
        }

        var scrollHeight = Math.Max(80f * scale, ImGui.GetContentRegionAvail().Y - footerHeight);

        using (var scroll = ImRaii.Child("##gw_host_scroll", new Vector2(0f, scrollHeight), false))
        {
            if (scroll.Success)
            {
                if (showingSchedule)
                    DrawScheduleForm();
                else if (showingForm)
                    DrawConfigForm();
                else
                    DrawActiveSessionList(sessions);
            }
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (showingSchedule)
            DrawScheduleBar();
        else if (showingForm)
            DrawBottomBar(sessions.Count);
        else
            DrawSessionListBar(sessions.Count);

        DrawScheduledConfirmPopup();
    }

    private void DrawScheduledConfirmPopup()
    {
        if (_openScheduledConfirm)
        {
            ImGui.OpenPopup(ScheduledConfirmPopupId);
            _openScheduledConfirm = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var open = true;

        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 6f * scale);
        using var modal = ImRaii.PopupModal(ScheduledConfirmPopupId, ref open,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal.Success)
            return;

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Your session is booked in.");

        ImGuiHelpers.ScaledDummy(6f);

        var labelX = 90f * scale;
        DrawActiveField("Starts", ServerTime.FormatServerTimeWithLocal(_scheduledConfirmFor), labelX);
        DrawActiveField("Game", _scheduledConfirmGame, labelX);
        DrawActiveField("Venue", _scheduledConfirmVenue, labelX);

        ImGuiHelpers.ScaledDummy(10f);

        var buttonSize = new Vector2(120f * scale, 0f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX()
            + Math.Max(0f, (ImGui.GetContentRegionAvail().X - buttonSize.X) * 0.5f));

        using (UIHelper.PushGreenButtonColours())
        {
            if (ImGui.Button("OK##gwScheduleConfirmOk", buttonSize))
                ImGui.CloseCurrentPopup();
        }
    }

    private void DrawActiveSessionList(IReadOnlyList<SessionState> sessions)
    {
        ImGuiHelpers.ScaledDummy(8f);

        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            using var id = ImRaii.PushId(session.EventId);

            var title = string.IsNullOrEmpty(session.VenueName) || session.VenueName == "No Venue"
                ? session.GameType
                : $"{session.GameType} at {session.VenueName}";

            DrawCard($"##gw_active_session_{session.EventId}", title, () => DrawActiveSessionBody(session));
            ImGuiHelpers.ScaledDummy(6f);
            DrawPartyFinderButtons(session);

            if (i < sessions.Count - 1)
            {
                ImGuiHelpers.ScaledDummy(10f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(4f);
            }
        }

        ImGuiHelpers.ScaledDummy(8f);

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(SoftRed, _form.StatusMessage);
        }
    }

    private void DrawConfigForm()
    {
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_setup_panel", "Setup", DrawSetupCardBody);
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_rules_panel", "Game Rules", DrawRulesCardBody);
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_session_details", "Session Details", DrawSessionDetailsBody);
        ImGuiHelpers.ScaledDummy(10f);
    }

    private void DrawCard(string id, string title, Action content) =>
        _card.Draw(id, title, _config.PrimaryColour, _config.SecondaryColour, content);

    private void DrawSetupCardBody()
    {
        DrawProfileRow();
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
        DrawVenueGameRow();
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
        DrawPresetRow();
    }

    private GambaProfile? GetSelectedProfile() =>
        _config.Profiles.FirstOrDefault(p => p.Id == _config.SelectedProfileId);

    private bool HasPendingImageUpload()
    {
        var profile = GetSelectedProfile();
        if (profile == null || !string.IsNullOrEmpty(profile.UploadedImageUrl))
            return false;

        return _imageService.GetProfileImagePath(profile.ImageFileName) != null;
    }

    private void DrawProfileRow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var diameter = 56f * scale;
        var comboWidth = 200f * scale;
        var selected = GetSelectedProfile();

        var path = selected != null ? _imageService.GetProfileImagePath(selected.ImageFileName) : null;
        var tex = path != null ? _imageService.GetFromPath(path) : null;

        var avail = ImGui.GetContentRegionAvail().X;
        var blockWidth = diameter + ImGui.GetStyle().ItemSpacing.X + comboWidth;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - blockWidth) * 0.5f));

        var startY = ImGui.GetCursorPosY();
        CircleImage.DrawInline(diameter, tex);
        ImGui.SameLine();

        var groupHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight();
        ImGui.SetCursorPosY(startY + Math.Max(0f, (diameter - groupHeight) * 0.5f));

        using (ImRaii.Group())
        {
            HostField.Label("Profile");
            ImGui.SetNextItemWidth(comboWidth);
            using var combo = ImRaii.Combo("##ProfilePicker", selected?.Name ?? "None");
            if (combo)
            {
                if (ImGui.Selectable("None", selected == null) && selected != null)
                {
                    _config.SelectedProfileId = null;
                    _config.Save();
                }

                foreach (var profile in _config.Profiles)
                {
                    if (ImGui.Selectable(profile.Name, profile.Id == _config.SelectedProfileId)
                        && profile.Id != _config.SelectedProfileId)
                    {
                        _config.SelectedProfileId = profile.Id;
                        _config.Save();
                    }
                }
            }
        }

        if (_config.Profiles.Count == 0)
        {
            const string msg = "Create one on the Profiles tab.";
            var msgW = ImGui.CalcTextSize(msg).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - msgW) * 0.5f));
            ImGui.TextDisabled(msg);
        }
    }

    private void DrawRulesCardBody() => DrawRulesSection();

    private void DrawPresetRow()
    {
        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);
        if (presets.Count == 0)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var presetNames = presets.Select(p => p.Name).ToArray();
        var presetIdx = _form.SelectedPresetIndex;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        CentreRow(
            MeasureIconTextButtonWidth(FontAwesomeIcon.FileImport, "Import")
            + spacing
            + MeasureIconTextButtonWidth(FontAwesomeIcon.Copy, "Export"));

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.FileImport, "Import", "##ImportPresetBtn"))
            {
                _importNameBuffer = string.Empty;
                _importKeyBuffer = string.Empty;
                _importError = string.Empty;
                ImGui.OpenPopup("ImportPresetPopup");
            }
        }

        ImGui.SameLine();

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Copy, "Export", "##ExportToClipboard"))
                ExportCurrentPreset(presets);
        }

        ImGuiHelpers.ScaledDummy(4f);

        var comboWidth = 200 * scale;
        var iconGap = 4 * scale;
        var actionsWidth =
            MeasureIconButtonWidth(FontAwesomeIcon.Save) + iconGap
            + MeasureIconButtonWidth(FontAwesomeIcon.Plus) + iconGap
            + MeasureIconButtonWidth(FontAwesomeIcon.Pen)
            + (presets.Count > 1 ? iconGap + MeasureIconButtonWidth(FontAwesomeIcon.Trash) : 0f);

        CentreRow(
            ImGui.CalcTextSize("Presets").X + spacing
            + comboWidth + spacing
            + actionsWidth);

        ImGui.AlignTextToFramePadding();
        HostField.Label("Presets");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(comboWidth);
        using (var presetCombo = ImRaii.Combo("##PresetPicker", presetIdx < presetNames.Length ? presetNames[presetIdx] : string.Empty))
        {
            if (presetCombo)
            {
                for (var i = 0; i < presetNames.Length; i++)
                {
                    if (ImGui.Selectable(presetNames[i], i == presetIdx) && i != _form.SelectedPresetIndex)
                    {
                        _form.SelectedPresetIndex = i;
                        LoadSelectedPreset();
                    }
                }
            }
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
                   .Push(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.12f))
                   .Push(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.20f)))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.35f, 0.6f, 0.95f, 1f)))
            {
                if (ImGuiComponents.IconButton("##SavePreset", FontAwesomeIcon.Save))
                    SaveCurrentPreset(presets);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Save");

            ImGui.SameLine(0, 4 * scale);

            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.3f, 0.85f, 0.3f, 1f)))
            {
                if (ImGuiComponents.IconButton("##AddPreset", FontAwesomeIcon.Plus))
                {
                    _showAddPresetInput = !_showAddPresetInput;
                    _showRenamePresetInput = false;
                    _newPresetNameBuffer = string.Empty;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add");

            ImGui.SameLine(0, 4 * scale);

            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.96f, 0.66f, 0.13f, 1f)))
            {
                if (ImGuiComponents.IconButton("##RenamePreset", FontAwesomeIcon.Pen))
                {
                    _showRenamePresetInput = !_showRenamePresetInput;
                    _showAddPresetInput = false;
                    _renameBuffer = presets[_form.SelectedPresetIndex].Name;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Rename");

            if (presets.Count > 1)
            {
                ImGui.SameLine(0, 4 * scale);

                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.92f, 0.34f, 0.34f, 1f)))
                {
                    if (ImGuiComponents.IconButton("##DeletePreset", FontAwesomeIcon.Trash))
                        DeleteCurrentPreset(presets);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Delete");
            }
        }

        if (DateTime.UtcNow < _saveNotificationUntil)
            ImGui.TextColored(Yellow, $"{_saveNotificationName} saved!");

        if (DateTime.UtcNow < _clipboardNotificationUntil)
            ImGui.TextColored(Yellow, $"{_clipboardNotificationName} copied to clipboard!");

        if (_showAddPresetInput)
            DrawAddPresetInput(presets);

        if (_showRenamePresetInput)
            DrawRenamePresetInput(presets);

        DrawImportPresetPopup(presets);
    }

    private void DrawVenueGameRow()
    {
        using var table = ImRaii.Table("##gw_venue_game", 4, ImGuiTableFlags.None);
        if (!table)
            return;

        var labelWidth = Math.Max(ImGui.CalcTextSize("Venue").X, ImGui.CalcTextSize("Game").X);

        ImGui.TableSetupColumn("##vlabel", ImGuiTableColumnFlags.WidthFixed, labelWidth);
        ImGui.TableSetupColumn("##vfield", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##glabel", ImGuiTableColumnFlags.WidthFixed, labelWidth);
        ImGui.TableSetupColumn("##gfield", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        HostField.Label("Venue");

        ImGui.TableNextColumn();
        DrawVenueField();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        HostField.Label("Game");

        ImGui.TableNextColumn();
        DrawGameField();
    }

    private void DrawVenueField()
    {
        ImGui.SetNextItemWidth(-1);
        var venueName = _form.SelectedVenueName;
        if (VenueSearchCombo.Draw("##VenuePicker", ref venueName, _config.FavouriteVenues, () => _config.Save()))
            _form.SelectedVenueName = venueName;
    }

    private void DrawGameField()
    {
        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo("##GamePicker", GameTypes[_form.SelectedGameIndex]);
        if (!combo)
            return;

        for (var i = 0; i < GameTypes.Length; i++)
        {
            if (ImGui.Selectable(GameTypes[i], i == _form.SelectedGameIndex) && i != _form.SelectedGameIndex)
            {
                _form.SelectedGameIndex = i;
                OnGameTypeChanged();
            }
        }
    }

    private void DrawRulesSection()
    {
        var sources = GetSources();
        if (sources.Count > 0)
            DrawRulesSourceSelector(sources);
        else
            _form.RuleConfig?.Draw();
    }

    private void DrawRulesSourceSelector(IReadOnlyList<HostRuleSource> sources)
    {
        var segments = new[] { "Manual" }.Concat(sources.Select(s => s.Name)).ToArray();
        if (_form.SelectedRuleSourceIndex < 0 || _form.SelectedRuleSourceIndex >= segments.Length)
            _form.SelectedRuleSourceIndex = 0;

        var muted = new[] { false }.Concat(sources.Select(s => !s.IsLive)).ToArray();

        HostField.Label("Rules Source");
        _form.SelectedRuleSourceIndex = DrawSegmentedBar("##gw_rules_source", segments, _form.SelectedRuleSourceIndex, _config.SecondaryColour, muted);
        _form.UseManualHostRules = _form.SelectedRuleSourceIndex == 0;

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        if (_form.SelectedRuleSourceIndex == 0)
        {
            _form.RuleConfig?.Draw();
            return;
        }

        var source = sources[_form.SelectedRuleSourceIndex - 1];
        var liveRules = source.GetRules();

        if (liveRules == null || liveRules.Count == 0)
        {
            ImGui.TextWrapped($"{source.Name} isn't running right now.");
            ImGui.TextDisabled("Its rules will be read when the session starts.");
            return;
        }

        DrawRuleKeyValues(liveRules);
    }

    private static int DrawSegmentedBar(string id, string[] segments, int selected, Vector4 accent, bool[]? muted = null)
    {
        var result = selected;
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = 4f * scale;
        var framePadX = ImGui.GetStyle().FramePadding.X * 2f;
        var avail = ImGui.GetContentRegionAvail().X;
        var lineWidth = 0f;

        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f * scale);

        for (var i = 0; i < segments.Length; i++)
        {
            var segmentWidth = ImGui.CalcTextSize(segments[i]).X + framePadX;

            if (i > 0 && lineWidth + spacing + segmentWidth <= avail)
            {
                ImGui.SameLine(0f, spacing);
                lineWidth += spacing + segmentWidth;
            }
            else
            {
                lineWidth = segmentWidth;
            }

            var isSelected = i == selected;
            var isMuted = muted != null && i < muted.Length && muted[i];
            var fade = isMuted ? 0.45f : 1f;
            var baseCol = new Vector4(accent.X, accent.Y, accent.Z, (isSelected ? 0.55f : 0.14f) * fade);
            var hovCol = new Vector4(accent.X, accent.Y, accent.Z, (isSelected ? 0.66f : 0.30f) * fade);
            var actCol = new Vector4(accent.X, accent.Y, accent.Z, 0.75f * fade);

            using var colours = ImRaii.PushColor(ImGuiCol.Button, baseCol)
                .Push(ImGuiCol.ButtonHovered, hovCol)
                .Push(ImGuiCol.ButtonActive, actCol)
                .Push(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], isMuted);

            if (ImGui.Button($"{segments[i]}##{id}_seg{i}"))
                result = i;
        }

        return result;
    }

    private IReadOnlyList<HostRuleSource> GetSources() =>
        GetHostAutomaticRuleSources?.Invoke() ?? Array.Empty<HostRuleSource>();

    private void OnGameTypeChanged()
    {
        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        _selectedGameKey = GetSelectedGameType();
        _form.SelectedPresetIndex = 0;
        _form.SelectedRuleSourceIndex = 0;
        LoadSelectedPreset();
        _showAddPresetInput = false;
        _showRenamePresetInput = false;
        if (GetSources().Count == 0)
            _form.UseManualHostRules = false;
    }

    private void DrawSessionDetailsBody()
    {
        DrawDescriptionField();

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        HostField.Label("Current Location");
        ImGui.TextWrapped(_playerInfo.GetCurrentLocation() ?? "Unknown");

        ImGuiHelpers.ScaledDummy(8f);
        DrawAutoEndControl();
    }

    private void DrawDescriptionField()
    {
        HostField.Label("Description");
        ImGui.SetNextItemWidth(-1);
        var desc = _form.Description;
        if (ImGui.InputTextMultiline("##Description", ref desc, 512, new Vector2(0, 60 * ImGuiHelpers.GlobalScale)))
            _form.Description = desc;

        var charCount = _form.Description.Length;
        var overLimit = charCount >= 511;
        var countColour = overLimit ? new Vector4(1f, 0.2f, 0.2f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
        ImGui.TextColored(countColour, $"{charCount} / 511");
        if (overLimit)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "- Description is too long!");
        }
    }

    private void DrawBottomBar(int sessionCount)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            var msgW = ImGui.CalcTextSize(_form.StatusMessage).X;
            ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - msgW) * 0.5f));
            ImGui.TextColored(SoftRed, _form.StatusMessage);
        }

        if (HasPendingImageUpload())
        {
            var wrapW = Math.Min(avail, PendingUploadHintWrap * scale);
            var startX = baseX + Math.Max(0f, (avail - wrapW) * 0.5f);
            ImGui.SetCursorPosX(startX);
            ImGui.PushTextWrapPos(startX + wrapW);
            ImGui.TextColored(SoftAmber, PendingUploadHint);
            ImGui.PopTextWrapPos();
            ImGuiHelpers.ScaledDummy(4f);
        }

        var startLabel = _form.IsStarting ? "Starting..." : "Start Session";
        var scheduleLabel = _isScheduling ? "Scheduling..." : "Schedule Session";
        var btnSize = new Vector2(240f * scale, 46f * scale);
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        var backWidth = sessionCount > 0
            ? MeasureIconTextButtonWidth(FontAwesomeIcon.ArrowLeft, "Back") + spacing
            : 0f;
        var groupWidth = backWidth + btnSize.X * 2f + spacing;

        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - groupWidth) * 0.5f));

        if (sessionCount > 0)
        {
            using (UIHelper.PushBlueButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Back", "##gw_back_to_sessions"))
                {
                    _form.StatusMessage = null;
                    _showNewSessionForm = false;
                }
            }

            ImGui.SameLine();
        }

        using (UIHelper.PushGreenButtonColours())
        using (ImRaii.Disabled(_form.IsStarting))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, startLabel, btnSize, "##StartHosting"))
                TriggerStartSession();
        }

        ImGui.SameLine();

        using (UIHelper.PushVioletButtonColours())
        using (ImRaii.Disabled(_isScheduling))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.CalendarPlus, scheduleLabel, btnSize, "##gw_schedule_session_btn"))
                OpenScheduleForm();
        }
    }

    private void OpenScheduleForm()
    {
        _schedulePicker.Reset(DateTime.UtcNow);
        _scheduleLocation = _playerInfo.GetCurrentLocation() ?? string.Empty;
        if (_scheduleLocation.Length > ScheduleLocationMaxLength)
            _scheduleLocation = _scheduleLocation[..ScheduleLocationMaxLength];

        _scheduleRecurrenceIndex = 0;
        _scheduleError = string.Empty;
        _form.StatusMessage = null;
        _showScheduleForm = true;
        _scheduleScrollFrames = 3;
    }

    private void CloseScheduleForm()
    {
        _scheduleError = string.Empty;
        _showScheduleForm = false;
    }

    private void DrawScheduleForm()
    {
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_setup_panel", "Setup", DrawSetupCardBody);
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_rules_panel", "Game Rules", DrawRulesCardBody);
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_schedule_details", "Session Details", DrawDescriptionField);
        ImGuiHelpers.ScaledDummy(8f);
        DrawCard("##gw_schedule_when", "When and Where", DrawScheduleWhenBody);
        ImGuiHelpers.ScaledDummy(10f);

        if (_scheduleScrollFrames <= 0)
            return;

        _scheduleScrollFrames--;
        ImGui.SetScrollY(ImGui.GetScrollMaxY());
    }

    private void DrawScheduleWhenBody()
    {
        var accent = _config.SecondaryColour;

        _schedulePicker.Draw("##gw_schedule_picker", accent);

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        HostField.Label("Location");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##gw_schedule_location", ref _scheduleLocation, ScheduleLocationMaxLength);

        var overLimit = _scheduleLocation.Length >= ScheduleLocationMaxLength;
        ImGui.TextColored(
            overLimit ? new Vector4(1f, 0.2f, 0.2f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f),
            $"{_scheduleLocation.Length} / {ScheduleLocationMaxLength}");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        HostField.Label("Repeats");
        _scheduleRecurrenceIndex = DrawSegmentedBar(
            "##gw_schedule_recurrence", ScheduleRecurrence.Labels, _scheduleRecurrenceIndex, accent);

        ImGuiHelpers.ScaledDummy(4f);
        DrawRecurrencePreview();
    }

    private void DrawScheduleBar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;

        if (!string.IsNullOrEmpty(_scheduleError))
        {
            var wrapW = Math.Min(avail, ScheduleErrorWrap * scale);
            var startX = baseX + Math.Max(0f, (avail - wrapW) * 0.5f);
            ImGui.SetCursorPosX(startX);
            ImGui.PushTextWrapPos(startX + wrapW);
            ImGui.TextColored(SoftRed, _scheduleError);
            ImGui.PopTextWrapPos();
            ImGuiHelpers.ScaledDummy(4f);
        }

        var btnSize = new Vector2(240f * scale, 46f * scale);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var backWidth = MeasureIconTextButtonWidth(FontAwesomeIcon.ArrowLeft, "Back");

        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - backWidth - spacing - btnSize.X) * 0.5f));

        using (UIHelper.PushBlueButtonColours())
        using (ImRaii.Disabled(_isScheduling))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.ArrowLeft, "Back", "##gw_schedule_back"))
                CloseScheduleForm();
        }

        ImGui.SameLine();

        using (UIHelper.PushVioletButtonColours())
        using (ImRaii.Disabled(_isScheduling))
        {
            var label = _isScheduling ? "Scheduling..." : "Confirm Schedule";
            if (UIHelper.IconTextButton(FontAwesomeIcon.CalendarPlus, label, btnSize, "##gw_schedule_confirm"))
                TriggerScheduleSession();
        }
    }

    private void DrawRecurrencePreview()
    {
        var recurrence = ScheduleRecurrence.All[_scheduleRecurrenceIndex];
        var first = _schedulePicker.Value;

        if (recurrence == ScheduleRecurrence.Once)
        {
            ImGui.TextDisabled("Runs once, then disappears.");
            return;
        }

        var day = recurrence == ScheduleRecurrence.MonthlyDate
            ? first.Day
            : ScheduleRecurrence.MondayBased(first);
        var week = recurrence == ScheduleRecurrence.MonthlyWeekday ? WeekOfMonth(first) : (int?)null;

        ImGui.TextDisabled(ScheduleRecurrence.Describe(recurrence, first, day, week));

        ImGuiHelpers.ScaledDummy(4f);

        using var table = ImRaii.Table("##gw_schedule_occurrences", 3,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV);
        if (!table)
            return;

        ImGui.TableSetupColumn("##when", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Server Time", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Local Time", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        DrawOccurrenceRow("First", first);

        var next = first;
        for (var i = 0; i < 3; i++)
        {
            next = NextOccurrence(next, recurrence, day, week);
            DrawOccurrenceRow(i == 0 ? "Then" : string.Empty, next);
        }
    }

    private static void DrawOccurrenceRow(string label, DateTime occurrence)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(ServerTime.FormatServerTimeShort(occurrence));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(ServerTime.FormatLocalDateTime(occurrence));
    }

    private static int WeekOfMonth(DateTime moment) =>
        moment.Day + 7 > ServerTime.DaysInMonth(moment.Year, moment.Month)
            ? 5
            : (moment.Day - 1) / 7 + 1;

    private static DateTime NextOccurrence(DateTime current, string recurrence, int day, int? week)
    {
        switch (recurrence)
        {
            case ScheduleRecurrence.Weekly:
                return current.AddDays(7);
            case ScheduleRecurrence.Fortnightly:
                return current.AddDays(14);
            case ScheduleRecurrence.MonthlyDate:
            {
                var start = new DateTime(current.Year, current.Month, 1, current.Hour, current.Minute, 0, DateTimeKind.Utc).AddMonths(1);
                return start.AddDays(Math.Min(day, ServerTime.DaysInMonth(start.Year, start.Month)) - 1);
            }
            case ScheduleRecurrence.MonthlyWeekday:
            {
                var start = new DateTime(current.Year, current.Month, 1, current.Hour, current.Minute, 0, DateTimeKind.Utc).AddMonths(1);
                var daysInMonth = ServerTime.DaysInMonth(start.Year, start.Month);
                var offset = (day - ScheduleRecurrence.MondayBased(start) + 7) % 7;
                var target = week is 5
                    ? 1 + offset + 7 * ((daysInMonth - 1 - offset) / 7)
                    : 1 + offset + 7 * ((week ?? 1) - 1);
                return start.AddDays(Math.Min(target, daysInMonth) - 1);
            }
            default:
                return current;
        }
    }

    private void TriggerScheduleSession()
    {
        if (!_playerInfo.IsLoggedIn)
        {
            _scheduleError = "You must be logged in to schedule a session.";
            return;
        }

        var scheduledFor = _schedulePicker.Value;
        if (scheduledFor < DateTime.UtcNow.AddMinutes(5))
        {
            _scheduleError = "Pick a time at least 5 minutes from now.";
            return;
        }

        if (scheduledFor > DateTime.UtcNow.AddDays(365))
        {
            _scheduleError = "Pick a time within the next year.";
            return;
        }

        var location = _scheduleLocation.Trim();
        if (location.Length == 0)
        {
            _scheduleError = "Enter where the session will be held.";
            return;
        }

        if (UserTextGuard.ContainsDisallowedContent(location))
        {
            _scheduleError = "Location must not contain URLs or HTML.";
            return;
        }

        if (_form.Description.Length > 511)
        {
            _scheduleError = "Description must be 511 characters or fewer.";
            return;
        }

        if (UserTextGuard.ContainsDisallowedContent(_form.Description))
        {
            _scheduleError = "Description must not contain URLs or HTML.";
            return;
        }

        _scheduleError = string.Empty;
        _isScheduling = true;

        ResolveRuleSource(out var automaticSourceName, out var presetName);

        var request = new PostScheduledEventRequest
        {
            CharacterName = _playerInfo.GetCharacterName()!,
            ScheduledFor = scheduledFor,
            Location = location,
            Game = GameTypes[_form.SelectedGameIndex],
            Description = _form.Description.Trim(),
            VenueName = _form.SelectedVenueName ?? "No Venue",
            Recurrence = ScheduleRecurrence.All[_scheduleRecurrenceIndex],
            DataCentre = _playerInfo.GetCurrentDataCentre(),
            World = _playerInfo.GetCurrentWorld(),
            BoosterKey = string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim()
        };

        var profile = GetSelectedProfile();
        var sentPictureHash = AttachProfileToSchedule(request, profile);

        _ = Task.Run(async () =>
        {
            var error = await _scheduledSessions.ScheduleAsync(request, automaticSourceName, presetName, profile?.Id, sentPictureHash);
            _isScheduling = false;

            if (error != null)
            {
                _scheduleError = error;
                return;
            }

            _scheduleError = string.Empty;
            _scheduledConfirmFor = scheduledFor;
            _scheduledConfirmGame = request.Game;
            _scheduledConfirmVenue = request.VenueName;
            _openScheduledConfirm = true;
            _showScheduleForm = false;
        });
    }

    private string? AttachProfileToSchedule(PostScheduledEventRequest request, GambaProfile? profile)
    {
        if (profile == null)
            return null;

        request.Bio = string.IsNullOrWhiteSpace(profile.Bio) ? null : profile.Bio.Trim();
        request.PreferredGames = new List<string>(profile.PreferredGames);
        request.BorderStyle = profile.BorderStyle;
        request.CardEffectStyle = profile.CardEffectStyle;

        var path = _imageService.GetProfileImagePath(profile.ImageFileName);
        if (path == null || !_imageService.TryEncodeProfileImage(path, out var b64, out var hash))
            return null;

        if (!string.IsNullOrEmpty(profile.UploadedImageUrl) && profile.UploadedImageHash == hash)
        {
            request.ProfileImageUrl = profile.UploadedImageUrl;
            return null;
        }

        request.ProfilePictureB64 = b64;
        return hash;
    }

    private void DrawAutoEndControl()
    {
        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, ThemeColours.AccentText(_config.SecondaryColour)))
            ImGui.TextUnformatted("Auto End Session");

        ImGui.SameLine(0f, spacing);
        var autoEnd = _form.AutoEndEnabled;
        if (ToggleSwitch.Draw("##gw_autoend_toggle", ref autoEnd, ThemeColours.ActiveCheckMark(_config.SecondaryColour)))
        {
            _form.AutoEndEnabled = autoEnd;
            if (autoEnd)
                ImGui.OpenPopup("AutoEndPopup");
        }

        if (_form.AutoEndEnabled)
        {
            ImGui.SameLine(0f, spacing);
            if (ImGui.SmallButton($"{_form.AutoEndHours}h {_form.AutoEndMinutes}m##gw_autoend_summary"))
                ImGui.OpenPopup("AutoEndPopup");
        }

        DrawAutoEndPopup();
    }

    private void DrawAutoEndPopup()
    {
        using var popup = ImRaii.Popup("AutoEndPopup", ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        HostField.Label("Auto End After");
        var fieldWidth = 72 * ImGuiHelpers.GlobalScale;

        ImGui.SetNextItemWidth(fieldWidth);
        var hours = _form.AutoEndHours;
        if (ImGui.InputInt("##AutoEndHours", ref hours, 0))
            _form.AutoEndHours = Math.Clamp(hours, 0, 23);
        ImGui.SameLine();
        ImGui.TextDisabled("h");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(fieldWidth);
        var minutes = _form.AutoEndMinutes;
        if (ImGui.InputInt("##AutoEndMinutes", ref minutes, 0))
            _form.AutoEndMinutes = Math.Clamp(minutes, 0, 59);
        ImGui.SameLine();
        ImGui.TextDisabled("m");

        ImGuiHelpers.ScaledDummy(6f);
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Done", "##AutoEndDone"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawActiveSessionBody(SessionState session)
    {
        var labelX = 120 * ImGuiHelpers.GlobalScale;

        DrawActiveField("Character:", session.CharacterName, labelX);
        DrawActiveField("Game:", session.GameType, labelX);
        DrawActiveField("Venue:", session.VenueName ?? "No Venue", labelX);
        DrawActiveField("Running:", session.Elapsed().ToString(@"hh\:mm\:ss"), labelX);

        if (session.AutoEndAt.HasValue)
        {
            ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Auto End:");
            ImGui.SameLine(labelX);
            var remaining = session.AutoEndAt.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                ImGui.TextColored(SoftRed, "Ending...");
            }
            else
            {
                var timerColour = remaining.TotalMinutes <= 5
                    ? new Vector4(1f, 0.65f, 0.1f, 1f)
                    : new Vector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(timerColour, remaining.ToString(@"hh\:mm\:ss"));
            }
        }

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Location:");
        ImGui.SameLine(labelX);
        ImGui.TextWrapped(session.Location);

        if (session.IsPaused)
        {
            ImGuiHelpers.ScaledDummy(2f);
            ImGui.TextColored(SoftAmber, SessionService.BreakMessage);
        }

        DrawActiveRules(session);

        ImGuiHelpers.ScaledDummy(8f);
        DrawSessionControls(session);
    }

    private void DrawSessionControls(SessionState session)
    {
        var stopping = _stoppingEventId == session.EventId;

        var pauseIcon = session.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        var pauseLabel = session.IsPaused ? "Resume" : "Take a Break";

        using (ImRaii.Disabled(stopping))
        {
            using (UIHelper.PushAmberButtonColours())
            {
                if (UIHelper.IconTextButton(pauseIcon, pauseLabel, "##gw_session_pause"))
                {
                    var eventId = session.EventId;
                    _ = Task.Run(() => _sessionService.TogglePauseAsync(eventId));
                }
            }

            ImGui.SameLine();

            using (UIHelper.PushRedButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, stopping ? "Stopping..." : "Stop Session", "##gw_session_stop"))
                    TriggerStopSession(session.EventId);
            }
        }
    }

    private void DrawSessionListBar(int sessionCount)
    {
        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;

        var atCapacity = sessionCount >= HostSessions.MaxConcurrent;
        var newLabel = $"New Session ({sessionCount}/{HostSessions.MaxConcurrent})";

        var newWidth = MeasureIconTextButtonWidth(FontAwesomeIcon.Plus, newLabel);
        var stopAllWidth = MeasureIconTextButtonWidth(FontAwesomeIcon.StopCircle, "Stop All");
        var groupWidth = newWidth + stopAllWidth + ImGui.GetStyle().ItemSpacing.X;

        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - groupWidth) * 0.5f));

        using (ImRaii.Disabled(atCapacity || _form.IsStarting))
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, newLabel, "##gw_new_session"))
            {
                _form.StatusMessage = null;
                _showNewSessionForm = true;
            }
        }

        if (atCapacity && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip($"You can host up to {HostSessions.MaxConcurrent} sessions at once.");

        ImGui.SameLine();

        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.StopCircle, "Stop All", "##gw_stop_all"))
                ImGui.OpenPopup("##gw_stop_all_confirm");
        }

        DrawStopAllPopup();

        ImGuiHelpers.ScaledDummy(6f);

        const string syncNote = "Session data syncs to the server every 1 minute.";
        var noteWidth = ImGui.CalcTextSize(syncNote).X;
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - noteWidth) * 0.5f));
        ImGui.TextColored(Yellow, syncNote);
    }

    private void DrawStopAllPopup()
    {
        using var popup = ImRaii.Popup("##gw_stop_all_confirm");
        if (!popup.Success)
            return;

        ImGui.TextUnformatted("End every active session?");
        ImGui.Spacing();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Yes, stop them all", "##gw_stop_all_yes"))
        {
            TriggerStopAllSessions();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##gw_stop_all_no"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawActiveField(string label, string value, float labelX)
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), label);
        ImGui.SameLine(labelX);
        ImGui.TextUnformatted(value);
    }

    private void DrawActiveRules(SessionState session)
    {
        if (session.ActiveRules == null || session.ActiveRules.Count == 0)
            return;

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        const string heading = "Game Info";
        var headingWidth = ImGui.CalcTextSize(heading).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - headingWidth) * 0.5f));
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), heading);

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2f);

        DrawRuleKeyValues(session.ActiveRules);
    }

    private void DrawPartyFinderButtons(SessionState session)
    {
        const string partyLabel = "Create Party in Partyfinder";
        const string allianceLabel = "Create Alliance in Partyfinder";

        var gameType = session.GameType;
        var venueName = session.VenueName;
        var location = session.Location;

        var running = _partyFinderCreator.IsRunning;
        var eligible = _partyFinderCreator.CanCreate(out var ineligibleReason);
        var disabled = running || !eligible;

        var groupWidth = MeasureIconTextButtonWidth(FontAwesomeIcon.Users, partyLabel)
                         + MeasureIconTextButtonWidth(FontAwesomeIcon.PeopleGroup, allianceLabel)
                         + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX()
            + Math.Max(0f, (ImGui.GetContentRegionAvail().X - groupWidth) * 0.5f));

        using (ImRaii.Disabled(disabled))
        {
            using (UIHelper.PushBlueButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.Users, partyLabel, "##gw_pf_party"))
                    _partyFinderCreator.CreateParty(gameType, venueName, location);
            }
            DrawDisabledReasonTooltip(disabled, eligible, ineligibleReason);

            ImGui.SameLine();

            using (UIHelper.PushAmberButtonColours())
            {
                if (UIHelper.IconTextButton(FontAwesomeIcon.PeopleGroup, allianceLabel, "##gw_pf_alliance"))
                    _partyFinderCreator.CreateAlliance(gameType, venueName, location);
            }
            DrawDisabledReasonTooltip(disabled, eligible, ineligibleReason);
        }

        var status = _partyFinderCreator.StatusMessage;
        if (!string.IsNullOrEmpty(status))
        {
            ImGuiHelpers.ScaledDummy(2f);
            ImGui.TextWrapped(status);
        }
    }

    private static float MeasureIconTextButtonWidth(FontAwesomeIcon icon, string label)
    {
        var style = ImGui.GetStyle();

        ImGui.PushFont(UiBuilder.IconFont);
        var iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        ImGui.PopFont();

        var textWidth = ImGui.CalcTextSize(label).X;
        return style.FramePadding.X * 2 + iconWidth + style.ItemInnerSpacing.X + textWidth;
    }

    private static float MeasureIconButtonWidth(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        ImGui.PopFont();

        return ImGui.GetStyle().FramePadding.X * 2 + iconWidth;
    }

    private static void CentreRow(float rowWidth)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        if (rowWidth < avail)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - rowWidth) * 0.5f);
    }

    private static void DrawDisabledReasonTooltip(bool disabled, bool eligible, string reason)
    {
        if (!disabled || eligible || string.IsNullOrEmpty(reason))
            return;

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(reason);
    }

    private void DrawRuleKeyValues(Dictionary<string, object> rules)
    {
        using var table = ImRaii.Table("##gw_rule_kv", 2, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        ImGui.TableSetupColumn("##key", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##value", ImGuiTableColumnFlags.WidthStretch);

        foreach (var (key, value) in rules)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), FormatRuleKey(key) + ":");
            ImGui.TableNextColumn();
            ImGui.TextColored(White, FormatRuleValue(value, key));
        }
    }

    private static string FormatRuleKey(string camelCase) => RuleKeyFormatting.FormatDisplayKey(camelCase);

    private static string FormatRuleValue(object value, string key = "")
    {
        var isOdds = key.Contains("odds", StringComparison.OrdinalIgnoreCase);

        return value switch
        {
            bool b => b ? "Yes" : "No",
            int i => i.ToString("N0"),
            long l => l.ToString("N0"),
            float f => isOdds ? f.ToString("N2") + "x" : f.ToString("N0"),
            double d => isOdds ? d.ToString("N2") + "x" : d.ToString("N0"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private Dictionary<string, object> ResolveRulesSnapshot(out string? automaticSourceName)
    {
        automaticSourceName = null;

        if (_form.SelectedRuleSourceIndex > 0)
        {
            var sources = GetSources();
            var idx = _form.SelectedRuleSourceIndex - 1;
            if (idx >= 0 && idx < sources.Count)
            {
                automaticSourceName = sources[idx].Name;

                var autoRules = sources[idx].GetRules();
                return autoRules is { Count: > 0 }
                    ? new Dictionary<string, object>(autoRules)
                    : new Dictionary<string, object>();
            }
        }

        return PresetRules.Sendable(_form.RuleConfig?.ToApiPayload() ?? new());
    }

    private void ResolveRuleSource(out string? automaticSourceName, out string? presetName)
    {
        automaticSourceName = null;
        presetName = null;

        if (_form.SelectedRuleSourceIndex > 0)
        {
            var sources = GetSources();
            var index = _form.SelectedRuleSourceIndex - 1;
            if (index >= 0 && index < sources.Count)
            {
                automaticSourceName = sources[index].Name;
                return;
            }
        }

        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);
        if (_form.SelectedPresetIndex >= 0 && _form.SelectedPresetIndex < presets.Count)
            presetName = presets[_form.SelectedPresetIndex].Name;
    }

    private void DrawAddPresetInput(List<GamePreset> presets)
    {
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##NewPresetName", ref _newPresetNameBuffer, 64);
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Confirm", "##ConfirmAddPreset") && !string.IsNullOrWhiteSpace(_newPresetNameBuffer))
        {
            var preset = new GamePreset
            {
                Name = _newPresetNameBuffer.Trim(),
                RuleValues = _form.RuleConfig?.SaveToPreset() ?? new()
            };
            presets.Add(preset);
            _form.SelectedPresetIndex = presets.Count - 1;
            _config.Save();
            _showAddPresetInput = false;
        }
    }

    private void DrawRenamePresetInput(List<GamePreset> presets)
    {
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##RenamePresetInput", ref _renameBuffer, 64);
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Confirm", "##ConfirmRenamePreset") && !string.IsNullOrWhiteSpace(_renameBuffer))
        {
            presets[_form.SelectedPresetIndex].Name = _renameBuffer.Trim();
            _config.Save();
            _showRenamePresetInput = false;
        }
    }

    private void DrawImportPresetPopup(List<GamePreset> presets)
    {
        using var popup = ImRaii.Popup("ImportPresetPopup", ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Preset Name");
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##ImportName", ref _importNameBuffer, 30);

        ImGui.Spacing();
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Import Key");
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##ImportKey", ref _importKeyBuffer, 4096);

        if (!string.IsNullOrEmpty(_importError))
        {
            ImGui.Spacing();
            ImGui.TextColored(SoftRed, _importError);
        }

        ImGui.Spacing();
        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(_importNameBuffer) || string.IsNullOrWhiteSpace(_importKeyBuffer)))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.FileImport, "Import", "##ConfirmImport"))
                TryImportPreset(presets);
        }
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##ImportCancel"))
            ImGui.CloseCurrentPopup();
    }

    private void TryImportPreset(List<GamePreset> presets)
    {
        var name = _importNameBuffer.Trim();
        if (presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _importError = "A preset with that name already exists.";
            return;
        }

        if (!TryDecodePreset(_importKeyBuffer.Trim(), out var ruleValues, out var description))
        {
            _importError = "Invalid import key.";
            return;
        }

        var preset = new GamePreset { Name = name, RuleValues = ruleValues, Description = description };
        presets.Add(preset);
        _form.SelectedPresetIndex = presets.Count - 1;
        _config.Save();
        LoadSelectedPreset();
        ImGui.CloseCurrentPopup();
    }

    private void ExportCurrentPreset(List<GamePreset> presets)
    {
        if (_form.SelectedPresetIndex < 0 || _form.SelectedPresetIndex >= presets.Count)
            return;

        var preset = presets[_form.SelectedPresetIndex];
        var key = EncodePreset(preset.RuleValues, preset.Description);
        ImGui.SetClipboardText(key);
        _clipboardNotificationName = preset.Name;
        _clipboardNotificationUntil = DateTime.UtcNow.AddSeconds(3);
    }

    private static string EncodePreset(Dictionary<string, object> ruleValues, string description)
    {
        var payload = JsonSerializer.Serialize(new { r = ruleValues, d = description });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryDecodePreset(string key, out Dictionary<string, object> ruleValues, out string description)
    {
        ruleValues = new();
        description = string.Empty;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(key.Trim()));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            description = root.TryGetProperty("d", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            if (root.TryGetProperty("r", out var r) && r.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in r.EnumerateObject())
                    ruleValues[prop.Name] = prop.Value.Clone();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveCurrentPreset(List<GamePreset> presets)
    {
        if (_form.SelectedPresetIndex < 0 || _form.SelectedPresetIndex >= presets.Count)
            return;

        var preset = presets[_form.SelectedPresetIndex];
        preset.RuleValues = _form.RuleConfig?.SaveToPreset() ?? new();
        preset.Description = _form.Description;
        _config.Save();

        _saveNotificationName = preset.Name;
        _saveNotificationUntil = DateTime.UtcNow.AddSeconds(3);
    }

    private void DeleteCurrentPreset(List<GamePreset> presets)
    {
        if (presets.Count <= 1)
            return;

        presets.RemoveAt(_form.SelectedPresetIndex);
        _form.SelectedPresetIndex = Math.Max(0, _form.SelectedPresetIndex - 1);
        _config.Save();
        LoadSelectedPreset();
    }

    private void LoadSelectedPreset()
    {
        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);

        if (_form.SelectedPresetIndex < 0 || _form.SelectedPresetIndex >= presets.Count)
            return;

        var preset = presets[_form.SelectedPresetIndex];
        _form.RuleConfig?.LoadFromPreset(preset.RuleValues);
        _form.Description = preset.Description;
    }

    private List<GamePreset> GetOrInitPresets(string gameType)
    {
        if (!_config.Presets.TryGetValue(gameType, out var list) || list.Count == 0)
        {
            _config.EnsureDefaultPresets();
            _config.Save();
            list = _config.Presets[gameType];
        }
        return list;
    }

    private void TriggerStartSession()
    {
        if (!_playerInfo.IsLoggedIn)
        {
            _form.StatusMessage = "You must be logged in to start a session.";
            return;
        }

        if (_sessions.AtCapacity)
        {
            _form.StatusMessage = $"You are already hosting {HostSessions.MaxConcurrent} sessions.";
            return;
        }

        if (_form.Description.Length > 511)
        {
            _form.StatusMessage = "Description must be 511 characters or fewer.";
            return;
        }

        if (UserTextGuard.ContainsDisallowedContent(_form.Description))
        {
            _form.StatusMessage = "Description must not contain URLs or HTML.";
            return;
        }

        if (_form.AutoEndEnabled && _form.AutoEndHours == 0 && _form.AutoEndMinutes == 0)
        {
            _form.StatusMessage = "Auto end duration must be at least 1 minute.";
            return;
        }

        _form.StatusMessage = null;
        _form.IsStarting = true;

        var characterName = _playerInfo.GetCharacterName()!;
        var location = _playerInfo.GetCurrentLocation() ?? "Unknown";
        var gameType = GameTypes[_form.SelectedGameIndex];
        var venueName = _form.SelectedVenueName ?? "No Venue";
        var rulesSnapshot = ResolveRulesSnapshot(out var automaticSourceName);

        DateTime? autoEndAt = null;
        if (_form.AutoEndEnabled)
            autoEndAt = DateTime.UtcNow.AddHours(_form.AutoEndHours).AddMinutes(_form.AutoEndMinutes);

        var request = new PostEventRequest
        {
            CharacterName = characterName,
            Location = location,
            Game = gameType,
            Rules = rulesSnapshot,
            Description = _form.Description.Trim(),
            VenueName = venueName,
            BoosterKey = string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim()
        };

        var profile = GetSelectedProfile();
        var sentPictureHash = AttachProfile(request, profile);

        _ = Task.Run(async () =>
        {
            var (error, created) = await _sessionService.StartSessionAsync(request, autoEndAt, automaticSourceName);
            _form.IsStarting = false;

            if (error != null)
            {
                _form.StatusMessage = error;
                return;
            }

            _showNewSessionForm = false;

            if (profile != null && sentPictureHash != null && !string.IsNullOrEmpty(created?.ProfileImageUrl))
            {
                profile.UploadedImageUrl = created!.ProfileImageUrl;
                profile.UploadedImageHash = sentPictureHash;
                _config.Save();
            }
        });
    }

    private string? AttachProfile(PostEventRequest request, GambaProfile? profile)
    {
        if (profile == null)
            return null;

        request.Bio = string.IsNullOrWhiteSpace(profile.Bio) ? null : profile.Bio.Trim();
        request.PreferredGames = new List<string>(profile.PreferredGames);
        request.BorderStyle = profile.BorderStyle;
        request.CardEffectStyle = profile.CardEffectStyle;

        var path = _imageService.GetProfileImagePath(profile.ImageFileName);
        if (path == null || !_imageService.TryEncodeProfileImage(path, out var b64, out var hash))
            return null;

        if (!string.IsNullOrEmpty(profile.UploadedImageUrl) && profile.UploadedImageHash == hash)
        {
            request.ProfileImageUrl = profile.UploadedImageUrl;
            return null;
        }

        request.ProfilePictureB64 = b64;
        return hash;
    }

    private void TriggerStopSession(string eventId)
    {
        _stoppingEventId = eventId;
        _ = Task.Run(async () =>
        {
            await _sessionService.StopSessionAsync(eventId);
            _stoppingEventId = null;
            _form.StatusMessage = null;
        });
    }

    private void TriggerStopAllSessions()
    {
        _form.IsStarting = true;
        _ = Task.Run(async () =>
        {
            await _sessionService.StopAllSessionsAsync();
            _form.IsStarting = false;
            _form.StatusMessage = null;
            _showNewSessionForm = false;
        });
    }

    private void FetchVenues()
    {
        if (_isFetchingVenues)
            return;

        _isFetchingVenues = true;
        _ = Task.Run(async () =>
        {
            var venues = await _client.GetVenuesAsync();
            VenueSearchCombo.SetVenues(venues);
            _isFetchingVenues = false;
        });
    }
}
