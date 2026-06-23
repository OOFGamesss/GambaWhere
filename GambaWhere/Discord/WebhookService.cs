using System;
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
    private readonly SessionState _sessionState;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _discordGate = new(1, 1);
    private readonly ImageService _imageService;
    private readonly string _bannerDir;

    private DiscordSessionSnapshot _lastSentActiveSnapshot;
    private string? _lastSentRulesJson;
    private bool _hasSentActiveSnapshot;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebhookService(
        IPluginLog log,
        Configuration config,
        SessionState sessionState,
        string pluginDirectory,
        ImageService imageService)
    {
        _log = log;
        _config = config;
        _sessionState = sessionState;
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
            var snapshot = CaptureSessionSnapshot();

            if (!snapshot.IsActive || !HasSnapshotChanged(snapshot))
                return;

            foreach (var entry in entries.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
            {
                await DispatchSingleEntryAsync(entry, snapshot, DispatchKind.RequireActiveSnapshot, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _config.Save();

            _lastSentActiveSnapshot = snapshot;
            _lastSentRulesJson = snapshot.Rules != null ? JsonSerializer.Serialize(snapshot.Rules) : null;
            _hasSentActiveSnapshot = true;
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
            foreach (var entry in entries.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Url) && !e.PostFailed))
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

    public async Task ApplyEntryCommittedAsync(DiscordWebhookEntry entry, CancellationToken cancellationToken = default)
    {
        if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Url))
            return;

        await _discordGate.WaitAsync(cancellationToken);
        try
        {
            var kind = _sessionState.IsActive ? DispatchKind.RequireActiveSnapshot : DispatchKind.AlwaysIdle;
            await DispatchSingleEntryAsync(entry, CaptureSessionSnapshot(), kind, cancellationToken);
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

        var currentRulesJson = current.Rules != null ? JsonSerializer.Serialize(current.Rules) : null;
        return currentRulesJson != _lastSentRulesJson;
    }

    private DiscordSessionSnapshot CaptureSessionSnapshot() =>
        new(_sessionState.IsActive,
            _sessionState.CharacterName,
            _sessionState.GameType,
            _sessionState.VenueName,
            _sessionState.Location,
            _sessionState.ActiveRules,
            _sessionState.DiscordUrl,
            _sessionState.ImageUrl);

    private async Task DispatchSingleEntryAsync(
        DiscordWebhookEntry entry,
        DiscordSessionSnapshot snapshot,
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
                if (!snapshot.IsActive)
                    return;

                var theme = WebhookTheme.ResolveForGame(snapshot.GameType);

                var customPath = _imageService.GetBannerPath(_config.CustomActiveBannerFileName);
                var loaded = customPath != null
                    ? LoadBannerFromPath(customPath) ?? LoadBanner(theme.BannerFile)
                    : LoadBanner(theme.BannerFile);

                if (!loaded.HasValue)
                {
                    entry.PostFailed = true;
                    return;
                }

                bannerBytes = loaded.Value.bytes;
                bannerFileName = loaded.Value.fileName;
                payloadJson = Serialize(WebhookPayload.ForActive(snapshot, theme, bannerFileName, isFirstPost));
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
                payloadJson = Serialize(WebhookPayload.ForIdle(bannerFileName, isFirstPost));
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await WebhookTransport.SendAsync(
                    _http, _log, entry, payloadJson, bannerBytes, bannerFileName, MaxRetries, cancellationToken);

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
