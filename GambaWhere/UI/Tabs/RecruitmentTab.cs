using System;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Services;

namespace GambaWhere.UI.Tabs;

/// <summary>
/// Recruitment feature: two browse-and-manage boards, one for venues seeking hosts (Find a Venue)
/// and one for hosts seeking venues (Find a Host). Both share a single board engine and are styled
/// after the Gamba Events grid using the configured primary and secondary colours.
/// </summary>
public class RecruitmentTab : IDisposable
{
    private readonly RecruitmentBoard _venueBoard;
    private readonly RecruitmentBoard _hostBoard;

    public RecruitmentTab(
        GambaWhereClient client,
        ImageCache imageCache,
        Configuration config,
        PlayerInfoService playerInfo,
        ProfileImageStore profileImages,
        IChatGui chatGui,
        IPluginLog log)
    {
        _venueBoard = new RecruitmentBoard("venue", client, imageCache, config, playerInfo, profileImages, chatGui, log);
        _hostBoard = new RecruitmentBoard("host", client, imageCache, config, playerInfo, profileImages, chatGui, log);
    }

    public void DrawFindVenueSection() => _venueBoard.Draw();

    public void DrawFindHostSection() => _hostBoard.Draw();

    public void OnVenueSelected() => _venueBoard.RequestRefresh();

    public void OnHostSelected() => _hostBoard.RequestRefresh();

    public void Dispose()
    {
        _venueBoard.Dispose();
        _hostBoard.Dispose();
    }
}
