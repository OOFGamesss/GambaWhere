using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Config;
using GambaWhere.Services;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>Modal that lets the host drag and zoom a picked picture to choose where the circular avatar crops it.</summary>
public sealed class ProfileImageCropper
{
    public readonly record struct CropSelection(string SourcePath, float Zoom, float CenterX, float CenterY);

    private const string PopupId = "Adjust Picture##profile_cropper";

    private string? _sourcePath;
    private float _zoom = ProfileCropGeometry.MinZoom;
    private float _centerX = 0.5f;
    private float _centerY = 0.5f;
    private bool _openRequested;
    private bool _visible;

    public void Open(string sourcePath, float zoom, float centerX, float centerY)
    {
        _sourcePath = sourcePath;
        _zoom = Math.Clamp(zoom, ProfileCropGeometry.MinZoom, ProfileCropGeometry.MaxZoom);
        _centerX = centerX;
        _centerY = centerY;
        _openRequested = true;
    }

    public bool Draw(ImageService imageService, Configuration config, out CropSelection selection)
    {
        selection = default;

        if (_openRequested)
        {
            ImGui.OpenPopup(PopupId);
            _openRequested = false;
            _visible = true;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(360f * scale, 0f), ImGuiCond.Appearing);

        using var svRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 6f * scale);
        using var modal = ImRaii.PopupModal(PopupId, ref _visible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal.Success)
            return false;

        if (_sourcePath == null)
        {
            ImGui.CloseCurrentPopup();
            return false;
        }

        ImGui.TextDisabled("Drag to move, scroll to zoom.");
        ImGuiHelpers.ScaledDummy(6f);

        var viewport = 300f * scale;
        var tex = imageService.GetFromPath(_sourcePath);
        DrawViewport(config, tex, viewport);

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.TextDisabled("Zoom");
        ImGui.SetNextItemWidth(viewport);
        ImGui.SliderFloat("##cropzoom", ref _zoom, ProfileCropGeometry.MinZoom, ProfileCropGeometry.MaxZoom, "%.2fx");

        ImGuiHelpers.ScaledDummy(10f);
        return DrawButtons(out selection);
    }

    private void DrawViewport(Configuration config, IDalamudTextureWrap? tex, float side)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > side)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - side) * 0.5f);

        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(side, side);
        ImGui.InvisibleButton("##cropview", size);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();

        var dl = ImGui.GetWindowDrawList();
        var centre = origin + size * 0.5f;
        var radius = side * 0.5f;

        dl.AddRectFilled(origin, origin + size, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.10f, 1f)), 4f * scale);

        if (tex == null)
        {
            dl.AddCircle(centre, radius, ImGui.GetColorU32(config.SecondaryColour), 0, 2f * scale);
            return;
        }

        if (hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
                _zoom = Math.Clamp(_zoom * (1f + wheel * 0.1f), ProfileCropGeometry.MinZoom, ProfileCropGeometry.MaxZoom);
        }

        var cropSidePx = Math.Min(tex.Width, tex.Height) / _zoom;
        var pxPerSource = side / cropSidePx;
        var imgSize = new Vector2(tex.Width * pxPerSource, tex.Height * pxPerSource);

        if (active)
        {
            var delta = ImGui.GetIO().MouseDelta;
            if (imgSize.X > 0f && imgSize.Y > 0f)
            {
                _centerX -= delta.X / imgSize.X;
                _centerY -= delta.Y / imgSize.Y;
            }
        }

        var (uv0, uv1) = ProfileCropGeometry.SquareUv(tex.Width, tex.Height, _zoom, _centerX, _centerY);
        _centerX = (uv0.X + uv1.X) * 0.5f;
        _centerY = (uv0.Y + uv1.Y) * 0.5f;

        var imgTopLeft = centre - new Vector2(_centerX * imgSize.X, _centerY * imgSize.Y);

        dl.PushClipRect(origin, origin + size, true);
        dl.AddImage(tex.Handle, imgTopLeft, imgTopLeft + imgSize, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)));
        dl.AddImageRounded(tex.Handle, centre - new Vector2(radius, radius), centre + new Vector2(radius, radius),
            uv0, uv1, ImGui.GetColorU32(Vector4.One), radius);
        dl.PopClipRect();

        dl.AddCircle(centre, radius, ImGui.GetColorU32(config.SecondaryColour), 0, 2f * scale);
    }

    private bool DrawButtons(out CropSelection selection)
    {
        selection = default;

        using (UIHelper.PushGreenButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Check, "Apply", "##cropApply"))
            {
                selection = new CropSelection(_sourcePath!, _zoom, _centerX, _centerY);
                ImGui.CloseCurrentPopup();
                _visible = false;
                return true;
            }
        }

        ImGui.SameLine();
        using (UIHelper.PushRedButtonColours())
        {
            if (UIHelper.IconTextButton(FontAwesomeIcon.Times, "Cancel", "##cropCancel"))
            {
                ImGui.CloseCurrentPopup();
                _visible = false;
            }
        }

        return false;
    }
}
