using System;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Services;

namespace GambaWhere.UI.Tabs;

/// <summary>The "Find a Host" recruitment tab (hosts seeking venues), backed by the shared RecruitmentTab engine.</summary>
public sealed class FindAHostTab : IDisposable
{
    private readonly RecruitmentTab _board;

    public FindAHostTab(
        GambaWhereClient client,
        ImageService imageService,
        Configuration config,
        PlayerInfoService playerInfo,
        IChatGui chatGui,
        IPluginLog log)
    {
        _board = new RecruitmentTab("host", client, imageService, config, playerInfo, chatGui, log);
    }

    public void Draw() => _board.Draw();

    public void OnSelected() => _board.RequestRefresh();

    public void Dispose() => _board.Dispose();
}
