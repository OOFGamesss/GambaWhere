using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

public class PostEventRequest
{
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("rules")]
    public Dictionary<string, object> Rules { get; set; } = new();

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("venue_name")]
    public string VenueName { get; set; } = string.Empty;
}
