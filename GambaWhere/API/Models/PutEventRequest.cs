using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

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
}
