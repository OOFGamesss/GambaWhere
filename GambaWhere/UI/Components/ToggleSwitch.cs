using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GambaWhere.UI.Components;

/// <summary>
/// Sliding toggle switch for boolean values.
/// </summary>
internal static class ToggleSwitch
{
    private static readonly Dictionary<string, float> Animations = new();

    private static readonly Vector4 OffTrack = new(0.28f, 0.28f, 0.32f, 1f);
    private static readonly Vector4 Knob = new(0.97f, 0.97f, 0.98f, 1f);

    internal static bool Draw(string id, ref bool value, Vector4 onColour)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = ImGui.GetFrameHeight();
        var width = height * 1.85f;
        var radius = height * 0.5f;

        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        if (clicked)
            value = !value;

        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var centreY = (min.Y + max.Y) * 0.5f;

        var target = value ? 1f : 0f;
        if (!Animations.TryGetValue(id, out var t))
            t = target;
        t += (target - t) * Math.Min(1f, ImGui.GetIO().DeltaTime * 14f);
        if (Math.Abs(target - t) < 0.001f)
            t = target;
        Animations[id] = t;

        var track = Lerp(OffTrack, onColour, t);
        if (hovered)
            track = new Vector4(track.X, track.Y, track.Z, Math.Min(1f, track.W + 0.12f));

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, ImGui.GetColorU32(track), radius);

        var knobX = min.X + radius + (width - radius * 2f) * t;
        dl.AddCircleFilled(new Vector2(knobX, centreY), radius - 2.5f * scale, ImGui.GetColorU32(Knob));

        return clicked;
    }

    private static Vector4 Lerp(Vector4 a, Vector4 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t,
        a.W + (b.W - a.W) * t);
}
