using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Event API models: the event read model, its create and page response wrappers, and the create and update request bodies.</summary>
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

public class EventCreateResponse : EventResponse
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}

public class EventPageResponse
{
    [JsonPropertyName("items")]
    public EventResponse[] Items { get; set; } = Array.Empty<EventResponse>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

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

public class PutEventRequest
{
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("rules")]
    public Dictionary<string, object>? Rules { get; set; }

    [JsonPropertyName("game")]
    public string? Game { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("booster_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BoosterKey { get; set; }
}
