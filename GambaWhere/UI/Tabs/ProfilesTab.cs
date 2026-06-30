using System;
using System.Collections.Generic;
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
using GambaWhere.Services;
using GambaWhere.UI.CardEffects;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

/// <summary>Create, edit, and delete local host profiles attached to hosted events.</summary>
public class ProfilesTab
{
    private readonly Configuration _config;
    private readonly ImageService _imageService;
    private readonly PlayerInfoService _playerInfo;
    private readonly FileDialogManager _fileDialog = new();
    private readonly ThemedCard _card = new();
    private readonly ProfileImageCropper _cropper = new();

    private static readonly string[] PreferredGameOptions = GambaEventsTab.KnownGameTypes;

    private static readonly string[] DecorationLabels = ["None", "Booster", "Gamba Where Beta"];
    private static readonly string[] DecorationValues = ["none", "booster", "gwbeta"];

    private const int MaxBioLines = 5;

    private GambaProfile? _draft;
    private bool _isNew;
    private readonly HashSet<string> _draftGames = new();
    private string? _pendingOriginalSource;
    private string? _pendingPreviewPath;
    private float _pendingZoom = 1f;
    private float _pendingCenterX = 0.5f;
    private float _pendingCenterY = 0.5f;
    private string? _pendingImageError;
    private string? _editError;
    private string? _pendingBrowsePath;

    private static readonly Vector4 SoftRed = new(1f, 0.4f, 0.4f, 1f);

    public ProfilesTab(Configuration config, ImageService imageService, PlayerInfoService playerInfo)
    {
        _config = config;
        _imageService = imageService;
        _playerInfo = playerInfo;
    }

    public void Draw()
    {
        _fileDialog.Draw();

        if (_pendingBrowsePath != null)
        {
            ApplyPickedImage(_pendingBrowsePath);
            _pendingBrowsePath = null;
        }

        if (_cropper.Draw(_imageService, _config, out var crop))
            ApplyCrop(crop);

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
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var minCardWidth = 300f * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var columns = avail >= minCardWidth * 2f + spacing ? 2 : 1;
        var cardW = columns == 2 ? (avail - spacing) * 0.5f : avail;
        var cardHeight = 315f * scale;

        var profiles = _config.Profiles.ToList();
        for (var i = 0; i < profiles.Count; i++)
        {
            if (columns == 2 && i % 2 == 1)
                ImGui.SameLine();
            DrawProfileCard(profiles[i], cardW, cardHeight);
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

    private void DrawProfileCard(GambaProfile profile, float cardW, float cardHeight)
    {
        using var id = ImRaii.PushId(profile.Id);

        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 6f * scale;
        var accent = _config.SecondaryColour;
        var cardEffect = CardEffectResolver.Resolve(profile.CardEffectStyle, profile.Booster);
        var cardBg = CardEffectResolver.BaseColour(cardEffect) ?? ThemeColours.CardBackground(_config.PrimaryColour);

        var clipMin = ImGui.GetWindowDrawList().GetClipRectMin();
        var clipMax = ImGui.GetWindowDrawList().GetClipRectMax();

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, rounding);
        using var svBorder = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
        using var svPad = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 12f * scale));
        using var colBg = ImRaii.PushColor(ImGuiCol.ChildBg, cardBg);

