using System;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Game store API models: an approved game listing returned by GET /games.</summary>
public class GameStoreGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("game_type")]
    public string GameType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public string Authors { get; set; } = string.Empty;

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("max_players")]
    public int? MaxPlayers { get; set; }

    [JsonPropertyName("players_over_24")]
    public bool PlayersOver24 { get; set; }

    [JsonPropertyName("repo_url")]
    public string? RepoUrl { get; set; }

    [JsonPropertyName("info_url")]
    public string? InfoUrl { get; set; }

    [JsonPropertyName("discord_invite_url")]
    public string? DiscordInviteUrl { get; set; }

    [JsonPropertyName("developer_name")]
    public string? DeveloperName { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTime? ApprovedAt { get; set; }
}
