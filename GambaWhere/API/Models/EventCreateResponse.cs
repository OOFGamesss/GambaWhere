using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

public class EventCreateResponse : EventResponse
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}
