using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.Services;
using GambaWhere.UI;

namespace GambaWhere.UI.Tabs;

/// <summary>Settings tab. Its sub-tabs (UI, Chat, Booster Key, Other) live in the Settings.*Tab.cs partials.</summary>
public partial class SettingsTab
{
    private readonly Configuration _config;
    private readonly ImageService _imageService;
    private readonly IPluginLog _log;
    private readonly SessionPillOverlay _pillOverlay;

    public SettingsTab(Configuration config, ImageService imageService, IPluginLog log, SessionPillOverlay pillOverlay)
    {
        _config = config;
        _imageService = imageService;
        _log = log;
        _pillOverlay = pillOverlay;
    }
}
