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
using GambaWhere.Models;
using GambaWhere.Services;
using GambaWhere.State;

namespace GambaWhere.Discord;

/// <summary>Manages Discord webhook embeds, dispatching active session or idle updates to all configured webhook URLs.</summary>
public sealed class WebhookService : IDisposable
{
    private const int MaxRetries = 4;

    private readonly IPluginLog _log;
    private readonly Configuration _config;
    private readonly HostSessions _sessions;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _discordGate = new(1, 1);
    private readonly ImageService _imageService;
    private readonly string _bannerDir;

    private string? _lastSentSnapshotJson;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebhookService(
        IPluginLog log,
        Configuration config,
        HostSessions sessions,
        string pluginDirectory,
        ImageService imageService)
    {
        _log = log;
        _config = config;
        _sessions = sessions;
        _imageService = imageService;

        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        _http = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(90) };

        _bannerDir = Path.Combine(pluginDirectory, "Images", "DiscordBanners");
    }

    public static bool TabShouldWarn(Configuration config) =>
        config.DiscordWebhooks.Exists(e => !string.IsNullOrWhiteSpace(e.Url) && e.PostFailed);

    public async Task SyncActiveSessionEmbedsAsync(CancellationToken cancellationToken = default)
    {
        DiscordWebhookEntry[] entries;
        lock (_config)
            entries = [.. _config.DiscordWebhooks];

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            var snapshots = CaptureSessionSnapshots();
            if (snapshots.Count == 0)
                return;

            var snapshotJson = JsonSerializer.Serialize(snapshots);
            if (snapshotJson == _lastSentSnapshotJson)
                return;

            foreach (var entry in entries.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshots, DispatchKind.RequireActiveSnapshot, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();

            _lastSentSnapshotJson = snapshotJson;
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
            _lastSentSnapshotJson = null;

            var snapshots = CaptureSessionSnapshots();
            foreach (var entry in entries.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshots, DispatchKind.AlwaysIdle, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();
        }
        finally
        {
            _discordGate.Release();
        }
    }

    public async Task ApplyEntryCommittedAsync(DiscordWebhookEntry entry, CancellationToken cancellationToken = default)
    {
        if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Url))
            return;

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            var snapshots = CaptureSessionSnapshots();
            var kind = snapshots.Count > 0 ? DispatchKind.RequireActiveSnapshot : DispatchKind.AlwaysIdle;
            await DispatchSingleEntryAsync(entry, snapshots, kind, cancellationToken);
            _config.Save();
        }
        finally
        {
            _discordGate.Release();
        }
    }

    private enum DispatchKind
    {
        RequireActiveSnapshot,
        AlwaysIdle
    }

    private List<DiscordSessionSnapshot> CaptureSessionSnapshots() =>
        _sessions.Snapshot()
            .Where(session => session.IsActive)
            .Select(session => new DiscordSessionSnapshot(
                session.IsActive,
                session.CharacterName,
                session.GameType,
                session.VenueName,
                session.Location,
                session.ActiveRules,
                session.DiscordUrl,
                session.ImageUrl))
            .ToList();

    private async Task DispatchSingleEntryAsync(
        DiscordWebhookEntry entry,
        IReadOnlyList<DiscordSessionSnapshot> snapshots,
        DispatchKind dispatchKind,
        CancellationToken cancellationToken)
    {
        if (!WebhookTransport.TryParseUrl(entry.Url, out _, out _))
            return;

        while (true)
        {
            var isFirstPost = string.IsNullOrWhiteSpace(entry.MessageId);

            byte[] bannerBytes;
            string bannerFileName;
            byte[] payloadJson;

            if (dispatchKind == DispatchKind.RequireActiveSnapshot)
            {
                if (snapshots.Count == 0)
                    return;

                var theme = WebhookTheme.ResolveForGame(snapshots[0].GameType);

                var customPath = _imageService.GetBannerPath(_config.CustomActiveBannerFileName);
                var loaded = customPath != null ? LoadBannerFromPath(customPath) : null;

                if (loaded.HasValue)
                {
                    bannerBytes = loaded.Value.bytes;
                    bannerFileName = loaded.Value.fileName;
                    payloadJson = Serialize(WebhookPayload.ForActive(snapshots, theme, bannerFileName, isFirstPost));
                }
                else if (!string.IsNullOrWhiteSpace(theme.BannerUrl))
                {
                    bannerBytes = Array.Empty<byte>();
                    bannerFileName = string.Empty;
                    payloadJson = Serialize(WebhookPayload.ForActive(snapshots, theme, null, isFirstPost));
                }
                else
                {
                    loaded = LoadBanner("minigamesbanner.png");
                    if (!loaded.HasValue)
                    {
                        entry.PostFailed = true;
                        return;
                    }

                    bannerBytes = loaded.Value.bytes;
                    bannerFileName = loaded.Value.fileName;
                    payloadJson = Serialize(WebhookPayload.ForActive(snapshots, theme, bannerFileName, isFirstPost));
                }
            }
            else
            {
                var customPath = _imageService.GetBannerPath(_config.CustomIdleBannerFileName);
                var loaded = customPath != null
                    ? LoadBannerFromPath(customPath) ?? LoadBanner(WebhookTheme.IdleBannerFile)
                    : LoadBanner(WebhookTheme.IdleBannerFile);

                if (!loaded.HasValue)
                {
                    entry.PostFailed = true;
                    return;
                }

                bannerBytes = loaded.Value.bytes;
                bannerFileName = loaded.Value.fileName;
                payloadJson = Serialize(WebhookPayload.ForIdle(bannerFileName, null, isFirstPost));
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await WebhookTransport.SendAsync(
                    _http, _log, entry, payloadJson,
                    bannerBytes.Length > 0 ? bannerBytes : null,
                    string.IsNullOrWhiteSpace(bannerFileName) ? null : bannerFileName,
                    MaxRetries, cancellationToken);

                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    if (!isFirstPost && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        entry.MessageId = null;
                        entry.PostFailed = false;
                        _config.Save();
                        continue;
                    }

                    entry.PostFailed = true;
                    return;
                }

                if (string.IsNullOrWhiteSpace(entry.MessageId) && response.Content != null)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("id", out var idEl)
                            && idEl.GetString() is { } id && !string.IsNullOrWhiteSpace(id))
                            entry.MessageId = id.Trim();
                    }
                    catch (JsonException)
                    {
                    }
                }

                entry.PostFailed = false;
                return;
            }
            catch (HttpRequestException)
            {
                entry.PostFailed = true;
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unexpected Discord webhook error.");
                entry.PostFailed = true;
                return;
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    private byte[] Serialize(DiscordOutboundPayloadDto dto) =>
        JsonSerializer.SerializeToUtf8Bytes(dto, _serializerOptions);

    private (byte[] bytes, string fileName)? LoadBanner(string fileName)
    {
        var path = Path.Combine(_bannerDir, fileName);
        if (!File.Exists(path))
        {
            _log.Warning("Discord banner asset is missing ({Path})", path);
            return null;
        }

        try
        {
            return (File.ReadAllBytes(path), fileName);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed reading Discord banner asset.");
            return null;
        }
    }

    private (byte[] bytes, string fileName)? LoadBannerFromPath(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            _log.Warning("Custom banner not found at path: {Path}", absolutePath);
            return null;
        }

        try
        {
            return (File.ReadAllBytes(absolutePath), Path.GetFileName(absolutePath));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed reading custom banner from path.");
            return null;
        }
    }

    public void Dispose()
    {
        _discordGate.Dispose();
        _http.Dispose();
    }
}
