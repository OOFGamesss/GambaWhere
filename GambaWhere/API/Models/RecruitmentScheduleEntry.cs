using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>One opening/availability day with a start and end time on a 24-hour Server Time clock.</summary>
public class RecruitmentScheduleEntry
{
    [JsonPropertyName("day")]
    public string Day { get; set; } = string.Empty;

    [JsonPropertyName("start_hour")]
    public int StartHour { get; set; }

    [JsonPropertyName("start_minute")]
    public int StartMinute { get; set; }

    [JsonPropertyName("end_hour")]
    public int EndHour { get; set; }

    [JsonPropertyName("end_minute")]
    public int EndMinute { get; set; }
}
