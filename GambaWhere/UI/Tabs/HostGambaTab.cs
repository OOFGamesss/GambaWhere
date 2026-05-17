using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.Rules;
using GambaWhere.Services;
using GambaWhere.State;
using GambaWhere.Utility;
using GambaWhere.Images;
using GambaWhere.UI.Components;

namespace GambaWhere.UI.Tabs;

public class HostGambaTab
{
    private readonly SessionService _sessionService;
    private readonly PlayerInfoService _playerInfo;
    private readonly GambaWhereClient _client;
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly HostFormState _form;
    private readonly ImageCache _imageCache;

    private static readonly string[] GameTypes = { "Bingo", "Blackjack", "Chocobo Racing", "Mini Games", "Poker", "Roulette", "Scratchcards", "Spin the Wheel" };

    private readonly IRuleConfig[] _ruleConfigs;

    private bool _venuesFetched;
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

    public Func<object?>? GetHostAutomaticRuleContext { get; set; }

    public HostGambaTab(
        SessionService sessionService,
        PlayerInfoService playerInfo,
        GambaWhereClient client,
        SessionState sessionState,
        Configuration config,
        HostFormState form,
        ImageCache imageCache)
    {
        _sessionService = sessionService;
        _playerInfo = playerInfo;
        _client = client;
        _sessionState = sessionState;
        _config = config;
        _form = form;
        _imageCache = imageCache;

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

    public void SelectChocoboRacing()
    {
        var index = Array.IndexOf(GameTypes, "Chocobo Racing");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectBingo()
    {
        var index = Array.IndexOf(GameTypes, "Bingo");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectMiniGames()
    {
        var index = Array.IndexOf(GameTypes, "Mini Games");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectBlackjack()
    {
        var index = Array.IndexOf(GameTypes, "Blackjack");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectPoker()
    {
        var index = Array.IndexOf(GameTypes, "Poker");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectRoulette()
    {
        var index = Array.IndexOf(GameTypes, "Roulette");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectScratchcards()
    {
        var index = Array.IndexOf(GameTypes, "Scratchcards");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void SelectSpinTheWheel()
    {
        var index = Array.IndexOf(GameTypes, "Spin the Wheel");
        if (index < 0 || index == _form.SelectedGameIndex)
            return;

        _form.SelectedGameIndex = index;
        OnGameTypeChanged();
    }

    public void Draw()
    {
        if (!_venuesFetched)
            FetchVenues();

        if (_sessionState.IsActive)
            DrawActiveSession();
        else
            DrawConfigForm();
    }

    private void DrawActiveSession()
    {
        ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), "Session Active");

        ImGui.SameLine(ImGui.GetContentRegionMax().X - UIHelper.CalcButtonSize(FontAwesomeIcon.Stop, "Stop Session").X);
        using var stopColours = UIHelper.PushRedButtonColours();
        using (ImRaii.Disabled(_form.IsStarting))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Stop, "Stop Session"))
                TriggerStopSession();
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        ImGui.Text("Character:");
        ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
        ImGui.Text(_sessionState.CharacterName);

        ImGui.Text("Game:");
        ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
        ImGui.Text(_sessionState.GameType);

        ImGui.Text("Venue:");
        ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
        ImGui.Text(_sessionState.VenueName ?? "No Venue");

        ImGui.Text("Location:");
        ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
        ImGui.TextWrapped(_sessionState.Location);

        DrawActiveRules();

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), "Location updates automatically every 1 minute.");

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _form.StatusMessage);
        }
    }

    private void DrawActiveRules()
    {
        if (_sessionState.ActiveRules == null || _sessionState.ActiveRules.Count == 0)
            return;

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        ImGui.TextDisabled("Game Info");
        ImGuiHelpers.ScaledDummy(2f);

        var offset = CalcActiveRulesLabelOffset(_sessionState.ActiveRules);
        foreach (var (key, value) in _sessionState.ActiveRules)
        {
            ImGui.Text(FormatRuleKey(key) + ":");
            ImGui.SameLine(offset);
            ImGui.Text(FormatRuleValue(value, key));
        }
    }

