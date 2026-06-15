using System;
using System.IO;
using Dalamud.Plugin.Services;

namespace GambaWhere.Images;

/// <summary>Stores user-supplied profile pictures in the plugin's config directory.</summary>
public sealed class ProfileImageStore
{
    private readonly string _storeDirectory;
    private readonly IPluginLog _log;

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg"];

    public const long MaxBytes = 5 * 1024 * 1024;

    public ProfileImageStore(string configDirectory, IPluginLog log)
    {
        _log = log;
        _storeDirectory = Path.Combine(configDirectory, "Profiles");
        if (!Directory.Exists(_storeDirectory))
            Directory.CreateDirectory(_storeDirectory);
    }

    public string? TrySave(string sourcePath, string profileId, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = "That file could not be found.";
            return null;
        }

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            error = "Picture must be a PNG or JPG.";
            return null;
        }

        if (new FileInfo(sourcePath).Length > MaxBytes)
        {
            error = "Picture must be 5 MB or smaller.";
            return null;
        }

        var fileName = $"{profileId}{ext}";
        var destPath = Path.Combine(_storeDirectory, fileName);

        try
        {
            DeleteOtherExtensions(profileId, ext);
            File.Copy(sourcePath, destPath, overwrite: true);
            return fileName;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to copy profile picture from {Source}", sourcePath);
            error = "Could not save that picture.";
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
            _log.Warning(ex, "Failed to delete profile picture {File}", fileName);
        }
    }

    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = Path.Combine(_storeDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private void DeleteOtherExtensions(string profileId, string keepExt)
    {
        foreach (var ext in AllowedExtensions)
        {
            if (ext == keepExt)
                continue;

            var path = Path.Combine(_storeDirectory, $"{profileId}{ext}");
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch {  }
            }
        }
    }
}
