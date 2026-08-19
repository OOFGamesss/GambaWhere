using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GambaWhere.Utility;

namespace GambaWhere.UI.Components;

/// <summary>Date and time picker working in Server Time, showing the equivalent in the viewer's own local time beneath it.</summary>
internal sealed class DateTimePicker
{
    private static readonly string[] MonthNames =
        CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames[..12];

    private int _year;
    private int _month;
    private int _day;
    private int _hour;
    private int _minute;

    public DateTimePicker() => Reset(DateTime.UtcNow.AddHours(1));

    public DateTime Value => ServerTime.ToUtc(_year, _month, _day, _hour, _minute);

    public void Reset(DateTime utc)
    {
        var start = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        _year = start.Year;
        _month = start.Month;
        _day = start.Day;
        _hour = start.Hour;
        _minute = start.Minute;
    }

    public void Draw(string id, Vector4 accent)
    {
        using var pushed = ImRaii.PushId(id);

        var scale = ImGuiHelpers.GlobalScale;
        var numberWidth = 64f * scale;
        var monthWidth = 84f * scale;
        var yearWidth = 80f * scale;

        HostField.Label("Date and Time (Server Time)");
        ImGuiHelpers.ScaledDummy(2f);

        DrawNumberField("Day", "##day", ref _day, 1, ServerTime.DaysInMonth(_year, _month), numberWidth);

        ImGui.SameLine();
        DrawMonthField(monthWidth);

        ImGui.SameLine();
        DrawNumberField("Year", "##year", ref _year, DateTime.UtcNow.Year, DateTime.UtcNow.Year + 1, yearWidth);

        _day = Math.Clamp(_day, 1, ServerTime.DaysInMonth(_year, _month));

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(10f, 0f);
        ImGui.SameLine();

        DrawNumberField("Hour", "##hour", ref _hour, 0, 23, numberWidth);

        ImGui.SameLine();
        DrawNumberField("Min", "##minute", ref _minute, 0, 59, numberWidth);

        ImGuiHelpers.ScaledDummy(6f);

        if (UIHelper.IconTextButton(FontAwesomeIcon.CalendarPlus, "+1 Day", "##gw_dtp_day"))
            Shift(TimeSpan.FromDays(1));

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.CalendarWeek, "+1 Week", "##gw_dtp_week"))
            Shift(TimeSpan.FromDays(7));

        ImGui.SameLine();

        if (UIHelper.IconTextButton(FontAwesomeIcon.Undo, "Next Hour", "##gw_dtp_reset"))
            Reset(DateTime.UtcNow.AddHours(1));

        ImGuiHelpers.ScaledDummy(6f);

        using (ImRaii.PushColor(ImGuiCol.Text, ThemeColours.AccentText(accent)))
            ImGui.TextUnformatted(ServerTime.FormatServerTimeWithLocal(Value));
    }

    private void Shift(TimeSpan delta) => Reset(Value + delta);

    private static void DrawNumberField(string label, string id, ref int value, int min, int max, float width)
    {
        using var group = ImRaii.Group();

        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(width);

        var working = value;
        if (ImGui.InputInt(id, ref working, 0))
            value = Math.Clamp(working, min, max);
    }

    private void DrawMonthField(float width)
    {
        using var group = ImRaii.Group();

        ImGui.TextDisabled("Month");
        ImGui.SetNextItemWidth(width);

        using var combo = ImRaii.Combo("##month", MonthNames[_month - 1]);
        if (!combo)
            return;

        for (var i = 0; i < MonthNames.Length; i++)
        {
            if (ImGui.Selectable(MonthNames[i], i == _month - 1))
                _month = i + 1;
        }
    }
}
