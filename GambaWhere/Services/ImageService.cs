using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.ImGuiMethods;
using GambaWhere.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace GambaWhere.Services;

/// <summary>loads textures through ECommons, caches remote downloads on disk, and stores user-supplied profile pictures and custom banners.</summary>
public sealed class ImageService : IDisposable
{
    public const long MaxProfileImageBytes = 5 * 1024 * 1024;

    private const int ProfileImageSize = 512;

    private static readonly string[] ProfileExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly string[] BannerExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly IPluginLog _log;
    private readonly HttpClient _http;

    private readonly string _cacheDir;
    private readonly string _profileDir;
    private readonly string _bannerDir;
    private readonly string _bundledDir;
    private readonly string _tempDir;

    private readonly HashSet<string> _downloading = new();
    private readonly Dictionary<string, long> _failedAtTick = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private const long RetryDelayMs = 30_000;

    public ImageService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var configDir = pluginInterface.ConfigDirectory.FullName;
        _cacheDir = EnsureDirectory(Path.Combine(configDir, "ImageCache"));
        _profileDir = EnsureDirectory(Path.Combine(configDir, "Profiles"));
        _bannerDir = EnsureDirectory(Path.Combine(configDir, "CustomBanners"));
        _tempDir = EnsureDirectory(Path.Combine(configDir, "Temp"));
        ClearTemp();

