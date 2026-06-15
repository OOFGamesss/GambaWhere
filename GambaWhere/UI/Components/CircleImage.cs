using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace GambaWhere.UI.Components;

/// <summary>
/// Draws a texture centre-cropped into a circle, matching the server's centre-crop
/// so the local preview shows exactly what other players will see.
/// </summary>
public static class CircleImage
{
    public static (Vector2 Uv0, Vector2 Uv1) CoverSquareUv(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return (Vector2.Zero, Vector2.One);

        if (width > height)
        {
            var visible = (float)height / width;
            var off = (1f - visible) * 0.5f;
            return (new Vector2(off, 0f), new Vector2(1f - off, 1f));
        }

        if (height > width)
        {
            var visible = (float)width / height;
            var off = (1f - visible) * 0.5f;
            return (new Vector2(0f, off), new Vector2(1f, 1f - off));
        }

        return (Vector2.Zero, Vector2.One);
    }

    public static void DrawAt(ImDrawListPtr dl, Vector2 topLeft, float diameter, IDalamudTextureWrap tex, float alpha = 1f)
    {
        var (uv0, uv1) = CoverSquareUv(tex.Width, tex.Height);
        dl.AddImageRounded(
            tex.Handle,
            topLeft,
            topLeft + new Vector2(diameter, diameter),
            uv0,
            uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)),
            diameter * 0.5f);
    }

    public static void DrawPlaceholderAt(ImDrawListPtr dl, Vector2 topLeft, float diameter)
    {
        dl.AddRectFilled(
            topLeft,
            topLeft + new Vector2(diameter, diameter),
            ImGui.GetColorU32(new Vector4(0.22f, 0.22f, 0.26f, 1f)),
            diameter * 0.5f);
    }

    public static void DrawInline(float diameter, IDalamudTextureWrap? tex)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        if (tex != null)
            DrawAt(dl, pos, diameter, tex);
        else
            DrawPlaceholderAt(dl, pos, diameter);

        ImGui.Dummy(new Vector2(diameter, diameter));
    }
}
