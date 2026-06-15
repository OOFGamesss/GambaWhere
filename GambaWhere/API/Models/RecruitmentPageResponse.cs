using System;
using System.Text.Json.Serialization;

namespace GambaWhere.API.Models;

/// <summary>Paged collection of recruitment posts returned by the API.</summary>
public class RecruitmentPageResponse
{
    [JsonPropertyName("items")]
    public RecruitmentPost[] Items { get; set; } = Array.Empty<RecruitmentPost>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}
