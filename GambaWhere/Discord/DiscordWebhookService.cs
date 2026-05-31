using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.State;

namespace GambaWhere.Discord;

/// <summary>Manages Discord webhook embeds, dispatching active session or idle updates to all configured webhook URLs.</summary>
public sealed class DiscordWebhookService : IDisposable
{
    private const int MaxRetries = 4;

    private readonly IPluginLog _log;
    private readonly Configuration _config;
    private readonly SessionState _sessionState;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _discordGate = new(1, 1);
    private readonly DiscordWebhookAssets _assets;
    private readonly CustomBannerStore _customBanners;

    private DiscordSessionSnapshot _lastSentActiveSnapshot;
    private string? _lastSentRulesJson;
    private bool _hasSentActiveSnapshot;

    private readonly JsonSerializerOptions _serializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    public DiscordWebhookService(
        IPluginLog log,
        Configuration config,
        SessionState sessionState,
        string pluginDirectory,
        CustomBannerStore customBanners)
    {
        _log = log;
        _config = config;
        _sessionState = sessionState;
        _customBanners = customBanners;
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        _http = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(90) };

        var bannerDir = Path.Combine(pluginDirectory, "Images", "DiscordBanners");

        _assets = new DiscordWebhookAssets(_log, bannerDir);
    }

    public static bool TabShouldWarn(Configuration config) =>
        config.DiscordWebhooks.Exists(e =>
            !string.IsNullOrWhiteSpace(e.Url) && e.PostFailed);

    public void Dispose()
    {
        _discordGate.Dispose();
        _http.Dispose();
    }

    public async Task SyncActiveSessionEmbedsAsync(CancellationToken cancellationToken = default)
    {
        DiscordWebhookEntry[] entries;
        lock (_config)
            entries = [.. _config.DiscordWebhooks];

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = CaptureSessionSnapshot();

            if (!snapshot.IsActive || !HasSnapshotChanged(snapshot))
                return;

            foreach (var entry in entries.Where(e =>
                         e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshot, DispatchKind.RequireActiveSnapshot, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();
            RecordSentSnapshot(snapshot);
        }
        finally
        {
            _discordGate.Release();
        }
    }

    public async Task PublishIdleEmbedsAsync(CancellationToken cancellationToken = default)
    {
        DiscordWebhookEntry[] entries;
        lock (_config)
            entries = [.. _config.DiscordWebhooks];

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            _hasSentActiveSnapshot = false;

            var snapshot = CaptureSessionSnapshot();
            foreach (var entry in entries.Where(e =>
                         e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshot, DispatchKind.AlwaysIdle, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();
        }
        finally
        {
            _discordGate.Release();
        }
    }

    public async Task ApplyEntryCommittedAsync(DiscordWebhookEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Url))
            return;

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            await DispatchSingleEntryAsync(
                entry,
                CaptureSessionSnapshot(),
                ResolveDispatchKind(snapshotIsActive: _sessionState.IsActive),
                cancellationToken);

            _config.Save();
        }
        finally
        {
            _discordGate.Release();
        }
    }

    private static DispatchKind ResolveDispatchKind(bool snapshotIsActive) =>
        snapshotIsActive ? DispatchKind.RequireActiveSnapshot : DispatchKind.AlwaysIdle;

    private enum DispatchKind
    {
        RequireActiveSnapshot,
        AlwaysIdle
    }

    private bool HasSnapshotChanged(DiscordSessionSnapshot current)
    {
        if (!_hasSentActiveSnapshot)
            return true;

        var prev = _lastSentActiveSnapshot;

        if (prev.IsActive != current.IsActive
            || prev.CharacterName != current.CharacterName
            || prev.GameType != current.GameType
            || prev.VenueName != current.VenueName
            || prev.Location != current.Location
            || prev.DiscordUrl != current.DiscordUrl
            || prev.ImageUrl != current.ImageUrl)
            return true;

        var currentRulesJson = current.Rules != null
            ? JsonSerializer.Serialize(current.Rules)
            : null;

        return currentRulesJson != _lastSentRulesJson;
    }

    private void RecordSentSnapshot(DiscordSessionSnapshot snapshot)
    {
        _lastSentActiveSnapshot = snapshot;
        _lastSentRulesJson = snapshot.Rules != null
            ? JsonSerializer.Serialize(snapshot.Rules)
            : null;
        _hasSentActiveSnapshot = true;
    }

    private DiscordSessionSnapshot CaptureSessionSnapshot()
    {
        return new DiscordSessionSnapshot(
            _sessionState.IsActive,
            _sessionState.CharacterName,
            _sessionState.GameType,
            _sessionState.VenueName,
            _sessionState.Location,
            _sessionState.ActiveRules,
            _sessionState.DiscordUrl,
            _sessionState.ImageUrl);
    }

    private async Task DispatchSingleEntryAsync(
        DiscordWebhookEntry entry,
        DiscordSessionSnapshot snapshot,
        DispatchKind dispatchKind,
        CancellationToken cancellationToken)
    {
        if (!DiscordWebhookUrlParser.TryParseDiscordWebhook(entry.Url, out _, out _))
            return;

        while (true)
        {
            var isFirstPost = IsCreatingNewMessage(entry);

            byte[] bannerBytes;
            string bannerFileName;
            byte[] payloadJson;

            if (dispatchKind == DispatchKind.RequireActiveSnapshot)
            {
                if (!snapshot.IsActive)
                    return;

                var theme = DiscordWebhookTheme.ResolveForGame(snapshot.GameType);

                var customActivePath = _customBanners.GetPath(_config.CustomActiveBannerFileName);
                var loadedActive = customActivePath != null
                    ? _assets.TryLoadBannerFromPath(customActivePath) ?? _assets.TryLoadBanner(theme.BannerFile)
                    : _assets.TryLoadBanner(theme.BannerFile);

                if (!loadedActive.HasValue)
                {
                    DiscordWebhookEntryMutations.MarkFailure(entry);
                    return;
                }

                bannerBytes = loadedActive.Value.bytes;
                bannerFileName = loadedActive.Value.fileName;
                payloadJson = SerializeActive(snapshot, theme, bannerFileName, isFirstPost);
            }
            else
            {
                var customIdlePath = _customBanners.GetPath(_config.CustomIdleBannerFileName);
                var loadedIdle = customIdlePath != null
                    ? _assets.TryLoadBannerFromPath(customIdlePath) ?? _assets.TryLoadBanner(DiscordWebhookTheme.IdleBannerFile)
                    : _assets.TryLoadBanner(DiscordWebhookTheme.IdleBannerFile);

                if (!loadedIdle.HasValue)
                {
                    DiscordWebhookEntryMutations.MarkFailure(entry);
                    return;
                }

                bannerBytes = loadedIdle.Value.bytes;
                bannerFileName = loadedIdle.Value.fileName;
                payloadJson = SerializeIdle(bannerFileName, isFirstPost);
            }

            var parts = DiscordWebhookMultipartBuilder.BuildFileParts(bannerBytes, bannerFileName);

            HttpResponseMessage? response = null;
            try
            {
                response = await DiscordWebhookHttp.SendMultipartWithRetriesAsync(
                    _http,
                    _log,
                    entry,
                    payloadJson,
                    parts,
                    MaxRetries,
                    cancellationToken);

                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    if (!isFirstPost && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        entry.MessageId = null;
                        DiscordWebhookEntryMutations.ClearFailure(entry);
                        _config.Save();
                        continue;
                    }

                    DiscordWebhookEntryMutations.MarkFailure(entry);

                    return;
                }

                if (IsCreatingNewMessage(entry) && response.Content != null)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    DiscordWebhookEntryMutations.AssignMessageIdIfPresent(entry, json);
                }

                DiscordWebhookEntryMutations.ClearFailure(entry);

                return;
            }
            catch (HttpRequestException)
            {
                DiscordWebhookEntryMutations.MarkFailure(entry);

                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected Discord webhook error.");
                DiscordWebhookEntryMutations.MarkFailure(entry);

                return;
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    private static bool IsCreatingNewMessage(DiscordWebhookEntry entry) =>
        string.IsNullOrWhiteSpace(entry.MessageId);

    private byte[] SerializeIdle(string bannerFileName, bool isFirstPost)
    {
        var dto = DiscordWebhookPayloadFactory.ForIdleBanner(
            bannerFileName,
            isFirstPost);

        return JsonSerializer.SerializeToUtf8Bytes(dto, _serializerOptions);
    }

    private byte[] SerializeActive(
        DiscordSessionSnapshot snapshot,
        (int Colour, string Emoji, string BannerFile) theme,
        string bannerFileName,
        bool isFirstPost)
    {
        var dto = DiscordWebhookPayloadFactory.ForActive(
            snapshot,
            theme,
            bannerFileName,
            isFirstPost);

        return JsonSerializer.SerializeToUtf8Bytes(dto, _serializerOptions);
    }
}
