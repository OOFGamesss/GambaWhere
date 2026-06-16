using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GambaWhere.Utility;

/// <summary>
/// Reads the in-game "_NaviMap" minimap addon and projects world positions onto it.
/// Maths and node indices adapted from https://github.com/GemPlugins/MiniMappingway.
/// </summary>
public static class MinimapProjector
{
    private const float NaviMapBaseSize = 218f;
    private const float RadiusFactor = 0.315f;
    private const float CentrePivotYOffset = 5f;

    public readonly struct NaviMapReading
    {
        public NaviMapReading(float x, float y, float naviScale, float zoom, float rotation, bool isLocked)
        {
            X = x;
            Y = y;
            NaviScale = naviScale;
            Zoom = zoom;
            Rotation = rotation;
            IsLocked = isLocked;
        }

        public float X { get; }
        public float Y { get; }
        public float NaviScale { get; }
        public float Zoom { get; }
        public float Rotation { get; }
        public bool IsLocked { get; }
    }

    public static unsafe bool TryRead(nint addonPtr, out NaviMapReading reading)
    {
        reading = default;

        var unit = (AtkUnitBase*)addonPtr;
        if (unit == null || !unit->IsVisible)
            return false;

        try
        {
            var isLocked = false;
            var lockNode = unit->GetNodeById(4);
            if (lockNode != null)
            {
                var checkbox = (AtkComponentCheckBox*)lockNode->GetComponent();
                if (checkbox != null)
                    isLocked = checkbox->IsChecked;
            }

            var rotation = 0f;
            var rotationNode = unit->GetNodeById(8);
            if (rotationNode != null)
                rotation = rotationNode->Rotation;

            var zoom = 1f;
            var zoomNode = unit->GetNodeById(18);
            if (zoomNode != null)
            {
                var component = zoomNode->GetComponent();
                if (component != null)
                {
                    var imageNode = component->GetImageNodeById(6);
                    if (imageNode != null)
                        zoom = imageNode->ScaleX;
                }
            }

            reading = new NaviMapReading(unit->X, unit->Y, unit->Scale, zoom, rotation, isLocked);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static float MapSizeFor(in NaviMapReading reading) => NaviMapBaseSize * reading.NaviScale;

    public static float RadiusFor(in NaviMapReading reading) => MapSizeFor(reading) * RadiusFactor;

    public static Vector2 PlayerCentre(in NaviMapReading reading, Vector2 viewportPos)
    {
        var mapSize = MapSizeFor(reading);
        var centre = new Vector2(reading.X + (mapSize / 2f), reading.Y + (mapSize / 2f)) + viewportPos;
        centre.Y -= CentrePivotYOffset;
        return centre;
    }

    public static Vector2 Project(
        in NaviMapReading reading,
        Vector2 playerCentre,
        float radius,
        float combinedScale,
        Vector2 playerWorldXz,
        Vector2 targetWorldXz)
    {
        var relative = (playerWorldXz - targetWorldXz) * combinedScale;
        var pos = playerCentre - relative;

        if (!reading.IsLocked)
            pos = Rotate(playerCentre, pos, reading.Rotation);

        var distance = Vector2.Distance(playerCentre, pos);
        if (distance > radius && distance > 0f)
        {
            var originToObject = (pos - playerCentre) * (radius / distance);
            pos = playerCentre + originToObject;
        }

        return pos;
    }

    private static Vector2 Rotate(Vector2 center, Vector2 pos, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var dx = pos.X - center.X;
        var dy = pos.Y - center.Y;
        return new Vector2(
            (cos * dx) - (sin * dy) + center.X,
            (sin * dx) + (cos * dy) + center.Y);
    }
}
