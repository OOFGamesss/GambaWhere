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

namespace GambaWhere.UI.Tabs;

public class HostGambaTab
{
    private readonly SessionService _sessionService;
    private readonly PlayerInfoService _playerInfo;
    private readonly GambaWhereClient _client;
    private readonly SessionState _sessionState;
    private readonly Configuration _config;
    private readonly HostFormState _form;

    private static readonly string[] GameTypes = { "Bingo", "Blackjack", "Chocobo Racing", "Mini Games" };

    private readonly IRuleConfig[] _ruleConfigs;

    private volatile string[] _venueOptions = { "No Venue" };
    private bool _venuesFetched;
    private volatile bool _isFetchingVenues;

    private string _newPresetNameBuffer = string.Empty;
    private bool _showAddPresetInput;
    private bool _showRenamePresetInput;
    private string _renameBuffer = string.Empty;

    public HostGambaTab(
        SessionService sessionService,
        PlayerInfoService playerInfo,
        GambaWhereClient client,
        SessionState sessionState,
        Configuration config,
        HostFormState form)
    {
        _sessionService = sessionService;
        _playerInfo = playerInfo;
        _client = client;
        _sessionState = sessionState;
        _config = config;
        _form = form;

        _ruleConfigs = new IRuleConfig[]
        {
            new BingoRules(),
            new BlackjackRules(),
            new ChocoboRacingRules(),
            new MiniGamesRules()
        };

        _form.RuleConfig = _ruleConfigs[_form.SelectedGameIndex];
        LoadSelectedPreset();
    }

    public void SelectChocoboRacing()
    {
        var index = Array.IndexOf(GameTypes, "Chocobo Racing");
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
        ImGui.TextWrapped(_sessionState.Location);

        DrawActiveRules();

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.TextDisabled("Location updates automatically every 15 minutes.");
        ImGuiHelpers.ScaledDummy(8f);

        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.7f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Stop Session", new System.Numerics.Vector2(140, 0)))
            TriggerStopSession();
        ImGui.PopStyleColor();

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
        ImGui.TextDisabled("Rules");
        ImGuiHelpers.ScaledDummy(2f);

        var offset = CalcActiveRulesLabelOffset(_sessionState.ActiveRules);
        foreach (var (key, value) in _sessionState.ActiveRules)
        {
            ImGui.Text(FormatRuleKey(key) + ":");
            ImGui.SameLine(offset);
            ImGui.Text(FormatRuleValue(value));
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

    private static string FormatRuleValue(object value) => value switch
    {
        bool b => b ? "Yes" : "No",
        int i => i.ToString("N0"),
        float f => f.ToString("N2"),
        _ => value.ToString() ?? string.Empty
    };

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
        _form.RuleConfig?.Draw();
        ImGuiHelpers.ScaledDummy(8f);
        DrawDescriptionInput();
        ImGuiHelpers.ScaledDummy(8f);
        DrawStartButton();

        if (!string.IsNullOrEmpty(_form.StatusMessage))
        {
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _form.StatusMessage);
        }
    }

    private void DrawVenueDropdown(float labelOffset)
    {
        var options = _venueOptions;
        var venueIdx = _form.SelectedVenueIndex;
        ImGui.Text("Venue");
        ImGui.SameLine(labelOffset);
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##VenuePicker", ref venueIdx, options, options.Length))
            _form.SelectedVenueIndex = Math.Min(venueIdx, options.Length - 1);
        ImGui.SameLine();
        if (_isFetchingVenues)
            ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton("##RefreshVenues", FontAwesomeIcon.Sync))
            FetchVenues();
        if (_isFetchingVenues)
        {
            ImGui.EndDisabled();
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
    }

    private void DrawPresetBar(float labelOffset)
    {
        var gameType = GameTypes[_form.SelectedGameIndex];
        var presets = GetOrInitPresets(gameType);

        if (presets.Count == 0)
            return;

        var presetNames = presets.Select(p => p.Name).ToArray();
        var presetIdx = _form.SelectedPresetIndex;

        ImGui.Text("Preset");
        ImGui.SameLine(labelOffset);
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
        if (ImGui.SmallButton("Save"))
            SaveCurrentPreset(gameType, presets);

        ImGui.SameLine();
        if (ImGui.SmallButton("Add"))
        {
            _showAddPresetInput = !_showAddPresetInput;
            _showRenamePresetInput = false;
            _newPresetNameBuffer = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Rename"))
        {
            _showRenamePresetInput = !_showRenamePresetInput;
            _showAddPresetInput = false;
            _renameBuffer = presets[_form.SelectedPresetIndex].Name;
        }

        if (presets.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Delete"))
                DeleteCurrentPreset(gameType, presets);
        }

        if (_showAddPresetInput)
            DrawAddPresetInput(gameType, presets);

        if (_showRenamePresetInput)
            DrawRenamePresetInput(gameType, presets);
    }

    private void DrawAddPresetInput(string gameType, List<GamePreset> presets)
    {
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##NewPresetName", ref _newPresetNameBuffer, 64);
        ImGui.SameLine();
        if (ImGui.SmallButton("Confirm##AddPreset") && !string.IsNullOrWhiteSpace(_newPresetNameBuffer))
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
        if (ImGui.SmallButton("Confirm##RenamePreset") && !string.IsNullOrWhiteSpace(_renameBuffer))
        {
            presets[_form.SelectedPresetIndex].Name = _renameBuffer.Trim();
            _config.Save();
            _showRenamePresetInput = false;
        }
    }

    private void SaveCurrentPreset(string gameType, List<GamePreset> presets)
    {
        if (_form.SelectedPresetIndex < 0 || _form.SelectedPresetIndex >= presets.Count)
            return;

        var preset = presets[_form.SelectedPresetIndex];
        preset.RuleValues = _form.RuleConfig?.SaveToPreset() ?? new();
        preset.Description = _form.Description;
        _config.Save();
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
        ImGui.TextDisabled("No URLs or HTML permitted.");
    }

    private void DrawStartButton()
    {
        if (_form.IsStarting)
            ImGui.BeginDisabled();

        var label = _form.IsStarting ? "Starting..." : "Start Hosting";
        if (ImGui.Button(label, new System.Numerics.Vector2(160, 0)))
            TriggerStartSession();

        if (_form.IsStarting)
            ImGui.EndDisabled();
    }

    private void TriggerStartSession()
    {
        if (!_playerInfo.IsLoggedIn)
        {
            _form.StatusMessage = "You must be logged in to start a session.";
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
        var venueName = _venueOptions[_form.SelectedVenueIndex];
        var rulesSnapshot = _form.RuleConfig?.ToApiPayload() ?? new();

        var request = new PostEventRequest
        {
            CharacterName = characterName,
            Location = location,
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

            _sessionState.GameType = gameType;
            _sessionState.VenueName = venueName;
            _sessionState.ActiveRules = rulesSnapshot;
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
            var options = new string[venues.Length + 1];
            options[0] = "No Venue";
            Array.Copy(venues, 0, options, 1, venues.Length);
            _venueOptions = options;
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
