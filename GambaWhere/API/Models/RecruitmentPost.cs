using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>A recruitment post returned by the API (Find a Venue / Find a Host).</summary>
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
