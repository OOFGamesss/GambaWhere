using System;
using System.IO;
using Dalamud.Plugin.Services;

namespace GambaWhere.Discord;

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
}
