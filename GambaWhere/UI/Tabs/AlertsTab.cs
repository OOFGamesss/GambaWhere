using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Alerting;
using GambaWhere.API;
using GambaWhere.Config;
using GambaWhere.UI.Components;
using GambaWhere.Utility;

namespace GambaWhere.UI.Tabs;

public class AlertsTab
{
    private readonly Configuration _config;
    private readonly GambaWhereClient _client;

    private volatile string[] _venueOptions = Array.Empty<string>();
    private volatile bool _isFetchingVenues;
    private bool _venuesFetched;

    private int? _pendingRowRemovalIndex;

    public AlertsTab(Configuration config, GambaWhereClient client)
    {
        _config = config;
        _client = client;
    }

    public void Draw()
    {
        if (!_venuesFetched)
            FetchVenues();

        FlushPendingRowRemoval();

        ImGui.TextWrapped("Get notified when an event matches your criteria.");
        ImGui.TextWrapped("Multiple selections in a field act as OR; fields combine as AND.");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (ImGuiComponents.IconButton("##AddAlert", FontAwesomeIcon.Plus))
        {
            _config.Alerts.Add(new AlertRule());
            _config.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Add alert");

        ImGuiHelpers.ScaledDummy(6f);

        if (_config.Alerts.Count == 0)
        {
            ImGui.TextDisabled("No alerts configured. Click + Add alert above to create one.");
            return;
        }

        for (var i = 0; i < _config.Alerts.Count; i++)
            DrawRule(_config.Alerts[i], i);
    }

    private void DrawRule(AlertRule rule, int index)
    {
        using var id = ImRaii.PushId(index);

        var cardTopScreen = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        try
        {
            ImGuiHelpers.ScaledDummy(6f);

            var enabled = rule.Enabled;
            if (ImGui.Checkbox("##Enabled", ref enabled))
            {
                rule.Enabled = enabled;
                _config.Save();
            }
            ImGui.SameLine();

            var btn = 26f * ImGuiHelpers.GlobalScale;
            var nameWidth = Math.Max(ImGui.GetContentRegionAvail().X - btn - ImGui.GetStyle().ItemSpacing.X, 120f);
            ImGui.SetNextItemWidth(nameWidth);
            var name = rule.Name;
            if (ImGui.InputTextWithHint("##Name", "Alert name", ref name, 128))
                rule.Name = name;
            if (ImGui.IsItemDeactivatedAfterEdit())
                _config.Save();

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##Delete", FontAwesomeIcon.TrashAlt))
                _pendingRowRemovalIndex = index;

            if (rule.IsInert)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.4f, 1f)))
                    ImGui.TextWrapped("(no criteria, will not fire)");
            }

            ImGuiHelpers.ScaledDummy(4f);

            var labelOffset = CalcLabelOffset();
            DrawFilterRow("Games", labelOffset, "##Games", GambaEventsTab.KnownGameTypes, rule.GameTypes);
            DrawFilterRow("Data Centres", labelOffset, "##DCs", GambaEventsTab.KnownDataCentres, rule.DataCentres);
            DrawFilterRow("Venues", labelOffset, "##Venues", _venueOptions, rule.VenueNames);

            ImGuiHelpers.ScaledDummy(6f);
        }
        finally
        {
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(
                cardTopScreen,
                new Vector2(cardTopScreen.X + availWidth, ImGui.GetCursorScreenPos().Y),
                ImGui.GetColorU32(ThemeColours.CardBackground(_config.PrimaryColour)),
                4f * ImGuiHelpers.GlobalScale);
            drawList.ChannelsMerge();
        }

        ImGuiHelpers.ScaledDummy(6f);
    }

    private void DrawFilterRow(string label, float labelOffset, string id, IReadOnlyList<string> options, HashSet<string> selected)
    {
        ImGui.Text(label);
        ImGui.SameLine(labelOffset);
        var prevCount = selected.Count;
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        MultiSelectCombo.Draw(id, "Any", options, selected);
        if (selected.Count != prevCount)
            _config.Save();
    }

    private static float CalcLabelOffset()
    {
        var labels = new[] { "Games", "Data Centres", "Venues" };
        var max = 0f;
        foreach (var label in labels)
        {
            var w = ImGui.CalcTextSize(label).X;
            if (w > max) max = w;
        }
        return max + 16f * ImGuiHelpers.GlobalScale;
    }

    private void FlushPendingRowRemoval()
    {
        if (_pendingRowRemovalIndex is not { } index)
            return;
        _pendingRowRemovalIndex = null;
        if (index >= 0 && index < _config.Alerts.Count)
        {
            _config.Alerts.RemoveAt(index);
            _config.Save();
        }
    }

    private void FetchVenues()
    {
        if (_isFetchingVenues)
            return;
        _isFetchingVenues = true;
        _venuesFetched = true;
        _ = Task.Run(async () =>
        {
            var venues = await _client.GetVenuesAsync();
            _venueOptions = venues;
            _isFetchingVenues = false;
        });
    }
}
