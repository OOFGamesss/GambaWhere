using System;
using System.IO;
using Dalamud.Plugin.Services;

namespace GambaWhere.Discord;

/// <summary>Loads Discord embed banner images from the plugin's bundled asset directory or from an absolute path.</summary>
internal sealed class DiscordWebhookAssets
{
    private readonly IPluginLog _log;
    private readonly string _discordBannerDirectory;

    public DiscordWebhookAssets(IPluginLog log, string discordBannerDirectory)
    {
        _log = log;
        _discordBannerDirectory = discordBannerDirectory;
    }

    public (byte[] bytes, string fileName)? TryLoadBanner(string fileName)
    {
        var path = Path.Combine(_discordBannerDirectory, fileName);
        if (!File.Exists(path))
        {
            _log.Error("Discord banner asset is missing ({Path})", path);

            return null;
        }

        try
        {
            return (File.ReadAllBytes(path), fileName);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed reading Discord banner asset.");

            return null;
        }
    }

    public (byte[] bytes, string fileName)? TryLoadBannerFromPath(string absolutePath)
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
            _log.Error(ex, "Failed reading custom banner from path.");
            return null;
        }
    }
}
