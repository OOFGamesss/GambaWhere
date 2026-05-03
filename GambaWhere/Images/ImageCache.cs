using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace GambaWhere.Images;

public class ImageCache : IDisposable
{
    private readonly ITextureProvider _textureProvider;
    private readonly HttpClient _http;

    private readonly Dictionary<string, IDalamudTextureWrap?> _cache = new();
    private readonly HashSet<string> _loading = new();
    private readonly object _lock = new();

    public ImageCache(ITextureProvider textureProvider)
    {
        _textureProvider = textureProvider;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
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

    private void BeginLoad(string url)
    {
        _loading.Add(url);

        _ = Task.Run(async () =>
        {
            IDalamudTextureWrap? wrap = null;
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
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
