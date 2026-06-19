using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using GambaWhere.API;
using GambaWhere.API.Models;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>
/// One recruitment board (either "venue" or "host"). Owns its own paging, filters, auto-refresh and
/// the create/edit form for that post type. Browsing mirrors the Gamba Events grid: server-side
/// paged (12 per page), newest first, with data-centre, game-type, NSFW and bank filters.
/// </summary>
internal sealed class RecruitmentBoard : IDisposable
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);
    private const int PageSize = 12;
    private const float CardWidth = 360f;
    private const float CardRounding = 8f;
    private const float CardPad = 18f;
    private const float AvatarSize = 52f;
    private const float VenueImageSize = 76f;

    private const float BadgeFontScale = 0.82f;

    internal static readonly string[] DaysOfWeek =
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private static readonly string[] CompanionPlugins =
    {
        "SimpleBingo", "SimpleBlackjack", "Chocobo Racing", "Mini Games Emporium",
        "SimpleMiniGames", "SimplePoker", "SimpleRoulette", "SimpleScratch", "SimpleWheel",
        "None", "Other Plugins",
    };

    private static readonly string[] NsfwOptions = { "SFW", "NSFW", "Both" };
    private static readonly string[] BankFilterOptions = { "Bank: Any", "Bank: Yes", "Bank: No" };

    private readonly string _postType;
    private readonly bool _isVenue;
    private readonly GambaWhereClient _client;
    private readonly ImageCache _imageCache;
    private readonly Configuration _config;
    private readonly PlayerInfoService _playerInfo;
    private readonly ProfileImageStore _profileImages;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly ThemedCard _formCard = new();

    private List<RecruitmentPost> _posts = new();
    private bool _hasLoaded;
    private volatile bool _isRefreshing;
    private volatile bool _lastRefreshFailed;
    private DateTime _nextAutoRefreshUtc;
    private int _page = 1;
    private int _totalPages = 1;
    private int _total;
    private bool _hasActiveFilters;
    private string _querySignature = string.Empty;

    private readonly HashSet<string> _selectedGameTypes = new();
    private readonly HashSet<string> _selectedDataCentres = new();
    private bool _includeNsfw;
    private int _bankFilter;

    private string? _detailsId;
    private bool _openDetailsRequested;
    private string? _profilePopupPostId;
    private bool _openProfileRequested;

    private bool _showForm;
    private string? _editingId;
    private bool _isSubmitting;
    private string? _formStatus;
    private volatile bool _isFetchingVenues;

    private string? _formVenueName;
    private string _formDataCentre = "Aether";
    private readonly Dictionary<string, DaySchedule> _formSchedule =
        DaysOfWeek.ToDictionary(d => d, _ => new DaySchedule());
    private bool _formBank;
    private int _formNsfw;
    private string _formDescription = string.Empty;
    private readonly HashSet<string> _formGames = new();
    private string _formDiscord = string.Empty;
    private readonly HashSet<string> _formCompanionPlugins = new();
    private string? _formProfileId;

    public RecruitmentBoard(
        string postType,
        GambaWhereClient client,
        ImageCache imageCache,
        Configuration config,
        PlayerInfoService playerInfo,
        ProfileImageStore profileImages,
        IChatGui chatGui,
        IPluginLog log)
    {
        _postType = postType;
        _isVenue = postType == "venue";
        _client = client;
        _imageCache = imageCache;
        _config = config;
        _playerInfo = playerInfo;
        _profileImages = profileImages;
        _chatGui = chatGui;
        _log = log;

        _includeNsfw = _config.ShowNsfwRecruitment;

        _querySignature = BuildQuerySignature();
        TriggerRefresh(); // one-time startup fetch (lazy load A)
    }

    public void RequestRefresh()
    {
        if (!_isRefreshing)
            TriggerRefresh();
    }

    private string IdScope => _isVenue ? "venue" : "host";

    // Background polling removed; refreshes are triggered by user navigation only.

    public void Draw()
    {
        _includeNsfw = _config.ShowNsfwRecruitment;

        DrawHeader();
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (_showForm)
        {
            DrawForm();
            return;
        }

        ApplyQueryChanges();
        DrawPaginationBar();
        ImGuiHelpers.ScaledDummy(4f);
        DrawPostList();
        DrawDetailsPopup();
        DrawProfilePopup();
    }

    private void DrawHeader()
    {
        if (!_showForm)
            DrawRightControls();
    }

    private void DrawRightControls()
    {
        const float FilterWidth = 140f;
        const float BankWidth = 110f;
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImGui.GetStyle();
        var spacing = style.ItemSpacing.X;

        var gameW = FilterWidth * scale;
        var dcW = FilterWidth * scale;
        var bankW = BankWidth * scale;
        var nsfwW = ImGui.GetFrameHeight() + style.ItemInnerSpacing.X + ImGui.CalcTextSize("Show NSFW").X;
        var createLabel = _isVenue ? "Create Venue Post" : "Create Host Post";
        var createW = UIHelper.CalcButtonSize(FontAwesomeIcon.Plus, createLabel).X;

        var total = gameW + dcW + bankW + nsfwW + createW + spacing * 4f;
        var start = Math.Max(0f, ImGui.GetContentRegionMax().X - total);

        ImGui.SetCursorPosX(start);
        ImGui.SetNextItemWidth(gameW);
        MultiSelectCombo.Draw($"##rec_game_{IdScope}", "Game Type", GambaEventsTab.KnownGameTypes, _selectedGameTypes);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(dcW);
        MultiSelectCombo.Draw($"##rec_dc_{IdScope}", "Data Centre", GambaEventsTab.KnownDataCentres, _selectedDataCentres);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(bankW);
        using (var bankCombo = ImRaii.Combo($"##rec_bank_{IdScope}", BankFilterOptions[_bankFilter]))
        {
            if (bankCombo)
            {
                for (var i = 0; i < BankFilterOptions.Length; i++)
                {
                    if (ImGui.Selectable(BankFilterOptions[i], _bankFilter == i))
                        _bankFilter = i;
                }
            }
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        if (ImGui.Checkbox($"Show NSFW##rec_nsfw_{IdScope}", ref _includeNsfw))
        {
            _config.ShowNsfwRecruitment = _includeNsfw;
            _config.Save();
        }

        ImGui.SameLine();
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, createLabel, $"##rec_create_{IdScope}"))
                BeginCreate();
        }
    }

    private string BuildQuerySignature()
    {
        var games = string.Join(",", _selectedGameTypes.OrderBy(g => g, StringComparer.Ordinal));
        var dcs = string.Join(",", _selectedDataCentres.OrderBy(d => d, StringComparer.Ordinal));
        return $"{games}|{dcs}|{_includeNsfw}|{_bankFilter}";
    }

    private void ApplyQueryChanges()
    {
        var signature = BuildQuerySignature();
        if (signature == _querySignature)
            return;

        _querySignature = signature;
        _page = 1;
        TriggerRefresh();
    }

    private bool? BankFilterValue() => _bankFilter switch
    {
        1 => true,
        2 => false,
        _ => null,
    };

    private void GoToPage(int page)
    {
        var clamped = Math.Clamp(page, 1, Math.Max(1, _totalPages));
        if (clamped == _page)
            return;

        _page = clamped;
        TriggerRefresh();
    }

    private void TriggerRefresh()
    {
        if (_cts.IsCancellationRequested)
            return;

        _isRefreshing = true;
        _nextAutoRefreshUtc = DateTime.UtcNow + AutoRefreshInterval;
        var ct = _cts.Token;

        var page = _page;
        var gameTypes = _selectedGameTypes.Count > 0 ? _selectedGameTypes.ToArray() : null;
        var dataCentres = _selectedDataCentres.Count > 0 ? _selectedDataCentres.ToArray() : null;
        var includeNsfw = _includeNsfw;
        var bank = BankFilterValue();
        var hasFilters = gameTypes != null || dataCentres != null || bank != null;

        // _log.Information("[GambaWhere/Recruitment] GET /recruitment/page (postType={PostType}, page={Page}, pageSize={PageSize})", _postType, page, PageSize);

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _client.GetRecruitmentPageAsync(
                    _postType, page, PageSize, gameTypes, dataCentres, includeNsfw, bank, ct);
                if (ct.IsCancellationRequested)
                    return;

                if (result == null)
                {
                    _posts = new List<RecruitmentPost>();
                    _lastRefreshFailed = true;
                }
                else
                {
                    _posts = new List<RecruitmentPost>(result.Items);
                    _total = result.Total;
                    _totalPages = Math.Max(1, result.TotalPages);
                    _hasActiveFilters = hasFilters;
                    _lastRefreshFailed = false;

                    if (_page > _totalPages)
                    {
                        _page = _totalPages;
                        _isRefreshing = false;
                        _hasLoaded = true;
                        TriggerRefresh();
                        return;
                    }
                }

                _hasLoaded = true;
                _isRefreshing = false;
            }
            catch (OperationCanceledException)
            {
                _isRefreshing = false;
            }
        }, ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private void DrawPaginationBar()
    {
        if (!_hasLoaded || _total <= 0)
            return;

        var refreshing = _isRefreshing;
        var pageText = $"Page {_page} of {_totalPages}";
        var countText = $"({_total} post{(_total == 1 ? "" : "s")})";

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth =
            UIHelper.CalcButtonSize(FontAwesomeIcon.AngleDoubleLeft, "First").X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleLeft, "Prev").X + spacing
            + ImGui.CalcTextSize(pageText).X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleRight, "Next").X + spacing
            + UIHelper.CalcButtonSize(FontAwesomeIcon.AngleDoubleRight, "Last").X + spacing
            + ImGui.CalcTextSize(countText).X;

        var offset = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;
        if (offset > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        using (ImRaii.Disabled(refreshing || _page <= 1))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleLeft, "First", $"##rec_first_{IdScope}"))
                GoToPage(1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleLeft, "Prev", $"##rec_prev_{IdScope}"))
                GoToPage(_page - 1);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(pageText);

        ImGui.SameLine();
        using (ImRaii.Disabled(refreshing || _page >= _totalPages))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleRight, "Next", $"##rec_next_{IdScope}"))
                GoToPage(_page + 1);
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.AngleDoubleRight, "Last", $"##rec_last_{IdScope}"))
                GoToPage(_totalPages);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(countText);
    }

    private void DrawPostList()
    {
        if (_lastRefreshFailed)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Server error fetching posts, please try again later.");
            return;
        }

        if (!_hasLoaded)
        {
            ImGui.TextDisabled("No posts loaded yet - press Refresh above.");
            return;
        }

        if (_posts.Count == 0)
        {
            ImGui.TextDisabled(_hasActiveFilters
                ? "No posts match the current filters."
                : _isVenue ? "No venues are recruiting right now." : "No hosts are looking right now.");
            return;
        }

        using var scroll = ImRaii.Child($"##rec_grid_{IdScope}", Vector2.Zero, false);
        if (scroll.Success)
            DrawCardGrid();
    }

    private void DrawCardGrid()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var margin = 10f * scale;

        ImGui.Indent(margin);
        ImGuiHelpers.ScaledDummy(2f);

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X - margin;
        var cardWidth = CardWidth * scale;
        var cardHeight = ComputeCardHeight();
        var columns = Math.Max(1, (int)((avail + spacing) / (cardWidth + spacing)));

        var mine = MyPostTokens();

        var col = 0;
        foreach (var post in _posts)
        {
            if (col > 0)
                ImGui.SameLine(0f, spacing);

            DrawCard(post, cardWidth, cardHeight, mine.ContainsKey(post.Id));

            col++;
            if (col >= columns)
                col = 0;
        }

        ImGui.Unindent(margin);
    }

    private float ComputeCardHeight()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var line = ImGui.GetTextLineHeightWithSpacing();
        return CardPad * 2f * scale
            + (_isVenue ? VenueImageSize : AvatarSize) * scale
            + line * 2f
            + (line + 8f * scale)
            + ImGui.GetFrameHeight()
            + 44f * scale;
    }

    private void DrawCard(RecruitmentPost post, float cardWidth, float cardHeight, bool isMine)
    {
        using var id = ImRaii.PushId($"{IdScope}_{post.Id}");

        var scale = ImGuiHelpers.GlobalScale;
        var pad = CardPad * scale;
        var rounding = CardRounding * scale;
        var accent = _config.SecondaryColour;
        var booster = post.Booster;

        var cardBg = booster ? BoosterCardEffect.BaseColour : ThemeColours.CardBackground(_config.PrimaryColour);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, rounding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        var child = ImRaii.Child("##card", new Vector2(cardWidth, cardHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        using (child)
        {
            if (child.Success)
            {
                var p0 = ImGui.GetWindowPos();
                var sz = ImGui.GetWindowSize();
                var dl = ImGui.GetWindowDrawList();
                var contentWidth = sz.X - pad * 2f;

                if (booster)
                    BoosterCardEffect.DrawHolographicFill(dl, p0, p0 + sz, ImGui.GetTime(), BoosterCardEffect.Seed(post.Id));

                var manageW = isMine ? 2f * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X : 0f;
                var rightReserve = Math.Max(manageW, MaxCornerBadgeWidth(post)) + 8f * scale;

                ImGui.SetCursorPos(new Vector2(pad, pad));
                DrawCardHeader(post, contentWidth, rightReserve);

                ImGuiHelpers.ScaledDummy(_isVenue ? 2f : 8f);
                ImGui.SetCursorPosX(pad);
                DrawContentSeparator(contentWidth);
                ImGuiHelpers.ScaledDummy(_isVenue ? 4f : 8f);

                ImGui.SetCursorPosX(pad);
                DrawCardDescription(post, contentWidth);
                ImGuiHelpers.ScaledDummy(6f);

                ImGui.SetCursorPosX(pad);
                DrawCardGames(post, contentWidth);

                ImGui.SetCursorPos(new Vector2(pad, sz.Y - pad - ImGui.GetFrameHeight()));
                DrawCardButtons(post, contentWidth);

                if (isMine)
                    DrawCardManageButtons(post, sz, pad);

                DrawCornerBadges(post, p0, sz, pad, isMine);

                if (booster)
                    BoosterCardEffect.DrawHolographicBorder(dl, p0, p0 + sz, rounding, ImGui.GetTime());
                else
                    dl.AddRect(p0, p0 + sz, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
                        rounding, ImDrawFlags.None, 1.5f * scale);
            }
        }
    }

    private void DrawCardHeader(RecruitmentPost post, float contentWidth, float rightReserve)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avatar = (_isVenue ? VenueImageSize : AvatarSize) * scale;
        var gap = 12f * scale;
        var imageRounding = _isVenue ? 4f * scale : avatar * 0.5f;

        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        if (_isVenue)
        {
            var tex = !string.IsNullOrWhiteSpace(post.ImageUrl) ? _imageCache.Get(post.ImageUrl!) : null;
            if (tex != null)
                dl.AddImageRounded(tex.Handle, pos, pos + new Vector2(avatar, avatar), Vector2.Zero, Vector2.One,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), imageRounding);
            else
                CircleImage.DrawPlaceholderAt(dl, pos, avatar);
        }
        else
        {
            var tex = !string.IsNullOrWhiteSpace(post.ProfileImageUrl) ? _imageCache.GetProfile(post.ProfileImageUrl!) : null;
            if (tex != null)
                CircleImage.DrawAt(dl, pos, avatar, tex);
            else
                CircleImage.DrawPlaceholderAt(dl, pos, avatar);
        }

        if (post.Booster && !_isVenue)
            DrawBoosterRing(dl, pos, avatar);

        ImGui.Dummy(new Vector2(avatar, avatar));
        ImGui.SameLine(0f, gap);

        var textWidth = Math.Max(60f * scale, contentWidth - avatar - gap - rightReserve);
        using (ImRaii.Group())
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);

            var title = _isVenue
                ? (string.IsNullOrWhiteSpace(post.VenueName) ? "Venue" : post.VenueName!)
                : FormatNameWorld(post.PosterCharacter);
            ImGui.TextColored(_config.SecondaryColour, title);

            ImGui.TextDisabled(post.DataCentre);

            ImGui.PopTextWrapPos();
        }

        if (!_isVenue && HasRecruitmentProfile(post))
        {
            var headerMin = pos;
            var headerMax = new Vector2(pos.X + avatar + gap + textWidth, pos.Y + avatar);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(headerMin, headerMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                dl.AddRectFilled(headerMin, headerMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), 4f * scale);
                ImGui.SetTooltip("View profile");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    _profilePopupPostId = post.Id;
                    _openProfileRequested = true;
                }
            }
        }
    }

    private static bool HasRecruitmentProfile(RecruitmentPost post) =>
        !string.IsNullOrWhiteSpace(post.ProfileImageUrl)
        || !string.IsNullOrWhiteSpace(post.Bio)
        || post.PreferredGames.Count > 0
        || post.Booster;

    private void DrawBoosterRing(ImDrawListPtr dl, Vector2 pos, float size)
    {
        var tex = _imageCache.GetBundledImage("Profile Borders/boosterborder.png");
        if (tex == null)
            return;

        var centre = pos + new Vector2(size * 0.5f, size * 0.5f);
        var half = size * 0.58f;
        dl.AddImage(tex.Handle, centre - new Vector2(half, half), centre + new Vector2(half, half),
            Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
    }

    private static Vector4 BankBadgeColour => new(0.36f, 0.8f, 0.46f, 1f);

    private static Vector4 NsfwBadgeColour(string nsfw) => nsfw switch
    {
        "NSFW" => new Vector4(1f, 0.45f, 0.45f, 1f),
        "Both" => new Vector4(1f, 0.72f, 0.32f, 1f),
        _ => new Vector4(0.45f, 0.75f, 1f, 1f),
    };

    private string BankBadgeText => _isVenue ? "Bank Offered" : "Bank Wanted";

    private float MaxCornerBadgeWidth(RecruitmentPost post)
    {
        var nsfwW = ChipWidth(post.Nsfw);
        return post.Bank ? Math.Max(nsfwW, ChipWidth(BankBadgeText)) : nsfwW;
    }

    private static float ChipWidth(string text) =>
        ImGui.CalcTextSize(text).X * BadgeFontScale + 6f * ImGuiHelpers.GlobalScale * 2f;

    private void DrawCornerBadges(RecruitmentPost post, Vector2 cardPos, Vector2 cardSize, float pad, bool isMine)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rightX = cardPos.X + cardSize.X - pad;
        var y = cardPos.Y + pad;

        if (isMine)
            y += ImGui.GetFrameHeight() + 6f * scale;

        if (post.Bank)
            y += DrawCornerChip(dl, rightX, y, BankBadgeText, BankBadgeColour) + 4f * scale;

        DrawCornerChip(dl, rightX, y, post.Nsfw, NsfwBadgeColour(post.Nsfw));
    }

    private static float DrawCornerChip(ImDrawListPtr dl, float rightX, float y, string text, Vector4 colour)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * BadgeFontScale;
        var hpad = 6f * scale;
        var vpad = 2f * scale;
        var textSize = ImGui.CalcTextSize(text) * BadgeFontScale;
        var size = new Vector2(textSize.X + hpad * 2f, textSize.Y + vpad * 2f);
        var pos = new Vector2(rightX - size.X, y);
        var rounding = size.Y * 0.5f;

        dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(colour.X, colour.Y, colour.Z, 0.10f)), rounding);
        dl.AddText(font, fontSize, new Vector2(pos.X + hpad, pos.Y + vpad),
            ImGui.GetColorU32(new Vector4(colour.X, colour.Y, colour.Z, 0.82f)), text);
        return size.Y;
    }

    private void DrawCardDescription(RecruitmentPost post, float contentWidth)
    {
        if (string.IsNullOrWhiteSpace(post.Description))
            return;

        var maxHeight = 2f * ImGui.GetTextLineHeight() + 1f;
        var text = UIHelper.TruncateToFit(post.Description!, contentWidth, maxHeight);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.82f, 0.82f, 0.86f, 1f)))
            ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
    }

    private void DrawContentSeparator(float contentWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var c = _config.SecondaryColour;
        dl.AddLine(p, new Vector2(p.X + contentWidth, p.Y), ImGui.GetColorU32(new Vector4(c.X, c.Y, c.Z, 0.22f)), 1f * scale);
        ImGui.Dummy(new Vector2(contentWidth, 1f * scale));
    }

    private void DrawCardGames(RecruitmentPost post, float contentWidth)
    {
        if (post.Games.Count == 0)
            return;

        var shown = new List<string>();
        var widthUsed = 0f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        foreach (var g in post.Games)
        {
            var w = GamePill.CalcSize(g).X;
            if (shown.Count > 0)
                widthUsed += spacing;
            widthUsed += w;
            if (widthUsed > contentWidth && shown.Count > 0)
                break;
            shown.Add(g);
        }

        GamePill.DrawList(shown, contentWidth);
    }

    private void DrawCardButtons(RecruitmentPost post, float rowWidth)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonWidth = (rowWidth - spacing) / 2f;
        var buttonSize = new Vector2(buttonWidth, ImGui.GetFrameHeight());

        using (ImRaii.PushColor(ImGuiCol.Button, ThemeColours.ButtonNormal(_config.PrimaryColour))
                   .Push(ImGuiCol.ButtonHovered, ThemeColours.ButtonHovered(_config.PrimaryColour))
                   .Push(ImGuiCol.ButtonActive, ThemeColours.ButtonPressed(_config.PrimaryColour)))
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.InfoCircle, "Details", buttonSize, "##rec_details"))
            {
                _detailsId = post.Id;
                _openDetailsRequested = true;
            }

            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, "Contact", buttonSize, "##rec_contact"))
                ChatInput.CopyTellToClipboard(post.PosterCharacter, _chatGui);
        }
    }

    private void DrawCardManageButtons(RecruitmentPost post, Vector2 cardSize, float pad)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var bw = ImGui.GetFrameHeight();
        var totalW = bw * 2f + spacing;

        ImGui.SetCursorPos(new Vector2(cardSize.X - pad - totalW, pad));

        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
                   .Push(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.12f))
                   .Push(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.20f)))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.96f, 0.66f, 0.13f, 1f)))
            {
                if (ImGuiComponents.IconButton("##rec_edit", FontAwesomeIcon.Pen))
                    BeginEdit(post);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Edit post");

            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.92f, 0.34f, 0.34f, 1f)))
            {
                if (ImGuiComponents.IconButton("##rec_delete", FontAwesomeIcon.Trash))
                    DeletePost(post.Id);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Delete post");
        }
    }

    private void DrawDetailsPopup()
    {
        var popupId = $"##rec_details_popup_{IdScope}";
        if (_openDetailsRequested)
        {
            ImGui.OpenPopup(popupId);
            _openDetailsRequested = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var post = _posts.FirstOrDefault(p => p.Id == _detailsId);
        var booster = post?.Booster ?? false;
        var rounding = CardRounding * scale;
        var cardBg = booster ? BoosterCardEffect.BaseColour : ThemeColours.TintedWindowBg(_config.PrimaryColour);

        ImGui.SetNextWindowSizeConstraints(new Vector2(420f * scale, 0f), new Vector2(560f * scale, float.MaxValue));

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var colBg = ImRaii.PushColor(ImGuiCol.PopupBg, cardBg, post != null);
        using var popup = ImRaii.Popup(popupId);
        if (!popup.Success)
            return;

        if (post == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var accent = _config.SecondaryColour;

        var title = _isVenue
            ? (string.IsNullOrWhiteSpace(post.VenueName) ? "Venue" : post.VenueName!)
            : FormatNameWorld(post.PosterCharacter);
        ImGui.TextColored(accent, title);
        ImGui.TextDisabled(_isVenue
            ? $"{post.DataCentre}  -  Posted by {FormatNameWorld(post.PosterCharacter)}"
            : post.DataCentre);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        var width = ImGui.GetContentRegionAvail().X;

        if (!string.IsNullOrWhiteSpace(post.Description))
        {
            SectionHeader("Description");
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
            ImGui.TextWrapped(post.Description!);
            ImGui.PopTextWrapPos();
            ImGuiHelpers.ScaledDummy(8f);
        }

        SectionHeader("Info");
        DetailLine(_isVenue ? "Bank offered" : "Bank wanted", post.Bank ? "Yes" : "No");
        DetailLine("Audience", post.Nsfw);
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.TextDisabled(_isVenue ? "Games needed" : "Games offered");
        if (post.Games.Count > 0)
            GamePill.DrawList(post.Games, width);
        else
            ImGui.TextDisabled("None listed.");

        if (!_isVenue && post.CompanionPlugins.Count > 0)
        {
            ImGuiHelpers.ScaledDummy(2f);
            ImGui.TextDisabled("Companion plugins");
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
            ImGui.TextWrapped(string.Join(", ", post.CompanionPlugins));
            ImGui.PopTextWrapPos();
        }
        ImGuiHelpers.ScaledDummy(8f);

        SectionHeader("Availability (Server Time)");
        if (post.Schedule.Count > 0)
        {
            foreach (var e in post.Schedule)
                ImGui.TextUnformatted(FormatEntry(e, true) + " ST");
        }
        else
        {
            ImGui.TextDisabled("Not specified.");
        }
        ImGuiHelpers.ScaledDummy(8f);

        SectionHeader("Contact");
        if (!string.IsNullOrWhiteSpace(post.Discord))
        {
            ImGui.TextDisabled("Discord");
            ImGui.SameLine();
            DrawDiscordValue(post.Discord!);
        }

        ImGuiHelpers.ScaledDummy(6f);
        if (UIHelper.IconTextButton(FontAwesomeIcon.CommentDots, "Contact (copy /tell)", "##rec_details_contact"))
            ChatInput.CopyTellToClipboard(post.PosterCharacter, _chatGui);

        ImGui.SameLine();
        if (ImGui.Button("Close##rec_details_close"))
            ImGui.CloseCurrentPopup();

        var p0 = ImGui.GetWindowPos();
        var sz = ImGui.GetWindowSize();
        dl.ChannelsSetCurrent(0);
        dl.PushClipRect(new Vector2(-10000f, -10000f), new Vector2(100000f, 100000f), false);
        if (booster)
        {
            BoosterCardEffect.DrawHolographicFoil(dl, p0, p0 + sz, ImGui.GetTime());
            BoosterCardEffect.DrawHolographicBorder(dl, p0, p0 + sz, rounding, ImGui.GetTime());
        }
        else
        {
            dl.AddRect(p0, p0 + sz, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
                rounding, ImDrawFlags.None, 1.5f * scale);
        }
        dl.PopClipRect();
        dl.ChannelsMerge();
    }

    private void DrawProfilePopup()
    {
        var post = _isVenue ? null : _posts.FirstOrDefault(p => p.Id == _profilePopupPostId);
        var data = post == null
            ? null
            : new ProfilePopup.Data
            {
                DisplayName = FormatNameWorld(post.PosterCharacter),
                ProfileImageUrl = post.ProfileImageUrl,
                Bio = post.Bio,
                PreferredGames = post.PreferredGames,
                Booster = post.Booster,
            };

        ProfilePopup.Draw($"##rec_profile_popup_{IdScope}", ref _openProfileRequested, _imageCache, _config, data);
    }

    private void SectionHeader(string text)
    {
        ImGui.TextColored(_config.SecondaryColour, text);
        ImGuiHelpers.ScaledDummy(2f);
    }

    private void DetailLine(string label, string value)
    {
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
    }

    private void DrawDiscordValue(string discord)
    {
        var isUrl = discord.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || discord.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || discord.Contains("discord.gg", StringComparison.OrdinalIgnoreCase)
                    || discord.Contains("discord.com", StringComparison.OrdinalIgnoreCase);

        if (isUrl)
        {
            var url = discord.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? discord : "https://" + discord;
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 1f, 1f)))
                ImGui.TextUnformatted(discord);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip($"Open in browser:\n{url}");
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                OpenBrowser.TryOpen(url);
            return;
        }

        ImGui.TextUnformatted(discord);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Click to copy");
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ImGui.SetClipboardText(discord);
    }

    private void BeginCreate()
    {
        ResetForm();
        _editingId = null;
        _formProfileId = _config.SelectedProfileId;
        _formCompanionPlugins.Add("None");
        _showForm = true;
        _formStatus = null;
        FetchVenues();
    }

    private void BeginEdit(RecruitmentPost post)
    {
        ResetForm();
        _editingId = post.Id;
        _formVenueName = post.VenueName;
        _formDataCentre = post.DataCentre;
        foreach (var entry in post.Schedule)
        {
            if (!_formSchedule.TryGetValue(entry.Day, out var ds))
                continue;
            ds.On = true;
            ds.StartHour = entry.StartHour;
            ds.StartMinute = entry.StartMinute;
            ds.EndHour = entry.EndHour;
            ds.EndMinute = entry.EndMinute;
        }
        _formBank = post.Bank;
        _formNsfw = Math.Max(0, Array.IndexOf(NsfwOptions, post.Nsfw));
        _formDescription = post.Description ?? string.Empty;
        foreach (var g in post.Games) _formGames.Add(g);
        _formDiscord = post.Discord ?? string.Empty;
        foreach (var c in post.CompanionPlugins) _formCompanionPlugins.Add(c);
        _formProfileId = _config.SelectedProfileId;
        _showForm = true;
        _formStatus = null;
        FetchVenues();
    }

    private void FetchVenues()
    {
        if (!_isVenue || _isFetchingVenues)
            return;

        _isFetchingVenues = true;
        _ = Task.Run(async () =>
        {
            var venues = await _client.GetVenuesAsync();
            VenueSearchCombo.SetVenues(venues);
            _isFetchingVenues = false;
        });
    }

    private void ResetForm()
    {
        _formVenueName = null;
        _formDataCentre = "Aether";
        foreach (var ds in _formSchedule.Values)
            ds.Reset();
        _formBank = false;
        _formNsfw = 0;
        _formDescription = string.Empty;
        _formGames.Clear();
        _formDiscord = string.Empty;
        _formCompanionPlugins.Clear();
        _formProfileId = null;
    }

    private void DrawForm()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var footerHeight = 76f * scale;
        var scrollHeight = Math.Max(80f * scale, ImGui.GetContentRegionAvail().Y - footerHeight);

        HostFieldTheme.Primary = _config.PrimaryColour;
        HostFieldTheme.Secondary = _config.SecondaryColour;

        using (var scroll = ImRaii.Child($"##rec_form_scroll_{IdScope}", new Vector2(0f, scrollHeight), false))
        {
            if (scroll.Success)
            {
                ImGuiHelpers.ScaledDummy(4f);

                if (_isVenue)
                    DrawPanel("##rec_panel_venue", "Venue", DrawVenueFields);
                else
                    DrawPanel("##rec_panel_host", "Host Profile", DrawHostFields);

                ImGuiHelpers.ScaledDummy(8f);
                DrawPanel("##rec_panel_schedule", "Schedule", DrawSchedulePanel);

                ImGuiHelpers.ScaledDummy(8f);
                DrawPanel("##rec_panel_details", "Details", DrawDetailsPanel);

                ImGuiHelpers.ScaledDummy(8f);
            }
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        DrawFormFooter();
    }

    private void DrawPanel(string id, string title, Action body) =>
        _formCard.Draw(id, title, _config.PrimaryColour, _config.SecondaryColour, body);

    private void DrawVenueFields()
    {
        ImGui.SetNextItemWidth(-1);
        var venue = _formVenueName;
        if (VenueSearchCombo.Draw($"##rec_venue_{IdScope}", ref venue, _config.FavouriteVenues, () => _config.Save()))
            _formVenueName = venue;

        if (_isFetchingVenues)
            ImGui.TextDisabled("Refreshing venue list...");
    }

    private void DrawHostFields()
    {
        var character = _playerInfo.GetCharacterName();
        HostField.Label("Posting as");
        ImGui.TextUnformatted(character != null ? FormatNameWorld(character) : "Not logged in");

        ImGuiHelpers.ScaledDummy(6f);
        DrawProfilePicker();

        ImGuiHelpers.ScaledDummy(6f);
        HostField.Label("Companion plugins");
        DrawCompanionPluginsCombo();

        ImGuiHelpers.ScaledDummy(6f);
        HostField.Label("Discord username (optional)");
        ImGui.SetNextItemWidth(280f * ImGuiHelpers.GlobalScale);
        ImGui.InputText($"##rec_discord_{IdScope}", ref _formDiscord, 64);
    }

    private void DrawCompanionPluginsCombo()
    {
        var preview = _formCompanionPlugins.Count switch
        {
            0 => "None",
            1 => _formCompanionPlugins.First(),
            _ => $"{_formCompanionPlugins.Count} selected",
        };

        ImGui.SetNextItemWidth(280f * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo($"##rec_plugins_{IdScope}", preview);
        if (!combo)
            return;

        foreach (var plugin in CompanionPlugins)
        {
            var isSelected = _formCompanionPlugins.Contains(plugin);
            if (ImGui.Selectable(plugin, isSelected, ImGuiSelectableFlags.DontClosePopups))
                ToggleCompanionPlugin(plugin, isSelected);
        }
    }

    private void ToggleCompanionPlugin(string plugin, bool wasSelected)
    {
        if (plugin == "None")
        {
            _formCompanionPlugins.Clear();
            if (!wasSelected)
                _formCompanionPlugins.Add("None");
            return;
        }

        if (wasSelected)
        {
            _formCompanionPlugins.Remove(plugin);
        }
        else
        {
            _formCompanionPlugins.Remove("None");
            _formCompanionPlugins.Add(plugin);
        }
    }

    private void DrawProfilePicker()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var previewSize = 48f * scale;
        var selected = _config.Profiles.FirstOrDefault(p => p.Id == _formProfileId);

        var path = selected != null ? _profileImages.GetPath(selected.ImageFileName) : null;
        var tex = path != null ? _imageCache.GetFromPath(path) : null;

        var startY = ImGui.GetCursorPosY();
        CircleImage.DrawInline(previewSize, tex);
        ImGui.SameLine();

        var groupHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight();
        ImGui.SetCursorPosY(startY + Math.Max(0f, (previewSize - groupHeight) * 0.5f));

        using (ImRaii.Group())
        {
            HostField.Label("Profile picture");
            ImGui.SetNextItemWidth(200f * scale);
            using var combo = ImRaii.Combo($"##rec_profile_{IdScope}", selected?.Name ?? "None");
            if (combo)
            {
                if (ImGui.Selectable("None", selected == null))
                    _formProfileId = null;

                foreach (var profile in _config.Profiles)
                {
                    if (ImGui.Selectable(profile.Name, profile.Id == _formProfileId))
                        _formProfileId = profile.Id;
                }
            }
        }

        if (selected == null && _config.Profiles.Count == 0)
            ImGui.TextDisabled("Create one on the Profiles tab.");
    }

    private void DrawSchedulePanel()
    {
        ImGui.TextDisabled(_isVenue
            ? "Tick the days you are open and set the hours (Server Time)."
            : "Tick the days you can host and set the hours (Server Time).");
        ImGuiHelpers.ScaledDummy(4f);
        DrawScheduleEditor();
    }

    private void DrawDetailsPanel()
    {
        var scale = ImGuiHelpers.GlobalScale;

        HostField.Label("Data Centre");
        if (_isVenue)
        {
            ImGui.SetNextItemWidth(200f * scale);
            using var combo = ImRaii.Combo($"##rec_form_dc_{IdScope}", _formDataCentre);
            if (combo)
            {
                foreach (var dc in GambaEventsTab.KnownDataCentres)
                {
                    if (ImGui.Selectable(dc, dc == _formDataCentre))
                        _formDataCentre = dc;
                }
            }
        }
        else
        {
            ImGui.TextUnformatted(_playerInfo.GetHomeDataCentre() ?? "Unknown (log in to detect)");
        }

        ImGuiHelpers.ScaledDummy(6f);
        var bank = _formBank;
        if (HostField.Toggle(_isVenue ? "Offer a bank?" : "Want a bank provided?", $"##rec_bank_toggle_{IdScope}", ref bank))
            _formBank = bank;

        ImGuiHelpers.ScaledDummy(6f);
        HostField.Label("Audience");
        for (var i = 0; i < NsfwOptions.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.RadioButton($"{NsfwOptions[i]}##rec_nsfw_opt_{IdScope}_{i}", _formNsfw == i))
                _formNsfw = i;
        }

        ImGuiHelpers.ScaledDummy(6f);
        HostField.Label(_isVenue ? "Games you need hosted" : "Games you want to host");
        GamePill.DrawSelector(GambaEventsTab.KnownGameTypes, _formGames, ImGui.GetContentRegionAvail().X, IdScope);

        ImGuiHelpers.ScaledDummy(6f);
        HostField.Label("Short description");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextMultiline($"##rec_desc_{IdScope}", ref _formDescription, 256, new Vector2(0, 60f * scale));
        var count = _formDescription.Length;
        var over = count > 255;
        ImGui.TextColored(over ? new Vector4(1f, 0.2f, 0.2f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f), $"{count} / 255");
    }

    private void DrawScheduleEditor()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var intWidth = 38f * scale;

        for (var i = 0; i < DaysOfWeek.Length; i++)
        {
            var day = DaysOfWeek[i];
            var ds = _formSchedule[day];

            var on = ds.On;
            if (ImGui.Checkbox($"{day}##rec_day_{IdScope}_{i}", ref on))
                ds.On = on;

            if (!ds.On)
                continue;

            ImGui.SameLine(120f * scale);
            ds.StartHour = DrawTimeInt($"##sh_{IdScope}_{i}", ds.StartHour, 23, intWidth);
            ImGui.SameLine(0f, 2f); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted(":");
            ImGui.SameLine(0f, 2f);
            ds.StartMinute = DrawTimeInt($"##sm_{IdScope}_{i}", ds.StartMinute, 59, intWidth);

            ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("to");
            ImGui.SameLine();
            ds.EndHour = DrawTimeInt($"##eh_{IdScope}_{i}", ds.EndHour, 23, intWidth);
            ImGui.SameLine(0f, 2f); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted(":");
            ImGui.SameLine(0f, 2f);
            ds.EndMinute = DrawTimeInt($"##em_{IdScope}_{i}", ds.EndMinute, 59, intWidth);

            ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.TextDisabled("ST");
        }
    }

    private static int DrawTimeInt(string id, int value, int max, float width)
    {
        ImGui.SetNextItemWidth(width);
        var v = value;
        ImGui.InputInt(id, ref v, 0);
        return Math.Clamp(v, 0, max);
    }

    private void DrawFormFooter()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;

        if (!string.IsNullOrEmpty(_formStatus))
        {
            var msgW = ImGui.CalcTextSize(_formStatus).X;
            ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - msgW) * 0.5f));
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _formStatus);
        }

        var createSize = new Vector2(200f * scale, 46f * scale);
        var cancelSize = new Vector2(150f * scale, 46f * scale);
        var total = createSize.X + spacing + cancelSize.X;
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - total) * 0.5f));

        var label = _isSubmitting ? "Saving..." : (_editingId == null ? "Create Post" : "Save Changes");
        using (ImRaii.Disabled(_isSubmitting))
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Check, label, createSize, $"##rec_submit_{IdScope}"))
                Submit();
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(_isSubmitting))
        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", cancelSize, $"##rec_cancel_{IdScope}"))
            {
                _showForm = false;
                _formStatus = null;
            }
        }
    }

    private bool ValidateForm(out string error)
    {
        error = string.Empty;

        if (!_isVenue && string.IsNullOrWhiteSpace(_playerInfo.GetCharacterName()))
        {
            error = "You must be logged in to create a host post.";
            return false;
        }

        if (_isVenue && string.IsNullOrWhiteSpace(_formVenueName))
        {
            error = "Please select a venue.";
            return false;
        }

        if (_formDescription.Length > 255)
        {
            error = "Description must be 255 characters or fewer.";
            return false;
        }

        if (UserTextGuard.ContainsDisallowedContent(_formDescription)
            || UserTextGuard.ContainsDisallowedContent(_formDiscord))
        {
            error = "Fields must not contain URLs or HTML.";
            return false;
        }

        return true;
    }

    private void Submit()
    {
        if (!ValidateForm(out var error))
        {
            _formStatus = error;
            return;
        }

        _formStatus = null;
        _isSubmitting = true;

        if (_editingId == null)
            SubmitCreate();
        else
            SubmitEdit(_editingId);
    }

    private void SubmitCreate()
    {
        var poster = _playerInfo.GetCharacterName();
        if (string.IsNullOrWhiteSpace(poster))
        {
            _formStatus = "You must be logged in to create a post.";
            _isSubmitting = false;
            return;
        }

        var request = new PostRecruitmentRequest
        {
            PostType = _postType,
            PosterCharacter = poster,
            DataCentre = ResolveDataCentre(),
            Nsfw = NsfwOptions[_formNsfw],
            Bank = _formBank,
            Schedule = BuildSchedule(),
            Description = NullIfBlank(_formDescription),
            Games = _formGames.ToList(),
            BoosterKey = BoosterKeyForRequest(),
        };

        GambaProfile? profile = null;
        string? sentHash = null;
        if (_isVenue)
        {
            request.VenueName = _formVenueName;
        }
        else
        {
            request.Discord = NullIfBlank(_formDiscord);
            request.CompanionPlugins = _formCompanionPlugins.ToList();
            profile = GetFormProfile();
            sentHash = AttachHostProfile(request, profile);
        }

        _ = Task.Run(async () =>
        {
            var (err, created) = await _client.PostRecruitmentAsync(request);
            _isSubmitting = false;

            if (err != null || created == null)
            {
                _formStatus = err ?? "Failed to create post.";
                return;
            }

            PersistUploadedImage(profile, sentHash, created.ProfileImageUrl);

            _config.RecruitmentPosts.Add(new RecruitmentPostToken
            {
                Id = created.Id,
                PostType = _postType,
                PosterCharacter = created.PosterCharacter,
                SessionToken = created.SessionToken,
            });
            _config.Save();

            _showForm = false;
            TriggerRefresh();
        });
    }

    private void SubmitEdit(string id)
    {
        var token = MyPostTokens().GetValueOrDefault(id);
        if (string.IsNullOrEmpty(token))
        {
            _formStatus = "You can no longer edit this post on this device.";
            _isSubmitting = false;
            return;
        }

        var request = new PutRecruitmentRequest
        {
            DataCentre = ResolveDataCentre(),
            Nsfw = NsfwOptions[_formNsfw],
            Bank = _formBank,
            Schedule = BuildSchedule(),
            Description = NullIfBlank(_formDescription),
            Games = _formGames.ToList(),
            BoosterKey = BoosterKeyForRequest(),
        };

        GambaProfile? profile = null;
        string? sentHash = null;
        if (_isVenue)
        {
            request.VenueName = _formVenueName;
        }
        else
        {
            request.Discord = _formDiscord ?? string.Empty;
            request.CompanionPlugins = _formCompanionPlugins.ToList();
            profile = GetFormProfile();
            sentHash = AttachHostProfileForEdit(request, profile);
        }

        _ = Task.Run(async () =>
        {
            var (err, updated) = await _client.PutRecruitmentAsync(id, token, request);
            _isSubmitting = false;

            if (err != null || updated == null)
            {
                _formStatus = err ?? "Failed to update post.";
                return;
            }

            PersistUploadedImage(profile, sentHash, updated.ProfileImageUrl);

            _showForm = false;
            TriggerRefresh();
        });
    }

    private void DeletePost(string id)
    {
        var token = MyPostTokens().GetValueOrDefault(id);
        if (string.IsNullOrEmpty(token))
            return;

        _ = Task.Run(async () =>
        {
            var ok = await _client.DeleteRecruitmentAsync(id, token);
            if (!ok)
                return;

            _config.RecruitmentPosts.RemoveAll(p => p.Id == id);
            _config.Save();
            TriggerRefresh();
        });
    }

    private GambaProfile? GetFormProfile() =>
        _config.Profiles.FirstOrDefault(p => p.Id == _formProfileId);

    private string? AttachHostProfile(PostRecruitmentRequest request, GambaProfile? profile)
    {
        if (profile == null)
            return null;

        request.Bio = string.IsNullOrWhiteSpace(profile.Bio) ? null : profile.Bio.Trim();
        request.PreferredGames = new List<string>(profile.PreferredGames);

        var path = _profileImages.GetPath(profile.ImageFileName);
        if (path == null || !ProfileImageEncoder.TryEncode(path, out var b64, out var hash))
            return null;

        if (!string.IsNullOrEmpty(profile.UploadedImageUrl) && profile.UploadedImageHash == hash)
        {
            request.ProfileImageUrl = profile.UploadedImageUrl;
            return null;
        }

        request.ProfilePictureB64 = b64;
        return hash;
    }

    private string? AttachHostProfileForEdit(PutRecruitmentRequest request, GambaProfile? profile)
    {
        if (profile == null)
            return null;

        request.Bio = string.IsNullOrWhiteSpace(profile.Bio) ? null : profile.Bio.Trim();
        request.PreferredGames = new List<string>(profile.PreferredGames);

        var path = _profileImages.GetPath(profile.ImageFileName);
        if (path == null || !ProfileImageEncoder.TryEncode(path, out var b64, out var hash))
            return null;

        if (!string.IsNullOrEmpty(profile.UploadedImageUrl) && profile.UploadedImageHash == hash)
        {
            request.ProfileImageUrl = profile.UploadedImageUrl;
            return null;
        }

        request.ProfilePictureB64 = b64;
        return hash;
    }

    private void PersistUploadedImage(GambaProfile? profile, string? sentHash, string? issuedUrl)
    {
        if (profile == null || sentHash == null || string.IsNullOrEmpty(issuedUrl))
            return;

        profile.UploadedImageUrl = issuedUrl;
        profile.UploadedImageHash = sentHash;
        _config.Save();
    }

    private Dictionary<string, string> MyPostTokens() =>
        _config.RecruitmentPosts
            .Where(p => p.PostType == _postType)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Last().SessionToken);

    private string? BoosterKeyForRequest() =>
        string.IsNullOrWhiteSpace(_config.BoosterKey) ? null : _config.BoosterKey.Trim();

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private string ResolveDataCentre() =>
        _isVenue ? _formDataCentre : (_playerInfo.GetHomeDataCentre() ?? _formDataCentre);

    private List<RecruitmentScheduleEntry> BuildSchedule() =>
        DaysOfWeek
            .Where(d => _formSchedule[d].On)
            .Select(d =>
            {
                var ds = _formSchedule[d];
                return new RecruitmentScheduleEntry
                {
                    Day = d,
                    StartHour = ds.StartHour,
                    StartMinute = ds.StartMinute,
                    EndHour = ds.EndHour,
                    EndMinute = ds.EndMinute,
                };
            })
            .ToList();

    private static string Abbrev(string day) => day.Length >= 3 ? day[..3] : day;

    private static string FormatEntry(RecruitmentScheduleEntry e, bool full) =>
        $"{(full ? e.Day : Abbrev(e.Day))} {e.StartHour:00}:{e.StartMinute:00}-{e.EndHour:00}:{e.EndMinute:00}";

    private sealed class DaySchedule
    {
        public bool On;
        public int StartHour = 20;
        public int StartMinute;
        public int EndHour = 23;
        public int EndMinute;

        public void Reset()
        {
            On = false;
            StartHour = 20;
            StartMinute = 0;
            EndHour = 23;
            EndMinute = 0;
        }
    }

    private static string FormatNameWorld(string posterCharacter)
    {
        var parts = posterCharacter.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{string.Join(' ', parts[..^1])}@{parts[^1]}" : posterCharacter;
    }

}