    private float CalcActiveRulesLabelOffset(System.Collections.Generic.Dictionary<string, object> rules)
    {
        var max = 0f;
        foreach (var key in rules.Keys)
        {
            var w = ImGui.CalcTextSize(FormatRuleKey(key) + ":").X;
            if (w > max) max = w;
        }
        return max + 16f * ImGuiHelpers.GlobalScale;
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

    private static float CalcLabelOffset(string[] labels)
    {
        var max = 0f;
        foreach (var label in labels)
        {
            var w = ImGui.CalcTextSize(label).X;
            if (w > max) max = w;
        }
        return max + 16f * ImGuiHelpers.GlobalScale;
    }

    private void DrawConfigForm()
    {
        var topOffset = CalcLabelOffset(new[] { "Venue", "Game", "Preset" });
        DrawVenueDropdown(topOffset);
        ImGuiHelpers.ScaledDummy(4f);
        DrawGameDropdown(topOffset);
        ImGuiHelpers.ScaledDummy(4f);
        DrawPresetBar(topOffset);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (_form.RuleConfig is IAutomaticHostRuleSource automatic
            && GetHostAutomaticRuleContext != null)
        {
            var ipcContext = GetHostAutomaticRuleContext();
            ManualVsAutomaticHostRulesDraw.Draw(_form, _form.RuleConfig, automatic, ipcContext, _config.PrimaryColour, _config.SecondaryColour);
        }
        else
        {
            _form.RuleConfig?.Draw();
        }

        ImGuiHelpers.ScaledDummy(8f);
        DrawDescriptionInput();

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _form.StatusMessage);
        }
    }

    private void DrawVenueDropdown(float labelOffset)
    {
        ImGui.Text("Venue");
        ImGui.SameLine(labelOffset);
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        var venueName = _form.SelectedVenueName;
        if (VenueSearchCombo.Draw("##VenuePicker", ref venueName, _config.FavouriteVenues, () => _config.Save()))
            _form.SelectedVenueName = venueName;
        ImGui.SameLine();
        var fetching = _isFetchingVenues;
        using (ImRaii.Disabled(fetching))
        {
            if (ImGuiComponents.IconButton("##RefreshVenues", FontAwesomeIcon.Sync))
                FetchVenues();
        }
        if (fetching)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Fetching latest venues...");
        }
    }

    private void DrawGameDropdown(float labelOffset)
    {
        var gameIdx = _form.SelectedGameIndex;
        ImGui.Text("Game");
        ImGui.SameLine(labelOffset);
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##GamePicker", ref gameIdx, GameTypes, GameTypes.Length))
        {
            if (gameIdx != _form.SelectedGameIndex)
            {
                _form.SelectedGameIndex = gameIdx;
                OnGameTypeChanged();
            }
        }
    }

    private void OnGameTypeChanged()
    {
        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        _form.SelectedPresetIndex = 0;
        LoadSelectedPreset();
        _showAddPresetInput = false;
        _showRenamePresetInput = false;
        if (_form.RuleConfig is not IAutomaticHostRuleSource)
            _form.UseManualHostRules = false;
    }

    private void DrawPresetBar(float labelOffset)
    {
        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);

        if (presets.Count == 0)
            return;

        var presetNames = presets.Select(p => p.Name).ToArray();
        var presetIdx = _form.SelectedPresetIndex;

        var startY       = ImGui.GetCursorPosY();
        var textLineH    = ImGui.GetTextLineHeight();
        var frameH       = ImGui.GetFrameHeight();
        var itemSpacingY = ImGui.GetStyle().ItemSpacing.Y;

        ImGui.SetCursorPosX(labelOffset);
        if (UIHelper.IconTextButton(FontAwesomeIcon.FileImport, "Import Preset", "##ImportPresetBtn"))
        {
            _importNameBuffer = string.Empty;
            _importKeyBuffer = string.Empty;
            _importError = string.Empty;
            ImGui.OpenPopup("ImportPresetPopup");
        }
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Copy, "Export to Clipboard", "##ExportToClipboard"))
            ExportCurrentPreset(presets);
        if (DateTime.UtcNow < _clipboardNotificationUntil)
        {
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), $"{_clipboardNotificationName} copied to clipboard!");
        }

        ImGui.SetCursorPosX(labelOffset);
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##PresetPicker", ref presetIdx, presetNames, presetNames.Length))
        {
            if (presetIdx != _form.SelectedPresetIndex)
            {
                _form.SelectedPresetIndex = presetIdx;
                LoadSelectedPreset();
            }
        }

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Save, "Save", "##SavePreset"))
            SaveCurrentPreset(gameType, presets);

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, "Add", "##AddPreset"))
        {
            _showAddPresetInput = !_showAddPresetInput;
            _showRenamePresetInput = false;
            _newPresetNameBuffer = string.Empty;
        }

        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Pen, "Rename", "##RenamePreset"))
        {
            _showRenamePresetInput = !_showRenamePresetInput;
            _showAddPresetInput = false;
            _renameBuffer = presets[_form.SelectedPresetIndex].Name;
        }

        if (presets.Count > 1)
        {
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Delete", "##DeletePreset"))
                DeleteCurrentPreset(gameType, presets);
        }

        if (DateTime.UtcNow < _saveNotificationUntil)
        {
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), $"{_saveNotificationName} saved!");
        }

        var startLabel = _form.IsStarting ? "Starting..." : "Start Hosting";
        var startBtnWidth = UIHelper.CalcButtonSize(FontAwesomeIcon.Play, startLabel).X;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - startBtnWidth);
        using var startColours = UIHelper.PushGreenButtonColours();
        using (ImRaii.Disabled(_form.IsStarting))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Play, startLabel, "##StartHosting"))
                TriggerStartSession();
        }

        var afterCursor = ImGui.GetCursorPos();
        var labelY = startY + (textLineH + itemSpacingY + frameH) / 2f - textLineH / 2f;
        ImGui.SetCursorPos(new System.Numerics.Vector2(0, labelY));
        ImGui.Text("Preset");
        ImGui.SetCursorPos(afterCursor);

        if (_showAddPresetInput)
            DrawAddPresetInput(gameType, presets);

        if (_showRenamePresetInput)
            DrawRenamePresetInput(gameType, presets);

        DrawImportPresetPopup(gameType, presets);
    }

    private void DrawAddPresetInput(string gameType, List<GamePreset> presets)
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

    private void DrawRenamePresetInput(string gameType, List<GamePreset> presets)
    {
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##RenamePreset", ref _renameBuffer, 64);
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Confirm", "##ConfirmRenamePreset") && !string.IsNullOrWhiteSpace(_renameBuffer))
        {
            presets[_form.SelectedPresetIndex].Name = _renameBuffer.Trim();
            _config.Save();
            _showRenamePresetInput = false;
        }
    }

    private void DrawImportPresetPopup(string gameType, List<GamePreset> presets)
    {
        using var popup = ImRaii.Popup("ImportPresetPopup", ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success)
            return;

        ImGui.Text("Preset Name");
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##ImportName", ref _importNameBuffer, 30);

        ImGui.Spacing();
        ImGui.Text("Import Key");
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##ImportKey", ref _importKeyBuffer, 4096);

        if (!string.IsNullOrEmpty(_importError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _importError);
        }

        ImGui.Spacing();
        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(_importNameBuffer) || string.IsNullOrWhiteSpace(_importKeyBuffer)))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.FileImport, "Import", "##ConfirmImport"))
                TryImportPreset(gameType, presets);
        }
        ImGui.SameLine();
        if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##ImportCancel"))
            ImGui.CloseCurrentPopup();
    }

    private void TryImportPreset(string gameType, List<GamePreset> presets)
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

    private void SaveCurrentPreset(string gameType, List<GamePreset> presets)
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

    private void DeleteCurrentPreset(string gameType, List<GamePreset> presets)
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

    private void DrawDescriptionInput()
    {
        ImGui.Text("Description");
        ImGui.SetNextItemWidth(-1);
        var desc = _form.Description;
        if (ImGui.InputTextMultiline("##Description", ref desc, 512,
            new System.Numerics.Vector2(0, 60 * ImGuiHelpers.GlobalScale)))
            _form.Description = desc;

        var charCount = _form.Description.Length;
        var overLimit = charCount >= 511;
        var countColor = overLimit
            ? new System.Numerics.Vector4(1f, 0.2f, 0.2f, 1f)
            : new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f);
        ImGui.TextColored(countColor, $"{charCount} / 511");
        if (overLimit)
        {
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.2f, 0.2f, 1f), "- Description is too long!");
        }

        ImGui.TextDisabled("No URLs or HTML permitted.");
        ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), "Please use the rules provided above to describe your session. I will add any rules to the plugin, just let me know!");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        if (ImGui.CollapsingHeader("Preview"))
        {
            ImGuiHelpers.ScaledDummy(4f);
            DrawPreviewSection();
        }
    }

    private Dictionary<string, object> BuildPreviewRules()
    {
        if (_form.RuleConfig is IAutomaticHostRuleSource automatic
            && GetHostAutomaticRuleContext != null
            && !_form.UseManualHostRules
            && automatic.TryGetAutomaticApiRules(GetHostAutomaticRuleContext(), out var autoRules)
            && autoRules != null)
        {
            return new Dictionary<string, object>(autoRules);
        }

        return ManualRulesApiOmitter.OmitEmptyOrDefault(_form.RuleConfig?.ToApiPayload() ?? new());
    }

    private void DrawPreviewSection()
    {
        var characterName = _playerInfo.IsLoggedIn
            ? EventCardRenderer.FormatDisplayName(_playerInfo.GetCharacterName() ?? "Your Character")
            : "Your Character";

        var gameType = GameTypes[_form.SelectedGameIndex];
        var venueName = _form.SelectedVenueName ?? "No Venue";
        var rules = BuildPreviewRules();

        EventCardRenderer.DrawPreviewCard(characterName, gameType, venueName, _form.Description, rules, _imageCache);

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.TextColored(
            new System.Numerics.Vector4(1f, 1f, 0f, 1f),
            "Venue image and Discord will load upon posting. Use this preview to see how your rules and description will look.");
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

        if (ContainsDisallowedContent(_form.Description))
        {
            _form.StatusMessage = "Description must not contain URLs or HTML.";
            return;
        }

        _form.StatusMessage = null;
        _form.IsStarting = true;

        var characterName = _playerInfo.GetCharacterName()!;
        var location = _playerInfo.GetCurrentLocation() ?? "Unknown";
        var gameType = GameTypes[_form.SelectedGameIndex];
        var venueName = _form.SelectedVenueName ?? "No Venue";
        var rulesSnapshot = _form.RuleConfig?.ToApiPayload() ?? new();
        var usedAutomaticIpc = false;

        if (_form.RuleConfig is IAutomaticHostRuleSource automatic
            && GetHostAutomaticRuleContext != null
            && !_form.UseManualHostRules
            && automatic.TryGetAutomaticApiRules(GetHostAutomaticRuleContext(), out var autoRules)
            && autoRules != null)
        {
            rulesSnapshot = new Dictionary<string, object>(autoRules);
            usedAutomaticIpc = true;
        }
        else
        {
            rulesSnapshot = ManualRulesApiOmitter.OmitEmptyOrDefault(rulesSnapshot);
        }

        var request = new PostEventRequest
        {
            CharacterName = characterName,
            Location = location,
            Game = gameType,
            Rules = rulesSnapshot,
            Description = _form.Description.Trim(),
            VenueName = venueName
        };

        _ = Task.Run(async () =>
        {
            var error = await _sessionService.StartSessionAsync(request);
            _form.IsStarting = false;

            if (error != null)
            {
                _form.StatusMessage = error;
                return;
            }

            _sessionState.UsesAutomaticHostRules = usedAutomaticIpc;
        });
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
        _venuesFetched = true;
        _isFetchingVenues = true;
        _ = Task.Run(async () =>
        {
            var venues = await _client.GetVenuesAsync();
            VenueSearchCombo.SetVenues(venues);
            _isFetchingVenues = false;
        });
    }

    private static readonly string[] DisallowedPatterns =
    {
        "http", "https://", "www.", ".com", ".gg", ".net", ".org", ".io", ".tv", "<", ">"
    };

    private static bool ContainsDisallowedContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var pattern in DisallowedPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
