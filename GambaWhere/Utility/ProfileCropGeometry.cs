using System;
using System.Numerics;

namespace GambaWhere.Utility;

/// <summary>Maps a pan and zoom selection onto a square sub-region of a source image.</summary>
public static class ProfileCropGeometry
{
    public const float MinZoom = 1f;
    public const float MaxZoom = 5f;

    public static (Vector2 Uv0, Vector2 Uv1) SquareUv(int width, int height, float zoom, float centerX, float centerY)
    {
        if (width <= 0 || height <= 0)
            return (Vector2.Zero, Vector2.One);

        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var cropSide = Math.Min(width, height) / zoom;
        var halfU = cropSide * 0.5f / width;
        var halfV = cropSide * 0.5f / height;
        centerX = Math.Clamp(centerX, halfU, 1f - halfU);
        centerY = Math.Clamp(centerY, halfV, 1f - halfV);
        return (new Vector2(centerX - halfU, centerY - halfV), new Vector2(centerX + halfU, centerY + halfV));
    }
}
