using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Scheduled session API models: the schedule read model, its create, page and advance response wrappers and the create, edit and advance request bodies.</summary>
public class ScheduledEventResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

    [JsonPropertyName("scheduled_for")]
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime ScheduledFor { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("rules")]
    public Dictionary<string, object> Rules { get; set; } = new();

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("venue_name")]
    public string? VenueName { get; set; }

    [JsonPropertyName("discord_url")]
    public string? DiscordUrl { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("recurrence")]
    public string Recurrence { get; set; } = ScheduleRecurrence.Once;

    [JsonPropertyName("recurrence_day")]
    public int? RecurrenceDay { get; set; }

    [JsonPropertyName("recurrence_week")]
    public int? RecurrenceWeek { get; set; }

    [JsonPropertyName("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("preferred_games")]
    public List<string> PreferredGames { get; set; } = new();

    [JsonPropertyName("booster")]
    public bool Booster { get; set; }

    [JsonPropertyName("border_style")]
    public string? BorderStyle { get; set; }

    [JsonPropertyName("card_effect_style")]
    public string? CardEffectStyle { get; set; }
}

public class ScheduledEventCreateResponse : ScheduledEventResponse
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}

public class ScheduledEventPageResponse
{
    [JsonPropertyName("items")]
    public ScheduledEventResponse[] Items { get; set; } = Array.Empty<ScheduledEventResponse>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

public class ScheduledAdvanceResponse
{
    [JsonPropertyName("removed")]
    public bool Removed { get; set; }

    [JsonPropertyName("schedule")]
    public ScheduledEventResponse? Schedule { get; set; }
}

public class PostScheduledEventRequest
{
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

    [JsonPropertyName("scheduled_for")]
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime ScheduledFor { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("rules")]
    public Dictionary<string, object> Rules { get; set; } = new();

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("venue_name")]
    public string VenueName { get; set; } = string.Empty;

    [JsonPropertyName("recurrence")]
    public string Recurrence { get; set; } = ScheduleRecurrence.Once;

    [JsonPropertyName("data_centre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataCentre { get; set; }

    [JsonPropertyName("world")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? World { get; set; }

    [JsonPropertyName("profile_picture_b64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfilePictureB64 { get; set; }

    [JsonPropertyName("profile_image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("bio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Bio { get; set; }

    [JsonPropertyName("preferred_games")]
    public List<string> PreferredGames { get; set; } = new();

    [JsonPropertyName("booster_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BoosterKey { get; set; }

    [JsonPropertyName("border_style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BorderStyle { get; set; }

    [JsonPropertyName("card_effect_style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CardEffectStyle { get; set; }

    [JsonPropertyName("request_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; set; }
}

public class PutScheduledEventRequest
{
    [JsonPropertyName("scheduled_for")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime? ScheduledFor { get; set; }

    [JsonPropertyName("location")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("recurrence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Recurrence { get; set; }

    [JsonIgnore]
    public bool HasChanges =>
        ScheduledFor != null || Location != null || Description != null || Recurrence != null;
}

public class AdvanceScheduledRequest
{
    [JsonPropertyName("occurrence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime? Occurrence { get; set; }
}

public static class ScheduleRecurrence
{
    public const string Once = "once";
    public const string Weekly = "weekly";
    public const string Fortnightly = "fortnightly";
    public const string MonthlyDate = "monthly_date";
    public const string MonthlyWeekday = "monthly_weekday";

    public static readonly string[] All =
    {
        Once, Weekly, Fortnightly, MonthlyDate, MonthlyWeekday
    };

    public static readonly string[] Labels =
    {
        "One Off", "Weekly", "Fortnightly", "Monthly", "Monthly by Day"
    };

    public static int MondayBased(DateTime moment) => ((int)moment.DayOfWeek + 6) % 7;

    public static string Describe(string? recurrence, DateTime occurrence, int? day, int? week) => recurrence switch
    {
        Weekly => $"Every {occurrence.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)}",
        Fortnightly => $"Every 2nd {occurrence.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)}",
        MonthlyDate => $"Monthly on the {Utility.ServerTime.Ordinal(day ?? occurrence.Day)}",
        MonthlyWeekday => week is 5
            ? $"Last {Utility.ServerTime.WeekdayName(day ?? MondayBased(occurrence))} monthly"
            : $"{Utility.ServerTime.Ordinal(week ?? 1)} {Utility.ServerTime.WeekdayName(day ?? MondayBased(occurrence))} monthly",
        _ => "One off"
    };
}
