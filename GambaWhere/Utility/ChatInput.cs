using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace GambaWhere.Utility;

/// <summary>
/// Helpers for the recruitment "Contact" action. Builds the <c>/tell Name@World</c> command for a
/// poster and copies it to the clipboard so the user can paste it into chat and send it themselves
/// (we never auto-send). A short confirmation is printed to the local chat log.
/// </summary>
public static class ChatInput
{
    public static bool TryBuildTellCommand(string posterCharacter, out string command, out string target)
    {
        command = string.Empty;
        target = string.Empty;

        if (string.IsNullOrWhiteSpace(posterCharacter))
            return false;

        var parts = posterCharacter.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        var world = parts[^1];
        var name = string.Join(' ', parts[..^1]);
        target = $"{name}@{world}";
        command = $"/tell {target} ";
        return true;
    }

    public static bool CopyTellToClipboard(string posterCharacter, IChatGui chat)
    {
        if (!TryBuildTellCommand(posterCharacter, out var command, out var target))
            return false;

        ImGui.SetClipboardText(command);

        var msg = new SeStringBuilder()
            .AddText($"Copied \"/tell {target}\" to your clipboard. Paste it into chat to message them.")
            .Build();
        chat.Print(msg, "GambaWhere");
        return true;
    }
}
