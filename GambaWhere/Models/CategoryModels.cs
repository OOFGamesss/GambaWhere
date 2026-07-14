using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Game type (category) API models returned by GET /games/types.</summary>
public class GameTypeDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("background")]
    public string Background { get; set; } = "0.50,0.50,0.50,0.12";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "0.75,0.75,0.75,1";

    [JsonPropertyName("discord_colour")]
    public int DiscordColour { get; set; } = 0x8040C0;

    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "🎲";

    [JsonPropertyName("discord_emoji")]
    public string DiscordEmoji { get; set; } = "🎲";

    [JsonPropertyName("banner_url")]
    public string BannerUrl { get; set; } = string.Empty;

    [JsonPropertyName("manual_fields")]
    public List<ManualRuleFieldDto>? ManualFields { get; set; }

    [JsonPropertyName("empty_rules_message")]
    public string? EmptyRulesMessage { get; set; }
}

public class ManualRuleFieldDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Text";

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public object? Default { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("text_max")]
    public int? TextMax { get; set; }
}
