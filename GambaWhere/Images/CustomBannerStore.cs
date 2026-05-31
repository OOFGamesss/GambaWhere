using System;
using System.IO;
using Dalamud.Plugin.Services;

namespace GambaWhere.Images;

/// <summary>Manages user-supplied custom banner images stored in the plugin's config directory.</summary>
public sealed class CustomBannerStore
{
    private readonly string _storeDirectory;
    private readonly IPluginLog _log;

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public CustomBannerStore(string configDirectory, IPluginLog log)
    {
        _log = log;
        _storeDirectory = Path.Combine(configDirectory, "CustomBanners");
        if (!Directory.Exists(_storeDirectory))
            Directory.CreateDirectory(_storeDirectory);
    }

    public string? TrySave(string sourcePath, string slotName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            _log.Warning("Custom banner source path does not exist: {Path}", sourcePath);
            return null;
        }

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            _log.Warning("Unsupported banner format: {Ext}", ext);
            return null;
        }

        var fileName = $"{slotName}{ext}";
        var destPath = Path.Combine(_storeDirectory, fileName);

        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return fileName;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to copy custom banner from {Source}", sourcePath);
            return null;
        }
    }

    public void Delete(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var path = Path.Combine(_storeDirectory, fileName);
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to delete custom banner {File}", fileName);
        }
    }

    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = Path.Combine(_storeDirectory, fileName);
        return File.Exists(path) ? path : null;
    }
}
