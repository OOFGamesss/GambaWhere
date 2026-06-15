using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Response returned by the API when a new event is created.</summary>
public class EventCreateResponse : EventResponse
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}
