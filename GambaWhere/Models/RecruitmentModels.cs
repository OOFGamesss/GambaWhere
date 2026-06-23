using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Recruitment API models: the post read model, schedule entry, create and page response wrappers, and request bodies.</summary>
public class RecruitmentPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("post_type")]
    public string PostType { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("poster_character")]
    public string PosterCharacter { get; set; } = string.Empty;

    [JsonPropertyName("data_centre")]
    public string DataCentre { get; set; } = string.Empty;

    [JsonPropertyName("nsfw")]
    public string Nsfw { get; set; } = "SFW";

    [JsonPropertyName("bank")]
    public bool Bank { get; set; }

    [JsonPropertyName("schedule")]
    public List<RecruitmentScheduleEntry> Schedule { get; set; } = new();

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("games")]
    public List<string> Games { get; set; } = new();

    [JsonPropertyName("discord")]
    public string? Discord { get; set; }

    [JsonPropertyName("venue_name")]
    public string? VenueName { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("companion_plugins")]
    public List<string> CompanionPlugins { get; set; } = new();

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

public class RecruitmentCreateResponse : RecruitmentPost
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}

public class RecruitmentPageResponse
{
    [JsonPropertyName("items")]
    public RecruitmentPost[] Items { get; set; } = Array.Empty<RecruitmentPost>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

public class PostRecruitmentRequest
{
    [JsonPropertyName("post_type")]
    public string PostType { get; set; } = string.Empty;

    [JsonPropertyName("poster_character")]
    public string PosterCharacter { get; set; } = string.Empty;

    [JsonPropertyName("data_centre")]
    public string DataCentre { get; set; } = string.Empty;

    [JsonPropertyName("nsfw")]
    public string Nsfw { get; set; } = "SFW";

    [JsonPropertyName("bank")]
    public bool Bank { get; set; }

    [JsonPropertyName("schedule")]
    public List<RecruitmentScheduleEntry> Schedule { get; set; } = new();

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("games")]
    public List<string> Games { get; set; } = new();

    [JsonPropertyName("discord")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Discord { get; set; }

    [JsonPropertyName("venue_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VenueName { get; set; }

    [JsonPropertyName("profile_picture_b64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfilePictureB64 { get; set; }

    [JsonPropertyName("profile_image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("companion_plugins")]
    public List<string> CompanionPlugins { get; set; } = new();

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
}

public class PutRecruitmentRequest
{
    [JsonPropertyName("data_centre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataCentre { get; set; }

    [JsonPropertyName("nsfw")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nsfw { get; set; }

    [JsonPropertyName("bank")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Bank { get; set; }

    [JsonPropertyName("schedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecruitmentScheduleEntry>? Schedule { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("games")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Games { get; set; }

    [JsonPropertyName("discord")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Discord { get; set; }

    [JsonPropertyName("venue_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VenueName { get; set; }

    [JsonPropertyName("profile_picture_b64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfilePictureB64 { get; set; }

    [JsonPropertyName("profile_image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("companion_plugins")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CompanionPlugins { get; set; }

    [JsonPropertyName("bio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Bio { get; set; }

    [JsonPropertyName("preferred_games")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PreferredGames { get; set; }

    [JsonPropertyName("booster_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BoosterKey { get; set; }

    [JsonPropertyName("border_style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BorderStyle { get; set; }

    [JsonPropertyName("card_effect_style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CardEffectStyle { get; set; }
}
