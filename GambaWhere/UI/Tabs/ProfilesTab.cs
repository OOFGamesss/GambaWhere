using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Images;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Create, edit, and delete local host profiles attached to hosted events.</summary>
public class ProfilesTab
{
    private readonly Configuration _config;
    private readonly ImageCache _imageCache;
    private readonly ProfileImageStore _profileImages;
    private readonly FileDialogManager _fileDialog = new();
    private readonly ThemedCard _card = new();

    private static readonly string[] PreferredGameOptions = GambaEventsTab.KnownGameTypes;

    private const int MaxBioLines = 5;

    private GambaProfile? _draft;
    private bool _isNew;
    private readonly HashSet<string> _draftGames = new();
    private string? _pendingImageSource;
    private string? _pendingImageError;
    private string? _editError;
    private string? _pendingBrowsePath;

    private static readonly Vector4 SoftRed = new(1f, 0.4f, 0.4f, 1f);

    public ProfilesTab(Configuration config, ImageCache imageCache, ProfileImageStore profileImages)
    {
        _config = config;
        _imageCache = imageCache;
        _profileImages = profileImages;
    }

    public void Draw()
    {
        _fileDialog.Draw();

        if (_pendingBrowsePath != null)
        {
            ApplyPickedImage(_pendingBrowsePath);
            _pendingBrowsePath = null;
        }

        ImGuiHelpers.ScaledDummy(8f);

        if (_draft != null)
            DrawEditorCard();
        else
            DrawList();
    }

