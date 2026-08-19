using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Games;
using GambaWhere.Models;

namespace GambaWhere.Services;

/// <summary>Owns the local player's scheduled sessions: creating them, keeping the saved copy in step with the server, and turning one into a live session or retiring it.</summary>
public sealed class ScheduledSessionService : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private const int MissesBeforeForgetting = 3;

    private readonly GambaWhereClient _client;
    private readonly SessionService _sessionService;
    private readonly PlayerInfoService _playerInfo;
    private readonly ImageService _imageService;
    private readonly Configuration _config;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _saveGate = new();
    private readonly Dictionary<string, int> _missedRefreshes = new(StringComparer.Ordinal);

    private volatile bool _isRefreshing;
    private DateTime _nextRefreshUtc = DateTime.MinValue;

    public ScheduledSessionService(
        GambaWhereClient client,
        SessionService sessionService,
        PlayerInfoService playerInfo,
        ImageService imageService,
        Configuration config,
        IFramework framework,
        IPluginLog log)
    {
        _client = client;
        _sessionService = sessionService;
        _playerInfo = playerInfo;
        _imageService = imageService;
        _config = config;
        _framework = framework;
        _log = log;
    }

    public Func<string, string, Dictionary<string, object>?>? ResolveAutomaticRules { get; set; }

    public IReadOnlyList<ScheduledSessionSnapshot> Snapshot()
    {
        lock (_saveGate)
            return _config.ScheduledSessions.ToArray();
    }

    public IReadOnlyList<ScheduledSessionSnapshot> Due()
    {
        var characterName = _playerInfo.GetCharacterName();
        if (string.IsNullOrEmpty(characterName))
            return Array.Empty<ScheduledSessionSnapshot>();

        var threshold = DateTime.UtcNow.AddMinutes(Math.Clamp(_config.ScheduledLeadMinutes, 0, 120));

        lock (_saveGate)
        {
            return _config.ScheduledSessions
                .Where(s => string.Equals(s.CharacterName, characterName, StringComparison.Ordinal))
                .Where(s => s.ScheduledForUtc <= threshold && s.DismissedForUtc != s.ScheduledForUtc)
                .OrderBy(s => s.ScheduledForUtc)
                .ToArray();
        }
    }

    public bool HasDue => Due().Count > 0;

    public void Tick()
    {
        if (_isRefreshing || _cts.IsCancellationRequested)
            return;

        if (DateTime.UtcNow < _nextRefreshUtc)
            return;

        if (!_playerInfo.IsLoggedIn || _config.ScheduledSessions.Count == 0)
        {
            _nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
            return;
        }

        var characterName = _playerInfo.GetCharacterName();
        if (string.IsNullOrEmpty(characterName))
        {
            _nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
            return;
        }

        _isRefreshing = true;
        _nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var remote = await _client.GetScheduledAsync(characterName, ct);
                if (remote != null && !ct.IsCancellationRequested)
                    Reconcile(characterName, remote);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isRefreshing = false;
            }
        }, ct);
    }

    private void Reconcile(string characterName, IReadOnlyList<ScheduledEventResponse> remote)
    {
        lock (_saveGate)
        {
            var byId = remote.ToDictionary(r => r.Id, StringComparer.Ordinal);
            var changed = false;

            for (var i = _config.ScheduledSessions.Count - 1; i >= 0; i--)
            {
                var local = _config.ScheduledSessions[i];

                if (!string.Equals(local.CharacterName, characterName, StringComparison.Ordinal))
                    continue;

                if (!byId.TryGetValue(local.Id, out var server))
                {
                    _missedRefreshes.TryGetValue(local.Id, out var misses);
                    misses++;

                    if (misses < MissesBeforeForgetting)
                    {
                        _missedRefreshes[local.Id] = misses;
                        _log.Warning(
                            "Scheduled session {Id} was absent from refresh {Miss} of {Limit}; keeping the saved copy for now.",
                            local.Id, misses, MissesBeforeForgetting);
                        continue;
                    }

                    _missedRefreshes.Remove(local.Id);
                    _config.ScheduledSessions.RemoveAt(i);
                    changed = true;
                    continue;
                }

                _missedRefreshes.Remove(local.Id);

                if (local.ScheduledForUtc == server.ScheduledFor)
                    continue;

                local.ScheduledForUtc = server.ScheduledFor;
                local.Recurrence = server.Recurrence;
                local.RecurrenceDay = server.RecurrenceDay;
                local.RecurrenceWeek = server.RecurrenceWeek;
                local.DismissedForUtc = null;
                changed = true;
            }

            if (changed)
                _config.Save();
        }
    }

    public async Task<string?> ScheduleAsync(
        PostScheduledEventRequest request,
        string? automaticRuleSourceName,
        string? presetName,
        string? profileId,
        string? sentPictureHash)
    {
        request.RequestId ??= Guid.NewGuid().ToString();

        var (created, error) = await _client.PostScheduledAsync(request);
        if (created == null)
            return error ?? "Failed to schedule the session. Check the log for details.";

        if (string.IsNullOrEmpty(created.Id) || string.IsNullOrEmpty(created.SessionToken))
        {
            _log.Error(
                "POST /scheduled answered without an id or session token, so the schedule cannot be managed from here.");
            return GambaWhereClient.UnconfirmedCreateMessage;
        }

        lock (_saveGate)
        {
            _config.ScheduledSessions.Add(new ScheduledSessionSnapshot
            {
                Id = created.Id,
                SessionToken = created.SessionToken,
                CharacterName = created.CharacterName,
                GameType = created.Game,
                VenueName = created.VenueName,
                AutomaticRuleSourceName = automaticRuleSourceName,
                PresetName = presetName,
                ProfileId = profileId,
                Description = created.Description,
                Location = created.Location,
                ScheduledForUtc = created.ScheduledFor,
                Recurrence = created.Recurrence,
                RecurrenceDay = created.RecurrenceDay,
                RecurrenceWeek = created.RecurrenceWeek
            });

            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null && sentPictureHash != null && !string.IsNullOrEmpty(created.ProfileImageUrl))
            {
                profile.UploadedImageUrl = created.ProfileImageUrl;
                profile.UploadedImageHash = sentPictureHash;
            }

            try
            {
                _config.Save();
            }
            catch (Exception ex)
            {
                _log.Error(
                    ex,
                    "Scheduled session {Id} was created on the server but could not be written to the config.",
                    created.Id);

                return "The session was scheduled, but saving it locally failed. Close the game cleanly and check Upcoming Gamba Events.";
            }
        }

        return null;
    }

    public async Task<string?> StartNowAsync(string scheduleId)
    {
        var snapshot = Find(scheduleId);
        if (snapshot == null)
            return "That scheduled session is no longer saved.";

        if (!_playerInfo.IsLoggedIn)
            return "You must be logged in to start a session.";

        var characterName = await _framework.RunOnFrameworkThread(() => _playerInfo.GetCharacterName());
        var location = await _framework.RunOnFrameworkThread(() => _playerInfo.GetCurrentLocation());

        if (string.IsNullOrEmpty(characterName))
            return "Could not read your character details.";

        var request = new PostEventRequest
        {
            CharacterName = characterName,
            Location = location ?? "Unknown",
            Game = snapshot.GameType ?? string.Empty,
            Rules = await ResolveRulesAsync(snapshot),
            Description = snapshot.Description ?? string.Empty,
            VenueName = snapshot.VenueName ?? "No Venue",
            BoosterKey = string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim()
        };

        var profile = ResolveProfile(snapshot.ProfileId);
        var sentPictureHash = AttachProfile(request, profile);

        var (error, created) = await _sessionService.StartSessionAsync(
            request, automaticRuleSourceName: snapshot.AutomaticRuleSourceName);

        if (error != null)
            return error;

        if (profile != null && sentPictureHash != null && !string.IsNullOrEmpty(created?.ProfileImageUrl))
        {
            lock (_saveGate)
            {
                profile.UploadedImageUrl = created!.ProfileImageUrl;
                profile.UploadedImageHash = sentPictureHash;
                _config.Save();
            }
        }

        await RetireAsync(snapshot);
        return null;
    }

    private GambaProfile? ResolveProfile(string? profileId)
    {
        if (string.IsNullOrEmpty(profileId))
            return null;

        return _config.Profiles.FirstOrDefault(p => p.Id == profileId)
            ?? _config.Profiles.FirstOrDefault(p => p.Id == _config.SelectedProfileId);
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

    private async Task<Dictionary<string, object>> ResolveRulesAsync(ScheduledSessionSnapshot snapshot)
    {
        var gameType = snapshot.GameType ?? string.Empty;
        var sourceName = snapshot.AutomaticRuleSourceName;

        if (string.IsNullOrEmpty(sourceName))
            return PresetRules.Build(_config, gameType, snapshot.PresetName);

        var live = await _framework.RunOnFrameworkThread(
            () => ResolveAutomaticRules?.Invoke(gameType, sourceName));

        if (live is { Count: > 0 })
            return new Dictionary<string, object>(live);

        _log.Information(
            "Rule source {Source} had nothing to give for {Game}; starting with no rules until the next heartbeat picks them up.",
            sourceName, gameType);

        return new Dictionary<string, object>();
    }

    public async Task<string?> EditAsync(string scheduleId, PutScheduledEventRequest request)
    {
        var snapshot = Find(scheduleId);
        if (snapshot == null)
            return "That scheduled session is no longer saved, so it cannot be edited from here.";

        if (!request.HasChanges)
            return null;

        var (updated, error) = await _client.PutScheduledAsync(snapshot.Id, snapshot.SessionToken, request);
        if (updated == null)
            return error ?? "Failed to save the changes. Check the log for details.";

        lock (_saveGate)
        {
            var local = _config.ScheduledSessions.FirstOrDefault(s => s.Id == snapshot.Id);
            if (local == null)
                return null;

            local.Description = updated.Description;
            local.Location = updated.Location;
            local.VenueName = updated.VenueName;
            local.GameType = updated.Game;
            local.ScheduledForUtc = updated.ScheduledFor;
            local.Recurrence = updated.Recurrence;
            local.RecurrenceDay = updated.RecurrenceDay;
            local.RecurrenceWeek = updated.RecurrenceWeek;
            local.DismissedForUtc = null;
            _config.Save();
        }

        return null;
    }

    public async Task<string?> CancelAsync(string scheduleId)
    {
        var snapshot = Find(scheduleId);
        if (snapshot == null)
            return null;

        await RetireAsync(snapshot);
        return null;
    }

    public async Task<string?> DeleteSeriesAsync(string scheduleId)
    {
        var snapshot = Find(scheduleId);
        if (snapshot == null)
            return null;

        if (!await _client.DeleteScheduledAsync(snapshot.Id, snapshot.SessionToken))
            return "Failed to delete the scheduled session. Check the log for details.";

        Forget(snapshot.Id);
        return null;
    }

    public void Dismiss(string scheduleId)
    {
        lock (_saveGate)
        {
            var snapshot = _config.ScheduledSessions.FirstOrDefault(s => s.Id == scheduleId);
            if (snapshot == null)
                return;

            snapshot.DismissedForUtc = snapshot.ScheduledForUtc;
            _config.Save();
        }
    }

    private async Task RetireAsync(ScheduledSessionSnapshot snapshot)
    {
        var (result, gone) = await _client.AdvanceScheduledAsync(
            snapshot.Id, snapshot.SessionToken, snapshot.ScheduledForUtc);

        if (gone || result is { Removed: true })
        {
            Forget(snapshot.Id);
            return;
        }

        if (result?.Schedule == null)
        {
            _log.Warning(
                "Could not retire scheduled session {Id}; keeping the saved copy so it can be cancelled later.",
                snapshot.Id);
            return;
        }

        lock (_saveGate)
        {
            var local = _config.ScheduledSessions.FirstOrDefault(s => s.Id == snapshot.Id);
            if (local == null)
                return;

            local.ScheduledForUtc = result.Schedule.ScheduledFor;
            local.Recurrence = result.Schedule.Recurrence;
            local.RecurrenceDay = result.Schedule.RecurrenceDay;
            local.RecurrenceWeek = result.Schedule.RecurrenceWeek;
            local.DismissedForUtc = null;
            _config.Save();
        }
    }

    private ScheduledSessionSnapshot? Find(string scheduleId)
    {
        lock (_saveGate)
            return _config.ScheduledSessions.FirstOrDefault(s => s.Id == scheduleId);
    }

    private void Forget(string scheduleId)
    {
        lock (_saveGate)
        {
            _missedRefreshes.Remove(scheduleId);

            if (_config.ScheduledSessions.RemoveAll(s => s.Id == scheduleId) > 0)
                _config.Save();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
