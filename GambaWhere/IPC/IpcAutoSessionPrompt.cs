using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace GambaWhere.IPC;

/// <summary>Shared helper for prompting the user to auto-start a session from an IPC game.</summary>
internal static class IpcAutoSessionPrompt
{
    private const ushort PromptUiForegroundColour = 573;
    private const ushort PromptLinkCyanColour = 518;
    private const string LinkLabel = "Start it here!";
    private const string ChatSourceTag = "GambaWhere";

    public static void Print(IChatGui chatGui, DalamudLinkPayload linkPayload, string openedPluginName)
    {
        var intro = $"{openedPluginName} has been opened. Starting a session? ";

        var msg = new SeStringBuilder()
            .AddUiForeground(PromptUiForegroundColour)
            .AddText(intro)
            .AddUiForegroundOff()
            .AddUiForeground(PromptLinkCyanColour)
            .Add(linkPayload)
            .AddText(LinkLabel)
            .Add(RawPayload.LinkTerminator)
            .AddUiForegroundOff()
            .AddUiForeground(PromptUiForegroundColour)
            .AddUiForegroundOff()
            .Build();

        chatGui.Print(msg, ChatSourceTag);
    }
}