    private void DrawList()
    {
        DrawNewProfileButton();
        ImGuiHelpers.ScaledDummy(6f);

        if (_config.Profiles.Count == 0)
        {
            ImGui.TextDisabled("No profiles yet. Create one to attach your picture, bio, and preferred games to your events.");
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var minCardWidth = 300f * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var columns = avail >= minCardWidth * 2f + ImGui.GetStyle().ItemSpacing.X ? 2 : 1;

        using var grid = ImRaii.Table("##profiles_grid", columns, ImGuiTableFlags.SizingStretchSame);
        if (!grid)
            return;

        var cardHeight = 220f * scale;
        foreach (var profile in _config.Profiles.ToList())
        {
            ImGui.TableNextColumn();
            var title = string.IsNullOrWhiteSpace(profile.Name) ? "(unnamed)" : profile.Name;
            _card.Draw($"##profile_{profile.Id}", title, _config.PrimaryColour, _config.SecondaryColour,
                cardHeight, () => DrawProfileCardBody(profile), () => DrawProfileCardHeaderActions(profile),
                ProfileHeaderActionsWidth());
            ImGuiHelpers.ScaledDummy(8f);
        }
    }

    private void DrawNewProfileButton()
    {
        var margin = 8f * ImGuiHelpers.GlobalScale;
        var size = UIHelper.CalcButtonSize(FontAwesomeIcon.Plus, "New Profile");
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, avail - margin - size.X));

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Plus, "New Profile", "##NewProfile"))
                BeginCreate();
        }
    }

    private void DrawProfileCardBody(GambaProfile profile)
    {
        using var id = ImRaii.PushId(profile.Id);

        var scale = ImGuiHelpers.GlobalScale;
        var diameter = 90f * scale;

        using (var table = ImRaii.Table("##pcard", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGui.TableSetupColumn("##pic", ImGuiTableColumnFlags.WidthFixed, diameter);
                ImGui.TableSetupColumn("##info", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var path = _profileImages.GetPath(profile.ImageFileName);
                var tex = path != null ? _imageCache.GetFromPath(path) : null;
                CircleImage.DrawInline(diameter, tex);

                ImGui.TableNextColumn();
                DrawBio(profile.Bio, ImGui.GetContentRegionAvail().X, diameter);
            }
        }

        ImGuiHelpers.ScaledDummy(2f);
        DrawPreferredGamesAboveButtons(profile);
    }

    private static float ProfileHeaderActionsWidth()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var btn = ImGui.GetFrameHeight();
        return btn * 2f + spacing;
    }

    private void DrawProfileCardHeaderActions(GambaProfile profile)
    {
        using var id = ImRaii.PushId(profile.Id);

        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
                   .Push(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.12f))
                   .Push(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.20f)))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.96f, 0.66f, 0.13f, 1f)))
            {
                if (ImGuiComponents.IconButton("##EditProfile", FontAwesomeIcon.Pen))
                    BeginEdit(profile);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Edit");

            ImGui.SameLine();

            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.92f, 0.34f, 0.34f, 1f)))
            {
                if (ImGuiComponents.IconButton("##DeleteProfile", FontAwesomeIcon.Trash))
                    DeleteProfile(profile);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Delete");
        }
    }

    private void DrawPreferredGamesAboveButtons(GambaProfile profile)
    {
        if (profile.PreferredGames.Count == 0)
            return;

        var width = ImGui.GetContentRegionAvail().X;
        var scale = ImGuiHelpers.GlobalScale;
        var bottomGap = 14f * scale;
        var bioGap = 10f * scale;
        var pillsHeight = GamePill.CalcWrappedHeight(profile.PreferredGames, width);

        var bottom = ImGui.GetWindowContentRegionMax().Y;
        var bottomPinned = bottom - bottomGap - pillsHeight;
        var minPillsY = ImGui.GetCursorPosY() + bioGap;
        var pillsY = Math.Min(Math.Max(minPillsY, ImGui.GetCursorPosY()), bottomPinned);
        if (pillsY > ImGui.GetCursorPosY())
            ImGui.SetCursorPosY(pillsY);

        GamePill.DrawList(profile.PreferredGames, width);
    }

    private void DrawBio(string bio, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 6f * scale;

        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.28f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f * scale))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        {
            using var child = ImRaii.Child("##bio_panel", new Vector2(width, height), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (!child.Success)
                return;

            const string label = "Bio";
            var labelSize = ImGui.CalcTextSize(label);
            ImGui.SetCursorPos(new Vector2((width - labelSize.X) * 0.5f, pad));
            ImGui.TextColored(_config.SecondaryColour, label);

            var lineHeight = ImGui.GetTextLineHeight();
            var regionTop = pad + labelSize.Y + 2f * scale;
            var regionH = Math.Max(lineHeight, height - regionTop - pad);
            var avail = Math.Max(10f, width - pad * 2f);

            var hasBio = !string.IsNullOrWhiteSpace(bio);
            var maxLines = Math.Max(1, (int)(regionH / lineHeight));
            var text = hasBio ? TruncateToLines(bio, avail, maxLines) : "No bio.";

            var textSize = ImGui.CalcTextSize(text, false, avail);
            var textX = pad;
            var textY = regionTop + Math.Max(0f, (regionH - textSize.Y) * 0.5f);

            ImGui.SetCursorPos(new Vector2(textX, textY));
            ImGui.PushTextWrapPos(textX + avail);
            if (hasBio)
                ImGui.TextWrapped(text);
            else
                ImGui.TextDisabled(text);
            ImGui.PopTextWrapPos();
        }
    }

    private static string ClampLines(string text, int maxLines)
    {
        var parts = text.Split('\n');
        return parts.Length <= maxLines ? text : string.Join("\n", parts.Take(maxLines));
    }

    private static string TruncateToLines(string text, float wrapWidth, int maxLines)
    {
        var maxHeight = maxLines * ImGui.GetTextLineHeight() + 1f;
        if (ImGui.CalcTextSize(text, false, wrapWidth).Y <= maxHeight)
            return text;

        const string ellipsis = "...";
        var low = 0;
        var high = text.Length;
        var best = 0;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = text.Substring(0, mid).TrimEnd() + ellipsis;
            if (ImGui.CalcTextSize(candidate, false, wrapWidth).Y <= maxHeight)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return text.Substring(0, best).TrimEnd() + ellipsis;
    }

    private void DrawEditorCard()
    {
        var title = _isNew ? "New Profile" : "Edit Profile";
        _card.Draw("##profile_editor", title, _config.PrimaryColour, _config.SecondaryColour, DrawEditorBody);

        ImGuiHelpers.ScaledDummy(10f);
        DrawEditorButtons();
    }

    private void DrawEditorBody()
    {
        var draft = _draft!;

        DrawEditorTopRow(draft);

        ImGuiHelpers.ScaledDummy(8f);

        ImGui.TextColored(_config.SecondaryColour, "Bio");
        ImGui.SetNextItemWidth(-1);
        var bio = draft.Bio;
        if (ImGui.InputTextMultiline("##ProfileBio", ref bio, GambaProfile.BioMaxLength, new Vector2(0, 90 * ImGuiHelpers.GlobalScale)))
            draft.Bio = ClampLines(bio, MaxBioLines);

        var count = draft.Bio.Length;
        var overLimit = count >= GambaProfile.BioMaxLength;
        ImGui.TextColored(overLimit ? SoftRed : new Vector4(0.5f, 0.5f, 0.5f, 1f), $"{count} / {GambaProfile.BioMaxLength}");

        ImGuiHelpers.ScaledDummy(8f);

        ImGui.TextColored(_config.SecondaryColour, "Preferred Games");
        ImGuiHelpers.ScaledDummy(2f);
        GamePill.DrawSelector(PreferredGameOptions, _draftGames, ImGui.GetContentRegionAvail().X, "editor");

        if (!string.IsNullOrEmpty(_editError))
        {
            ImGuiHelpers.ScaledDummy(6f);
            ImGui.TextColored(SoftRed, _editError);
        }
    }

    private void DrawEditorTopRow(GambaProfile draft)
    {
        using var table = ImRaii.Table("##editor_top", 2, ImGuiTableFlags.SizingStretchSame);
        if (!table)
            return;

        ImGui.TableNextColumn();
        ImGui.TextColored(_config.SecondaryColour, "Profile Name");
        ImGui.SetNextItemWidth(-1);
        var name = draft.Name;
        if (ImGui.InputText("##ProfileName", ref name, 64))
            draft.Name = name;

        ImGui.TableNextColumn();
        ImGui.TextColored(_config.SecondaryColour, "Picture");

        using (ImRaii.Group())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.FolderOpen, HasPicture(draft) ? "Change Picture" : "Choose Picture", "##ChoosePicture"))
                OpenPicturePicker();

            ImGui.TextDisabled("PNG or JPG,");
            ImGui.TextDisabled("max 5 MB.");
        }

        ImGui.SameLine();
        var diameter = 130f * ImGuiHelpers.GlobalScale;
        var remaining = ImGui.GetContentRegionAvail().X;
        if (remaining > diameter)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (remaining - diameter) / 2f);
        CircleImage.DrawInline(diameter, ResolveDraftTexture(draft));

        if (_pendingImageError != null)
            ImGui.TextColored(SoftRed, _pendingImageError);
    }

    private void DrawEditorButtons()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var saveSize = new Vector2(160f * scale, 44f * scale);
        var cancelSize = new Vector2(140f * scale, 44f * scale);
        var total = saveSize.X + spacing + cancelSize.X;

        var baseX = ImGui.GetCursorPosX();
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (avail - total) * 0.5f));

        using (ImRaii.Disabled(!CanSaveDraft()))
        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Save, "Save", saveSize, "##SaveProfile"))
                SaveDraft();
        }

        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", cancelSize, "##CancelProfile"))
                CancelEdit();
        }
    }

    private bool HasPicture(GambaProfile draft) =>
        _pendingImageSource != null || !string.IsNullOrWhiteSpace(draft.ImageFileName);

    private bool CanSaveDraft() =>
        _draft != null
        && !string.IsNullOrWhiteSpace(_draft.Name)
        && HasPicture(_draft)
        && _pendingImageError == null;

    private IDalamudTextureWrap? ResolveDraftTexture(GambaProfile draft)
    {
        if (_pendingImageSource != null)
            return _imageCache.GetFromPath(_pendingImageSource);

        var path = _profileImages.GetPath(draft.ImageFileName);
        return path != null ? _imageCache.GetFromPath(path) : null;
    }

    private void BeginCreate()
    {
        _draft = new GambaProfile();
        _isNew = true;
        _draftGames.Clear();
        _pendingImageSource = null;
        _pendingImageError = null;
        _editError = null;
    }

    private void BeginEdit(GambaProfile profile)
    {
        _draft = Clone(profile);
        _isNew = false;
        _draftGames.Clear();
        foreach (var g in profile.PreferredGames)
            _draftGames.Add(g);
        _pendingImageSource = null;
        _pendingImageError = null;
        _editError = null;
    }

    private void CancelEdit()
    {
        _draft = null;
        _pendingImageSource = null;
        _pendingImageError = null;
        _editError = null;
    }

    private void OpenPicturePicker()
    {
        _fileDialog.OpenFileDialog(
            "Select Profile Picture",
            "Image Files{.png,.jpg,.jpeg}",
            (success, paths) =>
            {
                if (success && paths.Count > 0)
                    _pendingBrowsePath = paths[0];
            },
            1);
    }

    private void ApplyPickedImage(string sourcePath)
    {
        _editError = null;

        try
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                _pendingImageError = "Picture must be a PNG or JPG.";
                return;
            }

            if (new FileInfo(sourcePath).Length > ProfileImageStore.MaxBytes)
            {
                _pendingImageError = "Image is too large (max 5 MB).";
                return;
            }
        }
        catch
        {
            _pendingImageError = "That file could not be read.";
            return;
        }

        _pendingImageSource = sourcePath;
        _pendingImageError = null;
    }

    private void SaveDraft()
    {
        var draft = _draft!;
        draft.Name = draft.Name.Trim();

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            _editError = "Profile name is required.";
            return;
        }

        if (draft.Bio.Length > GambaProfile.BioMaxLength)
        {
            _editError = $"Bio must be {GambaProfile.BioMaxLength} characters or fewer.";
            return;
        }

        if (UserTextGuard.ContainsDisallowedContent(draft.Bio))
        {
            _editError = "Bio must not contain URLs or HTML.";
            return;
        }

        if (_pendingImageSource == null && string.IsNullOrWhiteSpace(draft.ImageFileName))
        {
            _editError = "A picture is required.";
            return;
        }

        if (_pendingImageSource != null && !CommitPicture(draft))
            return;

        draft.PreferredGames = PreferredGameOptions.Where(_draftGames.Contains).ToList();
        draft.Bio = draft.Bio.Trim();

        CommitDraft(draft);
        CancelEdit();
    }

    private bool CommitPicture(GambaProfile draft)
    {
        var oldPath = _profileImages.GetPath(draft.ImageFileName);

        var saved = _profileImages.TrySave(_pendingImageSource!, draft.Id, out var error);
        if (saved == null)
        {
            _editError = error ?? "Could not save that picture.";
            return false;
        }

        if (oldPath != null)
            _imageCache.EvictFromPath(oldPath);

        draft.ImageFileName = saved;

        var newPath = _profileImages.GetPath(saved);
        if (newPath != null)
            _imageCache.EvictFromPath(newPath);

        draft.UploadedImageUrl = null;
        draft.UploadedImageHash = null;
        return true;
    }

    private void CommitDraft(GambaProfile draft)
    {
        if (_isNew)
        {
            _config.Profiles.Add(draft);
        }
        else
        {
            var index = _config.Profiles.FindIndex(p => p.Id == draft.Id);
            if (index >= 0)
                _config.Profiles[index] = draft;
            else
                _config.Profiles.Add(draft);
        }

        _config.Save();
    }

    private void DeleteProfile(GambaProfile profile)
    {
        var path = _profileImages.GetPath(profile.ImageFileName);
        if (path != null)
            _imageCache.EvictFromPath(path);
        _profileImages.Delete(profile.ImageFileName);

        _config.Profiles.RemoveAll(p => p.Id == profile.Id);
        if (_config.SelectedProfileId == profile.Id)
            _config.SelectedProfileId = null;

        _config.Save();
    }

    private static GambaProfile Clone(GambaProfile source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        ImageFileName = source.ImageFileName,
        Bio = source.Bio,
        PreferredGames = new List<string>(source.PreferredGames),
        Booster = source.Booster,
        UploadedImageUrl = source.UploadedImageUrl,
        UploadedImageHash = source.UploadedImageHash
    };
}
