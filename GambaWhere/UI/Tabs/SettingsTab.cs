using Dalamud.Bindings.ImGui;
using GambaWhere.Config;
using GambaWhere.Images;

namespace GambaWhere.UI.Tabs;

public class SettingsTab
{
    private readonly Configuration _config;
    private readonly ImageCache _imageCache;

    public SettingsTab(Configuration config, ImageCache imageCache)
    {
        _config = config;
        _imageCache = imageCache;
    }

    public void Draw()
    {
        DrawAutoSessionDetection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawImageCacheSettings();
    }

    private void DrawAutoSessionDetection()
    {
        var enabled = _config.AutoSessionDetection;

        if (ImGui.Checkbox("Auto Session Detection", ref enabled))
        {
            _config.AutoSessionDetection = enabled;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "When a supported companion plugin opens (e.g. Chocobo Racing Gamba), a chat\n" +
                "message will appear reminding you to start a session, with a clickable link\n" +
                "that opens GambaWhere and pre-selects the correct game type for you.");
        }
    }

    private void DrawImageCacheSettings()
    {
        ImGui.Text("Image Cache");
        var count = _imageCache.GetCachedImageCount();
        ImGui.TextUnformatted($"Images stored: {count}");

        if (ImGui.Button("Clear Venue Image Cache"))
        {
            _imageCache.ClearCache();
        }
    }
}
