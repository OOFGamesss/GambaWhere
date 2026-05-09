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
using GambaWhere.State;

namespace GambaWhere.Discord;

public sealed class DiscordWebhookService : IDisposable
{
    private const int MaxRetries = 4;

    private readonly IPluginLog _log;
    private readonly Configuration _config;
    private readonly SessionState _sessionState;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _discordGate = new(1, 1);
    private readonly DiscordWebhookAssets _assets;

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
        string pluginDirectory)
    {
        _log = log;
        _config = config;
        _sessionState = sessionState;
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
        await RunSyncedAsync(entries => entries, DispatchKind.RequireActiveSnapshot, cancellationToken);
    }

    public async Task PublishIdleEmbedsAsync(CancellationToken cancellationToken = default)
    {
        await RunSyncedAsync(entries => entries, DispatchKind.AlwaysIdle, cancellationToken);
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

    private async Task RunSyncedAsync(
        Func<DiscordWebhookEntry[], DiscordWebhookEntry[]> snapshotFilter,
        DispatchKind dispatchKind,
        CancellationToken cancellationToken)
    {
        DiscordWebhookEntry[] entries;
        lock (_config)
            entries = snapshotFilter([.. _config.DiscordWebhooks]);

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = CaptureSessionSnapshot();
            foreach (var entry in entries.Where(e =>
                         e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshot, dispatchKind, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();
        }
        finally
        {
            _discordGate.Release();
        }
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
                var loadedActive = _assets.TryLoadBanner(theme.BannerFile);
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
                var loadedIdle = _assets.TryLoadBanner(DiscordWebhookTheme.IdleBannerFile);
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
