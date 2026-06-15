using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace GambaWhere.UI.Components;

/// <summary>Combo box for searching and selecting a venue.</summary>
public static class VenueSearchCombo
{
    private static volatile string[] _venues = [];
    private static List<string> _filtered = [];
    private static volatile bool _filterDirty;
    private static string _search = string.Empty;
    private static bool _wasOpen;
    private static float _popupContentWidth;
    private static float _heartButtonWidth;

    public static void SetVenues(string[] venues)
    {
        _venues = venues.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
        _filterDirty = true;
    }

    public static bool Draw(string id, ref string? selectedVenueName, List<string> favourites, Action onFavouriteChanged)
    {
        if (_filterDirty)
        {
            RefreshFilter();
            _filterDirty = false;
        }

        var preview = selectedVenueName ?? "No Venue";
        var isOpen = ImGui.BeginCombo(id, preview);

        if (!isOpen)
        {
            if (_wasOpen)
            {
                _search = string.Empty;
                _wasOpen = false;
            }
            return false;
        }

        if (!_wasOpen)
        {
            ImGui.SetKeyboardFocusHere();
            _wasOpen = true;
            _popupContentWidth = ImGui.GetContentRegionAvail().X;
        }

        var changed = false;

        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##VenueSearchInput", ref _search, 128))
            RefreshFilter();

        if (ImGui.Selectable("No Venue##NoVenueOpt", selectedVenueName == null))
        {
            selectedVenueName = null;
            changed = true;
        }

        ImGui.Separator();

        var favSet = new HashSet<string>(favourites, StringComparer.OrdinalIgnoreCase);
        var filteredFavs = _filtered.Where(v => favSet.Contains(v)).ToList();
        var filteredNonFavs = _filtered.Where(v => !favSet.Contains(v)).ToList();

        foreach (var venue in filteredFavs)
            DrawRow(venue, ref selectedVenueName, ref changed, favourites, favSet, onFavouriteChanged);

        if (filteredFavs.Count > 0 && filteredNonFavs.Count > 0)
            ImGui.Separator();

        foreach (var venue in filteredNonFavs)
            DrawRow(venue, ref selectedVenueName, ref changed, favourites, favSet, onFavouriteChanged);

        ImGui.EndCombo();
        return changed;
    }

    private static void DrawRow(string venue, ref string? selectedVenueName, ref bool changed, List<string> favourites, HashSet<string> favSet, Action onFavouriteChanged)
    {
        var isFav = favSet.Contains(venue);
        var isSelected = string.Equals(venue, selectedVenueName, StringComparison.OrdinalIgnoreCase);

        var heartW = _heartButtonWidth > 0f ? _heartButtonWidth : ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var selectableWidth = Math.Max(1f, _popupContentWidth - heartW - spacing);

        if (ImGui.Selectable($"{venue}##{venue}", isSelected, ImGuiSelectableFlags.None, new Vector2(selectableWidth, 0)))
        {
            selectedVenueName = venue;
            changed = true;
        }

        ImGui.SameLine();

        var heartColour = isFav
            ? new Vector4(1f, 0.35f, 0.4f, 1f)
            : new Vector4(0.4f, 0.4f, 0.4f, 1f);

        using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.1f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.2f)))
        using (ImRaii.PushColor(ImGuiCol.Text, heartColour))
        {
            if (ImGuiComponents.IconButton($"##heart{venue}", FontAwesomeIcon.Heart))
            {
                if (isFav)
                    favourites.RemoveAll(v => string.Equals(v, venue, StringComparison.OrdinalIgnoreCase));
                else
                    favourites.Add(venue);
                onFavouriteChanged();
            }
        }

        if (_heartButtonWidth <= 0f)
            _heartButtonWidth = ImGui.GetItemRectSize().X;
    }

    private static void RefreshFilter()
    {
        var venues = _venues;

        if (string.IsNullOrWhiteSpace(_search))
        {
            _filtered = venues.Take(200).ToList();
            return;
        }

        _filtered = venues
            .Where(v => v.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .Take(200)
            .ToList();
    }
}
