using System;
using Dalamud.Bindings.ImGui;

namespace GambaWhere.Utility;

/// <summary>Trims wrapped text to a maximum number of rendered lines, appending an ellipsis when cut.</summary>
public static class TextTruncate
{
    public static string ToLines(string text, float wrapWidth, int maxLines)
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
}
