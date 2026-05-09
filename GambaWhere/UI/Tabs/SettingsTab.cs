using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using GambaWhere.Config;
using GambaWhere.Images;

namespace GambaWhere.UI.Tabs;

public class SettingsTab
{
    private static readonly string[] SoundEffectOptions = BuildSoundEffectOptions();

    private readonly Configuration _config;
    private readonly ImageCache _imageCache;
    private readonly IPluginLog _log;

    public SettingsTab(Configuration config, ImageCache imageCache, IPluginLog log)
    {
        _config = config;
        _imageCache = imageCache;
        _log = log;
    }

    public void Draw()
    {
        DrawAutoSessionDetection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAlertOptions();
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

    private void DrawAlertOptions()
    {
        ImGui.Text("Alerts");

        var toastEnabled = _config.AlertToastEnabled;
        if (ImGui.Checkbox("Show desktop notification when an alert fires", ref toastEnabled))
        {
            _config.AlertToastEnabled = toastEnabled;
            _config.Save();
        }

        var soundEnabled = _config.AlertSoundEnabled;
        if (ImGui.Checkbox("Play sound effect when an alert fires", ref soundEnabled))
        {
            _config.AlertSoundEnabled = soundEnabled;
            _config.Save();
        }

        using (ImRaii.Disabled(!_config.AlertSoundEnabled))
        {
            ImGui.Indent();

            ImGui.Text("Sound:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f);

            var idx = Math.Clamp(_config.AlertSoundEffectId - 1, 0, SoundEffectOptions.Length - 1);
            if (ImGui.Combo("##AlertSe", ref idx, SoundEffectOptions, SoundEffectOptions.Length))
            {
                _config.AlertSoundEffectId = idx + 1;
                _config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Test"))
                PlaySoundEffect(Math.Clamp(_config.AlertSoundEffectId, 1, 16));

            ImGui.Unindent();
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

    private void PlaySoundEffect(int id)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)id);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PlayChatSoundEffect failed for SE {Id}", id);
        }
    }

    private static string[] BuildSoundEffectOptions()
    {
        var arr = new string[16];
        for (var i = 0; i < 16; i++)
            arr[i] = $"SE {i + 1}";
        return arr;
    }
}
