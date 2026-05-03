using Dalamud.Bindings.ImGui;
using GambaWhere.Config;

namespace GambaWhere.UI.Tabs;

public class SettingsTab
{
    private readonly Configuration _config;

    public SettingsTab(Configuration config)
    {
        _config = config;
    }

    public void Draw()
    {
        DrawAutoSessionDetection();
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
}
