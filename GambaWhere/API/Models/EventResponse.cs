using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Event record as returned by the GambaWhere API.</summary>
public class EventResponse
{
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

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

    [JsonPropertyName("data_centre")]
    public string? DataCentre { get; set; }

    [JsonPropertyName("world")]
    public string? World { get; set; }

    [JsonPropertyName("area")]
    public string? Area { get; set; }

    [JsonPropertyName("ward")]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    public int? Plot { get; set; }

    [JsonPropertyName("is_apartment")]
    public bool? IsApartment { get; set; }

    [JsonPropertyName("subdivision")]
    public bool? Subdivision { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

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
