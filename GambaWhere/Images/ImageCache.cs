using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GambaWhere.Images;

/// <summary>Asynchronous image cache that fetches and stores remote and bundled images as GPU textures.</summary>
public class ImageCache : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ITextureProvider _textureProvider;
    private readonly IPluginLog _log;
    private readonly HttpClient _http;

    private const string ProfileCachePrefix = "profile:";

    private readonly Dictionary<string, IDalamudTextureWrap?> _cache = new();
    private readonly HashSet<string> _loading = new();
    private readonly object _lock = new();
    private readonly string _cacheDirectory;
    private readonly string _profileCacheDirectory;
    private readonly CancellationTokenSource _cts = new();

    public ImageCache(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider, IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _textureProvider = textureProvider;
        _log = log;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);

        _cacheDirectory = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "ImageCache");
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }

        _profileCacheDirectory = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "ProfileImageCache");
        if (!Directory.Exists(_profileCacheDirectory))
        {
            Directory.CreateDirectory(_profileCacheDirectory);
        }
    }

    public int GetCachedImageCount()
    {
        if (!Directory.Exists(_cacheDirectory)) return 0;
        return Directory.GetFiles(_cacheDirectory).Length;
    }

    public int GetCachedProfileImageCount()
    {
        if (!Directory.Exists(_profileCacheDirectory)) return 0;
        return Directory.GetFiles(_profileCacheDirectory).Length;
    }

    public void ClearCache()
    {
        EvictMemory(key => !key.StartsWith(ProfileCachePrefix, StringComparison.Ordinal));
        DeleteCachedFiles(_cacheDirectory);
    }

    public void ClearProfileCache()
    {
        EvictMemory(key => key.StartsWith(ProfileCachePrefix, StringComparison.Ordinal));
        DeleteCachedFiles(_profileCacheDirectory);
    }

    private void EvictMemory(Func<string, bool> keyMatches)
    {
        lock (_lock)
        {
            var keys = _cache.Keys.Where(keyMatches).ToList();
            foreach (var key in keys)
            {
                _cache[key]?.Dispose();
                _cache.Remove(key);
            }
        }
    }

    private static void DeleteCachedFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            foreach (var file in Directory.GetFiles(directory))
                File.Delete(file);
        }
        catch
        {
        }
    }

    public IDalamudTextureWrap? Get(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        lock (_lock)
        {
            if (_cache.TryGetValue(url, out var tex))
                return tex;

            if (!_loading.Contains(url))
                BeginLoad(url, url, _cacheDirectory);

            return null;
        }
    }

    public IDalamudTextureWrap? GetProfile(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var cacheKey = $"{ProfileCachePrefix}{url}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var tex))
                return tex;

            if (!_loading.Contains(cacheKey))
                BeginLoad(cacheKey, url, _profileCacheDirectory);

            return null;
        }
    }

    public IDalamudTextureWrap? GetBundledPng(string fileName) => GetBundledImage(fileName, "bundled");

    public IDalamudTextureWrap? GetBundledImage(string fileName, string cacheNamespace = "bundledimg")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var cacheKey = $"{cacheNamespace}:{fileName.Trim()}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var tex))
                return tex;

            if (!_loading.Contains(cacheKey))
                BeginLoadBundled(cacheKey, fileName.Trim());

            return null;
        }
    }

    private void BeginLoad(string cacheKey, string url, string directory)
    {
        _loading.Add(cacheKey);
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                var hash = GetSha256Hash(url);
                var filePath = Path.Combine(directory, $"{hash}.img");

                byte[] bytes;
                if (File.Exists(filePath))
                {
                    bytes = await File.ReadAllBytesAsync(filePath, ct);
                }
                else
                {
                    bytes = await _http.GetByteArrayAsync(url, ct);
                    await File.WriteAllBytesAsync(filePath, bytes, ct);
                }

                wrap = await _textureProvider.CreateFromImageAsync(bytes);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "GET {Url} returned unexpected status or failed to load image.", url);
            }
            finally
            {
                lock (_lock)
                {
                    _loading.Remove(cacheKey);
                    if (ct.IsCancellationRequested)
                        wrap?.Dispose();
                    else
                        _cache[cacheKey] = wrap;
                }
            }
        }, ct);
    }

    private void BeginLoadBundled(string cacheKey, string fileName)
    {
        _loading.Add(cacheKey);

        var pluginDir = _pluginInterface.AssemblyLocation.DirectoryName;
        if (string.IsNullOrEmpty(pluginDir))
        {
            lock (_lock)
            {
                _cache[cacheKey] = null;
                _loading.Remove(cacheKey);
            }

            return;
        }

        var fullPath = Path.Combine(pluginDir, "Images", fileName);
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                if (File.Exists(fullPath))
                {
                    var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                    wrap = await _textureProvider.CreateFromImageAsync(bytes);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                lock (_lock)
                {
                    _loading.Remove(cacheKey);
                    if (ct.IsCancellationRequested)
                        wrap?.Dispose();
                    else
                        _cache[cacheKey] = wrap;
                }
            }
        }, ct);
    }

    private string GetSha256Hash(string rawData)
    {
        using var sha256Hash = SHA256.Create();
        var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var t in bytes)
        {
            builder.Append(t.ToString("x2"));
        }
        return builder.ToString();
    }

    public IDalamudTextureWrap? GetFromPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        var cacheKey = $"localpath:{absolutePath}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var tex))
                return tex;

            if (!_loading.Contains(cacheKey))
                BeginLoadFromPath(cacheKey, absolutePath);

            return null;
        }
    }

    public void EvictFromPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return;

        var cacheKey = $"localpath:{absolutePath}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var tex))
            {
                tex?.Dispose();
                _cache.Remove(cacheKey);
            }
        }
    }

    private void BeginLoadFromPath(string cacheKey, string absolutePath)
    {
        _loading.Add(cacheKey);
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                if (File.Exists(absolutePath))
                {
                    var bytes = await File.ReadAllBytesAsync(absolutePath, ct);
                    wrap = await _textureProvider.CreateFromImageAsync(bytes);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to load image from path {Path}", absolutePath);
            }
            finally
            {
                lock (_lock)
                {
                    _loading.Remove(cacheKey);
                    if (ct.IsCancellationRequested)
                        wrap?.Dispose();
                    else
                        _cache[cacheKey] = wrap;
                }
            }
        }, ct);
    }

    public void Dispose()
    {
        _cts.Cancel();

        lock (_lock)
        {
            foreach (var tex in _cache.Values)
                tex?.Dispose();

            _cache.Clear();
        }

        _http.Dispose();
        _cts.Dispose();
    }
}