        _bundledDir = Path.Combine(
            pluginInterface.AssemblyLocation.DirectoryName ?? pluginInterface.AssemblyLocation.FullName, "Images");
    }

    public IDalamudTextureWrap? GetFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var filePath = Path.Combine(_cacheDir, hash + ".img");
        if (File.Exists(filePath))
            return Load(filePath);

        lock (_lock)
        {
            var recentlyFailed = _failedAtTick.TryGetValue(filePath, out var tick)
                && Environment.TickCount64 - tick < RetryDelayMs;
            if (_downloading.Contains(filePath) || recentlyFailed)
                return null;
            _downloading.Add(filePath);
        }

        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            var saved = false;
            try
            {
                var bytes = await _http.GetByteArrayAsync(url, ct);
                if (IsImage(bytes))
                {
                    await File.WriteAllBytesAsync(filePath, bytes, ct);
                    saved = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "GET {Url} failed to download image.", url);
            }
            finally
            {
                lock (_lock)
                {
                    _downloading.Remove(filePath);
                    if (saved)
                        _failedAtTick.Remove(filePath);
                    else
                        _failedAtTick[filePath] = Environment.TickCount64;
                }
            }
        }, ct);

        return null;
    }

    public IDalamudTextureWrap? GetBundled(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return Load(Path.Combine(_bundledDir, fileName.Trim()));
    }

    public IDalamudTextureWrap? GetFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Load(path);
    }

    public void ReloadImages() => ThreadLoadImageHandler.ClearAll();

    public int GetCachedImageCount()
        => Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir).Length : 0;

    public void ClearCache()
    {
        if (Directory.Exists(_cacheDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheDir))
                    File.Delete(file);
            }
            catch
            {
            }
        }

        ThreadLoadImageHandler.ClearAll();
    }

    public bool ValidateProfileSource(string sourcePath, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = "That file could not be found.";
            return false;
        }

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(ProfileExtensions, ext) < 0)
        {
            error = "Picture must be a PNG or JPG.";
            return false;
        }

        if (new FileInfo(sourcePath).Length > MaxProfileImageBytes)
        {
            error = "Picture must be 5 MB or smaller.";
            return false;
        }

        if (!FileIsImage(sourcePath))
        {
            error = "That file does not look like a valid picture.";
            return false;
        }

        return true;
    }

    public string? CreateCropPreview(string sourcePath, float zoom, float centerX, float centerY, out string? error)
    {
        if (!ValidateProfileSource(sourcePath, out error))
            return null;

        var destPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.png");
        return WriteCroppedSquare(sourcePath, zoom, centerX, centerY, destPath, out error) ? destPath : null;
    }

    public bool SaveProfileImageSet(string sourcePath, string profileId, float zoom, float centerX, float centerY,
        out string? originalFileName, out string? squareFileName, out string? error)
    {
        originalFileName = null;
        squareFileName = null;

        if (!ValidateProfileSource(sourcePath, out error))
            return false;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        var originalName = $"{profileId}.orig{ext}";
        var squareName = $"{profileId}.png";
        var destOriginal = Path.Combine(_profileDir, originalName);

        if (!PathsEqual(sourcePath, destOriginal))
        {
            try
            {
                ClearStaleProfileFiles(profileId, keepOriginalExt: ext);
                File.Copy(sourcePath, destOriginal, overwrite: true);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to copy original profile picture from {Source}", sourcePath);
                error = "Could not save that picture.";
                return false;
            }
        }

        if (!WriteCroppedSquare(destOriginal, zoom, centerX, centerY, Path.Combine(_profileDir, squareName), out error))
            return false;

        originalFileName = originalName;
        squareFileName = squareName;
        return true;
    }

    public void DeleteTemp(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var full = Path.GetFullPath(path);
        if (!full.StartsWith(_tempDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return;

        try { File.Delete(full); } catch { }
    }

    private bool WriteCroppedSquare(string sourcePath, float zoom, float centerX, float centerY, string destPath, out string? error)
    {
        error = null;

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(sourcePath);
            var (uv0, uv1) = ProfileCropGeometry.SquareUv(image.Width, image.Height, zoom, centerX, centerY);

            var x = Math.Clamp((int)MathF.Round(uv0.X * image.Width), 0, image.Width - 1);
            var y = Math.Clamp((int)MathF.Round(uv0.Y * image.Height), 0, image.Height - 1);
            var side = Math.Max(1, (int)MathF.Round((uv1.X - uv0.X) * image.Width));
            side = Math.Min(side, Math.Min(image.Width - x, image.Height - y));

            image.Mutate(ctx => ctx
                .Crop(new Rectangle(x, y, side, side))
                .Resize(new ResizeOptions
                {
                    Size = new Size(ProfileImageSize, ProfileImageSize),
                    Sampler = KnownResamplers.Lanczos3,
                    Mode = ResizeMode.Stretch,
                }));
            image.Save(destPath, new PngEncoder());
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to bake cropped profile picture from {Source}", sourcePath);
            error = "Could not process that picture.";
            return false;
        }
    }

    private void ClearStaleProfileFiles(string profileId, string keepOriginalExt)
    {
        foreach (var ext in ProfileExtensions)
        {
            var legacySquare = Path.Combine(_profileDir, $"{profileId}{ext}");
            if (ext != ".png" && File.Exists(legacySquare))
            {
                try { File.Delete(legacySquare); } catch { }
            }

            if (ext == keepOriginalExt)
                continue;

            var staleOriginal = Path.Combine(_profileDir, $"{profileId}.orig{ext}");
            if (File.Exists(staleOriginal))
            {
                try { File.Delete(staleOriginal); } catch { }
            }
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ClearTemp()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_tempDir))
                File.Delete(file);
        }
        catch
        {
        }
    }

    public string? GetProfileImagePath(string? fileName) => StoredPath(_profileDir, fileName);

    public void DeleteProfileImage(string? fileName) => DeleteStored(_profileDir, fileName, "profile picture");

    public bool TryEncodeProfileImage(string path, out string base64, out string hash)
    {
        base64 = string.Empty;
        hash = string.Empty;

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxProfileImageBytes)
                return false;

            var bytes = File.ReadAllBytes(path);
            base64 = Convert.ToBase64String(bytes);
            hash = Convert.ToHexString(SHA256.HashData(bytes));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? SaveBanner(string sourcePath, string slotName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            _log.Warning("Custom banner source path does not exist: {Path}", sourcePath);
            return null;
        }

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(BannerExtensions, ext) < 0)
        {
            _log.Warning("Unsupported banner format: {Ext}", ext);
            return null;
        }

        var fileName = $"{slotName}{ext}";

        try
        {
            File.Copy(sourcePath, Path.Combine(_bannerDir, fileName), overwrite: true);
            return fileName;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to copy custom banner from {Source}", sourcePath);
            return null;
        }
    }

    public string? GetBannerPath(string? fileName) => StoredPath(_bannerDir, fileName);

    public void DeleteBanner(string? fileName) => DeleteStored(_bannerDir, fileName, "custom banner");

    private static IDalamudTextureWrap? Load(string path)
        => ThreadLoadImageHandler.TryGetTextureWrap(path, out var wrap) ? wrap : null;

    private static bool IsImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            return false;

        return (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            || (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            || (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            || (bytes[0] == 0x42 && bytes[1] == 0x4D)
            || (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50);
    }

    private static bool FileIsImage(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            return IsImage(header[..stream.Read(header)]);
        }
        catch
        {
            return false;
        }
    }

    private static string? StoredPath(string directory, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }

    private void DeleteStored(string directory, string? fileName, string label)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to delete {Label} {File}", label, fileName);
        }
    }

    private static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        _cts.Cancel();
        ClearTemp();
        ThreadLoadImageHandler.ClearAll();
        _http.Dispose();
        _cts.Dispose();
    }
}
