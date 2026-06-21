using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Request body for creating a new event via the API.</summary>
public class PostEventRequest
{
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

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
}
