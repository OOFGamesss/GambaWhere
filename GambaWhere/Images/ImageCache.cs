using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GambaWhere.Images;

public class ImageCache : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ITextureProvider _textureProvider;
    private readonly HttpClient _http;

    private readonly Dictionary<string, IDalamudTextureWrap?> _cache = new();
    private readonly HashSet<string> _loading = new();
    private readonly object _lock = new();
    private readonly string _cacheDirectory;

    public ImageCache(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider)
    {
        _pluginInterface = pluginInterface;
        _textureProvider = textureProvider;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);

        _cacheDirectory = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "ImageCache");
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public int GetCachedImageCount()
    {
        if (!Directory.Exists(_cacheDirectory)) return 0;
        return Directory.GetFiles(_cacheDirectory).Length;
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            foreach (var tex in _cache.Values)
                tex?.Dispose();

            _cache.Clear();
        }

        if (Directory.Exists(_cacheDirectory))
        {
            try
            {
                var files = Directory.GetFiles(_cacheDirectory);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
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
                BeginLoad(url);

            return null;
        }
    }

    public IDalamudTextureWrap? GetBundledPng(string fileName) => GetBundledImage(fileName, "bundled");

    /// <summary>Loads any image file from the plugin <c>Images</c> directory (e.g. PNG or WebP).</summary>
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

    private void BeginLoad(string url)
    {
        _loading.Add(url);

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                var hash = GetSha256Hash(url);
                var filePath = Path.Combine(_cacheDirectory, $"{hash}.img");

                byte[] bytes;
                if (File.Exists(filePath))
                {
                    bytes = await File.ReadAllBytesAsync(filePath);
                }
                else
                {
                    bytes = await _http.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(filePath, bytes);
                }

                wrap = await _textureProvider.CreateFromImageAsync(bytes);
            }
            catch
            {
            }
            finally
            {
                lock (_lock)
                {
                    _cache[url] = wrap;
                    _loading.Remove(url);
                }
            }
        });
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

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                if (File.Exists(fullPath))
                {
                    var bytes = await File.ReadAllBytesAsync(fullPath);
                    wrap = await _textureProvider.CreateFromImageAsync(bytes);
                }
            }
            catch
            {
            }
            finally
            {
                lock (_lock)
                {
                    _cache[cacheKey] = wrap;
                    _loading.Remove(cacheKey);
                }
            }
        });
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

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var tex in _cache.Values)
                tex?.Dispose();

            _cache.Clear();
        }

        _http.Dispose();
    }
}
