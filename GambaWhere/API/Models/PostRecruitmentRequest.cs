using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Body for POST /recruitment. Venue posts omit host fields and vice-versa.</summary>
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
}
