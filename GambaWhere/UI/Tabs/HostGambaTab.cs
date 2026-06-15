using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Rules;
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
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly HostFormState _form;
    private readonly ImageCache _imageCache;
    private readonly ProfileImageStore _profileImages;

    private static readonly string[] GameTypes = { "Bingo", "Blackjack", "Chocobo Racing", "Mini Games", "Poker", "Roulette", "Scratchcards", "Spin the Wheel" };

    private static readonly Vector4 Yellow = new(1f, 1f, 0f, 1f);
    private static readonly Vector4 SoftRed = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly IRuleConfig[] _ruleConfigs;

    private int _lastDrawFrame = -10;
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

    private readonly ThemedCard _card = new();

    public Func<IReadOnlyList<HostRuleSource>>? GetHostAutomaticRuleSources { get; set; }

    public HostGambaTab(
        SessionService sessionService,
        PlayerInfoService playerInfo,
        GambaWhereClient client,
        SessionState sessionState,
        Configuration config,
        HostFormState form,
        ImageCache imageCache,
        ProfileImageStore profileImages)
    {
        _sessionService = sessionService;
        _playerInfo = playerInfo;
        _client = client;
        _sessionState = sessionState;
        _config = config;
        _form = form;
        _imageCache = imageCache;
        _profileImages = profileImages;

        _ruleConfigs = new IRuleConfig[]
        {
            new BingoRules(),
            new BlackjackRules(),
            new ChocoboRacingRules(),
            new MiniGamesRules(),
            new PokerRules(),
            new RouletteRules(),
            new ScratchcardsRules(),
            new SpinTheWheelRules()
        };

        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        LoadSelectedPreset();
    }

    public string GetSelectedGameType() => GameTypes[_form.SelectedGameIndex];

    public void SelectChocoboRacing() => SelectGame("Chocobo Racing");

    public void SelectBingo() => SelectGame("Bingo");

    public void SelectMiniGames() => SelectGame("Mini Games");

    public void SelectBlackjack() => SelectGame("Blackjack");

    public void SelectPoker() => SelectGame("Poker");

    public void SelectRoulette() => SelectGame("Roulette");

    public void SelectScratchcards() => SelectGame("Scratchcards");

    public void SelectSpinTheWheel() => SelectGame("Spin the Wheel");

    private void SelectGame(string gameType)
    {
        var index = Array.IndexOf(GameTypes, gameType);
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void Draw()
    {
        var frame = ImGui.GetFrameCount();
        if (frame - _lastDrawFrame > 1)
            FetchVenues();
        _lastDrawFrame = frame;

        HostFieldTheme.Primary = _config.PrimaryColour;
        HostFieldTheme.Secondary = _config.SecondaryColour;

        var scale = ImGuiHelpers.GlobalScale;
        var footerHeight = 76f * scale;
        var scrollHeight = Math.Max(80f * scale, ImGui.GetContentRegionAvail().Y - footerHeight);

        using (var scroll = ImRaii.Child("##gw_host_scroll", new Vector2(0f, scrollHeight), false))
        {
            if (scroll.Success)
            {
                if (_sessionState.IsActive)
                {
                    ImGuiHelpers.ScaledDummy(8f);
                    DrawCard("##gw_active_session", "Active Session", DrawActiveSessionBody);
                }
                else
                {
                    DrawConfigForm();
                }
            }
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (_sessionState.IsActive)
            DrawStopBar();
        else
            DrawBottomBar();
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
        DrawVenueGameRow();
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
        DrawProfileRow();
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
        DrawPresetRow();
    }

    private GambaProfile? GetSelectedProfile() =>
        _config.Profiles.FirstOrDefault(p => p.Id == _config.SelectedProfileId);

    private void DrawProfileRow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var diameter = 56f * scale;
        var selected = GetSelectedProfile();

        var path = selected != null ? _profileImages.GetPath(selected.ImageFileName) : null;
        var tex = path != null ? _imageCache.GetFromPath(path) : null;

        var startY = ImGui.GetCursorPosY();
        CircleImage.DrawInline(diameter, tex);
        ImGui.SameLine();

        var groupHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight();
        ImGui.SetCursorPosY(startY + Math.Max(0f, (diameter - groupHeight) * 0.5f));

        using (ImRaii.Group())
        {
            HostField.Label("Profile");
            ImGui.SetNextItemWidth(200f * scale);
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
            ImGui.TextDisabled("Create one on the Profiles tab.");
    }

    private void DrawRulesCardBody() => DrawRulesSection();

    private void DrawPresetRow()
    {
        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);
        if (presets.Count == 0)
            return;

        var presetNames = presets.Select(p => p.Name).ToArray();
        var presetIdx = _form.SelectedPresetIndex;

        ImGui.AlignTextToFramePadding();
        HostField.Label("Presets");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
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

        var buttons = new List<(FontAwesomeIcon Icon, string Label, string Id, Func<ImRaii.ColorDisposable> Colours, Action OnClick)>
        {
            (FontAwesomeIcon.FileImport, "Import", "##ImportPresetBtn", UIHelper.PushGreenButtonColours, () =>
            {
                _importNameBuffer = string.Empty;
                _importKeyBuffer = string.Empty;
                _importError = string.Empty;
                ImGui.OpenPopup("ImportPresetPopup");
            }),
            (FontAwesomeIcon.Copy, "Export", "##ExportToClipboard", UIHelper.PushRedButtonColours, () => ExportCurrentPreset(presets)),
            (FontAwesomeIcon.Save, "Save", "##SavePreset", UIHelper.PushBlueButtonColours, () => SaveCurrentPreset(presets)),
            (FontAwesomeIcon.Plus, "Add", "##AddPreset", UIHelper.PushGreenButtonColours, () =>
            {
                _showAddPresetInput = !_showAddPresetInput;
                _showRenamePresetInput = false;
                _newPresetNameBuffer = string.Empty;
            }),
            (FontAwesomeIcon.Pen, "Rename", "##RenamePreset", UIHelper.PushAmberButtonColours, () =>
            {
                _showRenamePresetInput = !_showRenamePresetInput;
                _showAddPresetInput = false;
                _renameBuffer = presets[_form.SelectedPresetIndex].Name;
            }),
        };

        if (presets.Count > 1)
            buttons.Add((FontAwesomeIcon.Trash, "Delete", "##DeletePreset", UIHelper.PushRedButtonColours, () => DeleteCurrentPreset(presets)));

        var rightLimit = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        foreach (var b in buttons)
        {
            FitSameLine(UIHelper.CalcButtonSize(b.Icon, b.Label).X, rightLimit, spacing);
            using (b.Colours())
            {
                if (UIHelper.IconTextButton(b.Icon, b.Label, b.Id))
                    b.OnClick();
            }
        }

        if (DateTime.UtcNow < _saveNotificationUntil)
        {
            var msg = $"{_saveNotificationName} saved!";
            FitSameLine(ImGui.CalcTextSize(msg).X, rightLimit, spacing);
            ImGui.TextColored(Yellow, msg);
        }

        if (DateTime.UtcNow < _clipboardNotificationUntil)
        {
            var msg = $"{_clipboardNotificationName} copied to clipboard!";
            FitSameLine(ImGui.CalcTextSize(msg).X, rightLimit, spacing);
            ImGui.TextColored(Yellow, msg);
        }

        if (_showAddPresetInput)
            DrawAddPresetInput(presets);

        if (_showRenamePresetInput)
            DrawRenamePresetInput(presets);

        DrawImportPresetPopup(presets);
    }

    private static void FitSameLine(float nextWidth, float rightLimit, float spacing)
    {
        if (ImGui.GetItemRectMax().X + spacing + nextWidth <= rightLimit)
            ImGui.SameLine();
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

        if (_isFetchingVenues)
            ImGui.TextDisabled("Fetching latest venues...");
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
        if (_form.RuleConfig is IAutomaticHostRuleSource automatic && sources.Count > 0)
            DrawRulesSourceSelector(automatic, sources);
        else
            _form.RuleConfig?.Draw();
    }

    private void DrawRulesSourceSelector(IAutomaticHostRuleSource automatic, IReadOnlyList<HostRuleSource> sources)
    {
        var segments = new[] { "Manual" }.Concat(sources.Select(s => s.Name)).ToArray();
        if (_form.SelectedRuleSourceIndex < 0 || _form.SelectedRuleSourceIndex >= segments.Length)
            _form.SelectedRuleSourceIndex = 0;

        HostField.Label("Rules Source");
        _form.SelectedRuleSourceIndex = DrawSegmentedBar("##gw_rules_source", segments, _form.SelectedRuleSourceIndex, _config.SecondaryColour);
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
        var context = source.GetContext();

        if (context == null
            || !automatic.TryGetAutomaticApiRules(context, out var liveRules)
            || liveRules == null
            || liveRules.Count == 0)
        {
            ImGui.TextDisabled("No Session Found");
            return;
        }

        DrawRuleKeyValues(liveRules);
    }

    private static int DrawSegmentedBar(string id, string[] segments, int selected, Vector4 accent)
    {
        var result = selected;
        var scale = ImGuiHelpers.GlobalScale;

        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f * scale);

        for (var i = 0; i < segments.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, 4f * scale);

            var isSelected = i == selected;
            var baseCol = new Vector4(accent.X, accent.Y, accent.Z, isSelected ? 0.55f : 0.14f);
            var hovCol = new Vector4(accent.X, accent.Y, accent.Z, isSelected ? 0.66f : 0.30f);
            var actCol = new Vector4(accent.X, accent.Y, accent.Z, 0.75f);

            using var colours = ImRaii.PushColor(ImGuiCol.Button, baseCol)
                .Push(ImGuiCol.ButtonHovered, hovCol)
                .Push(ImGuiCol.ButtonActive, actCol);

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
        _form.SelectedPresetIndex = 0;
        _form.SelectedRuleSourceIndex = 0;
        LoadSelectedPreset();
        _showAddPresetInput = false;
        _showRenamePresetInput = false;
        if (_form.RuleConfig is not IAutomaticHostRuleSource)
            _form.UseManualHostRules = false;
    }

    private void DrawSessionDetailsBody()
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

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        HostField.Label("Current Location");
        ImGui.TextWrapped(_playerInfo.GetCurrentLocation() ?? "Unknown");

        ImGuiHelpers.ScaledDummy(8f);
        DrawAutoEndControl();
    }

    private void DrawBottomBar()
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

        var startLabel = _form.IsStarting ? "Starting..." : "Start Session";
        var btnSize = new Vector2(240f * scale, 46f * scale);
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - btnSize.X) * 0.5f));
        using (UIHelper.PushGreenButtonColours())
        using (ImRaii.Disabled(_form.IsStarting))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, startLabel, btnSize, "##StartHosting"))
                TriggerStartSession();
        }
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

    private void DrawActiveSessionBody()
    {
        var labelX = 120 * ImGuiHelpers.GlobalScale;

        DrawActiveField("Character:", _sessionState.CharacterName, labelX);
        DrawActiveField("Game:", _sessionState.GameType, labelX);
        DrawActiveField("Venue:", _sessionState.VenueName ?? "No Venue", labelX);

        if (_sessionState.AutoEndAt.HasValue)
        {
            ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Auto End:");
            ImGui.SameLine(labelX);
            var remaining = _sessionState.AutoEndAt.Value - DateTime.UtcNow;
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
        ImGui.TextWrapped(_sessionState.Location);

        DrawActiveRules();

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.TextColored(Yellow, "Location updates automatically every 1 minute.");

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(SoftRed, _form.StatusMessage);
        }
    }

    private void DrawStopBar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;

        var stopLabel = _form.IsStarting ? "Stopping..." : "Stop Session";
        var btnSize = new Vector2(240f * scale, 46f * scale);
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - btnSize.X) * 0.5f));
        using (UIHelper.PushRedButtonColours())
        using (ImRaii.Disabled(_form.IsStarting))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, stopLabel, btnSize, "##StopHosting"))
                TriggerStopSession();
        }
    }

    private void DrawActiveField(string label, string value, float labelX)
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), label);
        ImGui.SameLine(labelX);
        ImGui.TextUnformatted(value);
    }

    private void DrawActiveRules()
    {
        if (_sessionState.ActiveRules == null || _sessionState.ActiveRules.Count == 0)
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

        DrawRuleKeyValues(_sessionState.ActiveRules);
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

    private Dictionary<string, object> ResolveRulesSnapshot(out bool usedAutomatic)
    {
        usedAutomatic = false;
        var manual = _form.RuleConfig?.ToApiPayload() ?? new();

        if (_form.RuleConfig is IAutomaticHostRuleSource automatic && _form.SelectedRuleSourceIndex > 0)
        {
            var sources = GetSources();
            var idx = _form.SelectedRuleSourceIndex - 1;
            if (idx >= 0 && idx < sources.Count)
            {
                var context = sources[idx].GetContext();
                if (context != null
                    && automatic.TryGetAutomaticApiRules(context, out var autoRules)
                    && autoRules != null)
                {
                    usedAutomatic = true;
                    return new Dictionary<string, object>(autoRules);
                }
            }
        }

        return ManualRulesApiOmitter.OmitEmptyOrDefault(manual);
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

        if (!PresetCodec.TryDecode(_importKeyBuffer.Trim(), out var ruleValues, out var description))
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
        var key = PresetCodec.Encode(preset.RuleValues, preset.Description);
        ImGui.SetClipboardText(key);
        _clipboardNotificationName = preset.Name;
        _clipboardNotificationUntil = DateTime.UtcNow.AddSeconds(3);
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
        var rulesSnapshot = ResolveRulesSnapshot(out var usedAutomaticIpc);

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
            var (error, created) = await _sessionService.StartSessionAsync(request, autoEndAt);
            _form.IsStarting = false;

            if (error != null)
            {
                _form.StatusMessage = error;
                return;
            }

            _sessionState.UsesAutomaticHostRules = usedAutomaticIpc;

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

        var path = _profileImages.GetPath(profile.ImageFileName);
        if (path == null || !ProfileImageEncoder.TryEncode(path, out var b64, out var hash))
            return null;

        if (!string.IsNullOrEmpty(profile.UploadedImageUrl) && profile.UploadedImageHash == hash)
        {
            request.ProfileImageUrl = profile.UploadedImageUrl;
            return null;
        }

        request.ProfilePictureB64 = b64;
        return hash;
    }

    private void TriggerStopSession()
    {
        _form.IsStarting = true;
        _ = Task.Run(async () =>
        {
            await _sessionService.StopSessionAsync();
            _form.IsStarting = false;
            _form.StatusMessage = null;
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
