using System;
using System.Globalization;

namespace GambaWhere.Utility;

/// <summary>Server Time helpers.</summary>
public static class ServerTime
{
    public static DateTime ToUtc(int year, int month, int day, int hour, int minute)
    {
        var safeYear = Math.Clamp(year, 1, 9999);
        var safeMonth = Math.Clamp(month, 1, 12);
        var safeDay = Math.Clamp(day, 1, DateTime.DaysInMonth(safeYear, safeMonth));

        return new DateTime(
            safeYear, safeMonth, safeDay,
            Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0,
            DateTimeKind.Utc);
    }

    public static int DaysInMonth(int year, int month) =>
        DateTime.DaysInMonth(Math.Clamp(year, 1, 9999), Math.Clamp(month, 1, 12));

    public static string FormatServerTime(DateTime utc) =>
        AsUtc(utc).ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture) + " ST";

    public static string FormatServerTimeShort(DateTime utc) =>
        AsUtc(utc).ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);

    public static string FormatLocalDateTime(DateTime utc) =>
        AsUtc(utc).ToLocalTime().ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);

    public static string FormatServerTimeWithLocal(DateTime utc)
    {
        var serverTime = AsUtc(utc);
        var local = serverTime.ToLocalTime();

        var localPart = local.Date == serverTime.Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("ddd HH:mm", CultureInfo.InvariantCulture);

        return $"{FormatServerTime(serverTime)}  ({localPart} Local Time)";
    }

    public static bool IsWithinHour(DateTime utc) =>
        AsUtc(utc) - DateTime.UtcNow < TimeSpan.FromHours(1);

    public static string FormatCountdown(DateTime utc)
    {
        var remaining = AsUtc(utc) - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
            return remaining > TimeSpan.FromMinutes(-1) ? "starting now" : "overdue";

        if (remaining < TimeSpan.FromMinutes(1))
            return "in under a minute";

        if (remaining < TimeSpan.FromHours(1))
            return $"in {(int)remaining.TotalMinutes}m";

        if (remaining < TimeSpan.FromDays(1))
            return remaining.Minutes == 0
                ? $"in {(int)remaining.TotalHours}h"
                : $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";

        return remaining.Hours == 0
            ? $"in {(int)remaining.TotalDays}d"
            : $"in {(int)remaining.TotalDays}d {remaining.Hours}h";
    }

    public static string Ordinal(int value)
    {
        if (value is >= 11 and <= 13)
            return $"{value}th";

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
    }

    public static string WeekdayName(int mondayBasedIndex)
    {
        var day = (DayOfWeek)(((Math.Clamp(mondayBasedIndex, 0, 6) + 1) % 7));
        return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
