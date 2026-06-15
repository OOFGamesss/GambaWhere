using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Request body for updating an existing event via the API.</summary>
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