        using var child = ImRaii.Child("##pcard", new Vector2(cardW, cardHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding);
        if (!child.Success)
            return;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var title = string.IsNullOrWhiteSpace(profile.Name) ? "(unnamed)" : profile.Name;

        var rowStartX = ImGui.GetCursorPosX();
        var rowY = ImGui.GetCursorPosY();
        var availW = ImGui.GetContentRegionAvail().X;
        var actionsW = ProfileHeaderActionsWidth();
        ImGui.SetCursorPos(new Vector2(rowStartX + availW - actionsW, rowY));
        DrawProfileCardHeaderActions(profile);
        ImGui.SetCursorPos(new Vector2(rowStartX, rowY));

        ImGuiHelpers.ScaledDummy(6f);

        var width = ImGui.GetContentRegionAvail().X;
        var diameter = 80f * scale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - diameter) * 0.5f));
        var path = _imageService.GetProfileImagePath(profile.ImageFileName);
        var tex = path != null ? _imageService.GetFromPath(path) : null;
        ProfilePopup.DrawAvatarAt(dl, _imageService, ImGui.GetCursorScreenPos(), diameter, tex, profile.BorderStyle, profile.Booster);
        ImGui.Dummy(new Vector2(diameter, diameter));

        ImGuiHelpers.ScaledDummy(6f);
        var nameSize = ImGui.CalcTextSize(title);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - nameSize.X) * 0.5f));
        ImGui.TextColored(accent, title);

        if (profile.Booster)
        {
            ImGuiHelpers.ScaledDummy(2f);
            const string label = "Booster";
            var lblSize = ImGui.CalcTextSize(label);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - lblSize.X) * 0.5f));
            ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.78f, 1f), label);
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        var contentWidth = ImGui.GetContentRegionAvail().X;
        ImGui.TextDisabled("Bio");
        if (!string.IsNullOrWhiteSpace(profile.Bio))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
            ImGui.TextWrapped(TextTruncate.ToLines(profile.Bio, contentWidth, 2));
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.TextDisabled("No bio.");
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextDisabled("Preferred Games");
        if (profile.PreferredGames.Count > 0)
            GamePill.DrawList(profile.PreferredGames, contentWidth);
        else
            ImGui.TextDisabled("None listed.");

        var p0 = ImGui.GetWindowPos();
        ProfilePopup.DrawCardBackground(dl, p0, ImGui.GetWindowSize(), rounding, cardEffect, profile.Name, accent, (clipMin, clipMax));
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

    private static string ClampLines(string text, int maxLines)
    {
        var parts = text.Split('\n');
        return parts.Length <= maxLines ? text : string.Join("\n", parts.Take(maxLines));
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

        ImGuiHelpers.ScaledDummy(4f);

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

        ImGuiHelpers.ScaledDummy(8f);
        DrawDecorationPickers(draft);

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.TextColored(_config.SecondaryColour, "Preview");
        ImGuiHelpers.ScaledDummy(4f);
        var previewData = new ProfilePopup.Data
        {
            DisplayName = FormatCharacterName(_playerInfo.GetCharacterName()),
            Bio = draft.Bio,
            PreferredGames = PreferredGameOptions.Where(_draftGames.Contains).ToList(),
            Booster = draft.Booster,
            BorderStyle = draft.BorderStyle,
            CardEffectStyle = draft.CardEffectStyle,
        };
        ProfilePopup.DrawInlinePreview(_imageService, _config, previewData, ResolveDraftTexture(draft));

        if (!string.IsNullOrEmpty(_editError))
        {
            ImGuiHelpers.ScaledDummy(6f);
            ImGui.TextColored(SoftRed, _editError);
        }
    }

    private void DrawDecorationPickers(GambaProfile draft)
    {
        ImGui.TextColored(_config.SecondaryColour, "Decorations");
        ImGuiHelpers.ScaledDummy(2f);

        var hasKey = !string.IsNullOrWhiteSpace(_config.BoosterKey);

        using var table = ImRaii.Table("##deco_pickers", 2, ImGuiTableFlags.SizingStretchSame);
        if (!table)
            return;

        ImGui.TableNextColumn();
        ImGui.TextDisabled("Profile Border");
        ImGui.SetNextItemWidth(-1);
        var borderIdx = Array.IndexOf(DecorationValues, draft.BorderStyle);
        if (borderIdx < 0) borderIdx = 0;
        if (ImGui.BeginCombo("##BorderStyle", DecorationLabels[borderIdx]))
        {
            for (var i = 0; i < DecorationValues.Length; i++)
            {
                using var itemDisabled = ImRaii.Disabled(DecorationValues[i] == "booster" && !hasKey);
                if (ImGui.Selectable(DecorationLabels[i], i == borderIdx))
                    draft.BorderStyle = DecorationValues[i];
            }
            ImGui.EndCombo();
        }

        ImGui.TableNextColumn();
        ImGui.TextDisabled("Card Effect");
        ImGui.SetNextItemWidth(-1);
        var effectIdx = Array.IndexOf(DecorationValues, draft.CardEffectStyle);
        if (effectIdx < 0) effectIdx = 0;
        if (ImGui.BeginCombo("##CardEffectStyle", DecorationLabels[effectIdx]))
        {
            for (var i = 0; i < DecorationValues.Length; i++)
            {
                using var itemDisabled = ImRaii.Disabled(DecorationValues[i] == "booster" && !hasKey);
                if (ImGui.Selectable(DecorationLabels[i], i == effectIdx))
                    draft.CardEffectStyle = DecorationValues[i];
            }
            ImGui.EndCombo();
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

        if (UIHelper.IconTextButton(FontAwesomeIcon.FolderOpen, HasPicture(draft) ? "Change Picture" : "Choose Picture", "##ChoosePicture"))
            OpenPicturePicker();

        if (CanReCrop(draft))
        {
            ImGui.SameLine();
            if (UIHelper.IconTextButton(FontAwesomeIcon.Crop, "Adjust Crop", "##AdjustCrop"))
                OpenCropEditor(draft);
        }

        ImGui.TextDisabled("PNG or JPG, max 5 MB.");

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
        _pendingPreviewPath != null || !string.IsNullOrWhiteSpace(draft.ImageFileName);

    private bool CanSaveDraft() =>
        _draft != null
        && !string.IsNullOrWhiteSpace(_draft.Name)
        && HasPicture(_draft)
        && _pendingImageError == null;

    private bool CanReCrop(GambaProfile draft) => CropSourcePath(draft) != null;

    private string? CropSourcePath(GambaProfile draft)
    {
        if (_pendingOriginalSource != null)
            return _pendingOriginalSource;

        return _imageService.GetProfileImagePath(draft.OriginalImageFileName)
            ?? _imageService.GetProfileImagePath(draft.ImageFileName);
    }

    private IDalamudTextureWrap? ResolveDraftTexture(GambaProfile draft)
    {
        if (_pendingPreviewPath != null)
            return _imageService.GetFromPath(_pendingPreviewPath);

        var path = _imageService.GetProfileImagePath(draft.ImageFileName);
        return path != null ? _imageService.GetFromPath(path) : null;
    }

    private void BeginCreate()
    {
        _draft = new GambaProfile();
        _isNew = true;
        _draftGames.Clear();
        ResetPendingPicture();
        _editError = null;
    }

    private void BeginEdit(GambaProfile profile)
    {
        _draft = Clone(profile);
        _isNew = false;
        _draftGames.Clear();
        foreach (var g in profile.PreferredGames)
            _draftGames.Add(g);
        ResetPendingPicture();
        _editError = null;
    }

    private void CancelEdit()
    {
        _draft = null;
        ResetPendingPicture();
        _editError = null;
    }

    private void ResetPendingPicture()
    {
        _imageService.DeleteTemp(_pendingPreviewPath);
        _pendingPreviewPath = null;
        _pendingOriginalSource = null;
        _pendingZoom = 1f;
        _pendingCenterX = 0.5f;
        _pendingCenterY = 0.5f;
        _pendingImageError = null;
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

        if (!_imageService.ValidateProfileSource(sourcePath, out var error))
        {
            _pendingImageError = error;
            return;
        }

        _pendingImageError = null;
        _cropper.Open(sourcePath, 1f, 0.5f, 0.5f);
    }

    private void OpenCropEditor(GambaProfile draft)
    {
        var source = CropSourcePath(draft);
        if (source == null)
            return;

        var fromPending = _pendingOriginalSource != null;
        var zoom = fromPending ? _pendingZoom : draft.ImageCropZoom;
        var centerX = fromPending ? _pendingCenterX : draft.ImageCropCenterX;
        var centerY = fromPending ? _pendingCenterY : draft.ImageCropCenterY;
        _cropper.Open(source, zoom, centerX, centerY);
    }

    private void ApplyCrop(ProfileImageCropper.CropSelection crop)
    {
        var preview = _imageService.CreateCropPreview(crop.SourcePath, crop.Zoom, crop.CenterX, crop.CenterY, out var error);
        if (preview == null)
        {
            _pendingImageError = error ?? "Could not process that picture.";
            return;
        }

        _imageService.DeleteTemp(_pendingPreviewPath);
        _pendingPreviewPath = preview;
        _pendingOriginalSource = crop.SourcePath;
        _pendingZoom = crop.Zoom;
        _pendingCenterX = crop.CenterX;
        _pendingCenterY = crop.CenterY;
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

        if (_pendingPreviewPath == null && string.IsNullOrWhiteSpace(draft.ImageFileName))
        {
            _editError = "A picture is required.";
            return;
        }

        if (_pendingPreviewPath != null && !CommitPicture(draft))
            return;

        draft.PreferredGames = PreferredGameOptions.Where(_draftGames.Contains).ToList();
        draft.Bio = draft.Bio.Trim();

        CommitDraft(draft);
        CancelEdit();
    }

    private bool CommitPicture(GambaProfile draft)
    {
        var ok = _imageService.SaveProfileImageSet(
            _pendingOriginalSource!, draft.Id, _pendingZoom, _pendingCenterX, _pendingCenterY,
            out var originalName, out var squareName, out var error);
        if (!ok)
        {
            _editError = error ?? "Could not save that picture.";
            return false;
        }

        draft.ImageFileName = squareName;
        draft.OriginalImageFileName = originalName;
        draft.ImageCropZoom = _pendingZoom;
        draft.ImageCropCenterX = _pendingCenterX;
        draft.ImageCropCenterY = _pendingCenterY;
        _imageService.ReloadImages();

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
        _imageService.DeleteProfileImage(profile.ImageFileName);
        _imageService.DeleteProfileImage(profile.OriginalImageFileName);
        _imageService.ReloadImages();

        _config.Profiles.RemoveAll(p => p.Id == profile.Id);
        if (_config.SelectedProfileId == profile.Id)
            _config.SelectedProfileId = null;

        _config.Save();
    }

    private static string FormatCharacterName(string? raw)
    {
        if (raw == null) return "(not logged in)";
        var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{string.Join(' ', parts[..^1])}@{parts[^1]}" : raw;
    }

    private static GambaProfile Clone(GambaProfile source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        ImageFileName = source.ImageFileName,
        OriginalImageFileName = source.OriginalImageFileName,
        ImageCropZoom = source.ImageCropZoom,
        ImageCropCenterX = source.ImageCropCenterX,
        ImageCropCenterY = source.ImageCropCenterY,
        Bio = source.Bio,
        PreferredGames = new List<string>(source.PreferredGames),
        Booster = source.Booster,
        BorderStyle = source.BorderStyle,
        CardEffectStyle = source.CardEffectStyle,
        UploadedImageUrl = source.UploadedImageUrl,
        UploadedImageHash = source.UploadedImageHash
    };
}
