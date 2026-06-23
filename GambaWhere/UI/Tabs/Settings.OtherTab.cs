using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

// Other settings sub-tab: image cache information and clearing.
public partial class SettingsTab
{
    public void DrawOtherSection()
    {
        ImGui.Spacing();
        DrawImageCacheSettings();
    }

    private void DrawImageCacheSettings()
    {
        ImGui.TextColored(ThemeColours.AccentText(_config.SecondaryColour), "Image Cache");

        var count = _imageService.GetCachedImageCount();
        ImGui.TextUnformatted($"Cached images stored: {count}");

        if (UIHelper.IconTextButton(FontAwesomeIcon.Trash, "Clear Image Cache", "##ClearImageCache"))
            _imageService.ClearCache();
    }
}
