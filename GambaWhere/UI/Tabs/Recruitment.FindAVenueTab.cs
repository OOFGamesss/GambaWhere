using System;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Services;

namespace GambaWhere.UI.Tabs;

/// <summary>The "Find a Venue" recruitment tab (venues seeking hosts), backed by the shared RecruitmentTab engine.</summary>
public sealed class FindAVenueTab : IDisposable
{
    private readonly RecruitmentTab _board;

    public FindAVenueTab(
        GambaWhereClient client,
        ImageService imageService,
        Configuration config,
        PlayerInfoService playerInfo,
        IChatGui chatGui,
        IPluginLog log)
    {
        _board = new RecruitmentTab("venue", client, imageService, config, playerInfo, chatGui, log);
    }

    public void Draw() => _board.Draw();

    public void OnSelected() => _board.RequestRefresh();

    public void Dispose() => _board.Dispose();
}
