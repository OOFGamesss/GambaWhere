using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>POST /recruitment response: the saved post plus the session token used for edit/delete.</summary>
public class RecruitmentCreateResponse : RecruitmentPost
{
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = string.Empty;
}
