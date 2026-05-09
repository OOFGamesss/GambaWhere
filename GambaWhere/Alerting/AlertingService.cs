using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.UI.Tabs;

namespace GambaWhere.Alerting;

public sealed class AlertingService : IDisposable
{
    private const uint LinkId = 8;
    private const ushort AlertColour = 25;
    private const string CharacterMarker = " by ";
    private const string ChatSourceTag = "GambaWhere";

    private readonly Configuration _config;
    private readonly IChatGui _chatGui;
    private readonly IToastGui _toasts;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly Action<string> _onLinkClicked;

    private readonly DalamudLinkPayload _linkPayload;

    private readonly HashSet<string> _alertedCharacterNames = new();
    private bool _initialised;

    public AlertingService(
        Configuration config,
        IChatGui chatGui,
        IToastGui toasts,
        IFramework framework,
        IPluginLog log,
        Action<string> onLinkClicked)
    {
        _config = config;
        _chatGui = chatGui;
        _toasts = toasts;
        _framework = framework;
        _log = log;
        _onLinkClicked = onLinkClicked;

        _linkPayload = _chatGui.AddChatLinkHandler(LinkId, OnLinkClicked);
    }

    public void OnEventsRefreshed(IReadOnlyList<EventResponse> events)
    {
        _ = _framework.RunOnFrameworkThread(() => Process(events));
    }

    private void Process(IReadOnlyList<EventResponse> events)
    {
        var matches = new List<(AlertRule rule, EventResponse ev)>();
        var currentNames = events.Select(e => e.CharacterName).ToHashSet();
        _alertedCharacterNames.IntersectWith(currentNames);

        var ownSessionCharacter = _config.ActiveCharacterName;

        foreach (var ev in events)
        {
            if (_alertedCharacterNames.Contains(ev.CharacterName))
                continue;

            if (!string.IsNullOrEmpty(ownSessionCharacter) && ev.CharacterName == ownSessionCharacter)
                continue;

            foreach (var rule in _config.Alerts)
            {
                if (!rule.Enabled || rule.IsInert)
                    continue;

                if (!Matches(rule, ev))
                    continue;

                matches.Add((rule, ev));
                _alertedCharacterNames.Add(ev.CharacterName);
                break;
            }
        }

        if (!_initialised)
        {
            _initialised = true;
            if (matches.Count > 0)
                _log.Verbose("[GambaWhere/Alerts] First refresh seeded dedup state; suppressing initial alerts.");
            return;
        }

        foreach (var (rule, ev) in matches)
        {
            EmitChatAlert(rule, ev);

            if (_config.AlertToastEnabled)
                EmitQuestToast(ev);

            if (_config.AlertSoundEnabled)
                PlaySoundEffect(Math.Clamp(_config.AlertSoundEffectId, 1, 16));
        }
    }

    private void EmitChatAlert(AlertRule rule, EventResponse ev)
    {
        var ruleNamePart = string.IsNullOrEmpty(rule.Name) ? string.Empty : $" ({rule.Name})";
        var venueOrLoc = !string.IsNullOrEmpty(ev.VenueName) && ev.VenueName != "No Venue"
            ? ev.VenueName
            : ev.Location;

        var msg = new SeStringBuilder()
            .AddUiForeground(AlertColour)
            .Add(_linkPayload)
            .AddText($"Alert{ruleNamePart}: {ev.Game} at {venueOrLoc}{CharacterMarker}{ev.CharacterName}")
            .Add(RawPayload.LinkTerminator)
            .AddUiForegroundOff()
            .Build();

        _chatGui.Print(msg, ChatSourceTag);
    }

    private void EmitQuestToast(EventResponse ev)
    {
        var text = $"GambaWhere Alert: {ev.Game}";
        try
        {
            _toasts.ShowQuest(text);
            _log.Verbose("[GambaWhere/Alerts] Quest toast issued: {Text}", text);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[GambaWhere/Alerts] Quest toast failed for {Text}; falling back to ShowNormal", text);
            try
            {
                _toasts.ShowNormal(text);
            }
            catch (Exception fallbackEx)
            {
                _log.Warning(fallbackEx, "[GambaWhere/Alerts] Normal toast also failed for {Text}", text);
            }
        }
    }

    private void PlaySoundEffect(int id)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)id);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PlayChatSoundEffect failed for SE {Id}", id);
        }
    }

    private void OnLinkClicked(uint id, SeString message)
    {
        var linkText = message.TextValue;
        if (string.IsNullOrEmpty(linkText))
            return;

        var idx = linkText.LastIndexOf(CharacterMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            _log.Warning("[GambaWhere/Alerts] Link text missing character marker: '{Text}'", linkText);
            return;
        }

        var characterName = linkText[(idx + CharacterMarker.Length)..].Trim();
        if (string.IsNullOrEmpty(characterName))
            return;

        _onLinkClicked.Invoke(characterName);
    }

    private static bool Matches(AlertRule rule, EventResponse ev)
    {
        if (rule.GameTypes.Count > 0 && !rule.GameTypes.Contains(ev.Game))
            return false;

        if (rule.DataCentres.Count > 0)
        {
            var dc = !string.IsNullOrEmpty(ev.DataCentre)
                ? ev.DataCentre
                : GambaEventsTab.InferDataCentre(ev.Location);
            if (!rule.DataCentres.Contains(dc))
                return false;
        }

        if (rule.VenueNames.Count > 0)
        {
            if (string.IsNullOrEmpty(ev.VenueName) || ev.VenueName == "No Venue" || !rule.VenueNames.Contains(ev.VenueName))
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        _chatGui.RemoveChatLinkHandler(LinkId);
    }
}
