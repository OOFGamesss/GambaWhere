using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GambaWhere.Models;

/// <summary>Discord webhook models: the session snapshot and the JSON payload DTOs sent to Discord.</summary>
internal readonly record struct DiscordSessionSnapshot(
    bool IsActive,
    string CharacterName,
    string GameType,
    string? VenueName,
    string Location,
    IReadOnlyDictionary<string, object>? Rules,
    string? DiscordUrl,
    string? ImageUrl);

internal sealed class DiscordOutboundPayloadDto
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("embeds")]
    public List<DiscordEmbedDto> Embeds { get; init; } = [];

    [JsonPropertyName("attachments")]
    public List<DiscordAttachmentDto>? Attachments { get; init; }
}

internal sealed class DiscordEmbedDto
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("color")]
    public int Color { get; init; }

    [JsonPropertyName("thumbnail")]
    public DiscordMediaDto? Thumbnail { get; init; }

    [JsonPropertyName("image")]
    public DiscordMediaDto? Image { get; init; }

    [JsonPropertyName("fields")]
    public List<DiscordEmbedFieldDto>? Fields { get; init; }

    [JsonPropertyName("footer")]
    public DiscordFooterDto? Footer { get; init; }
}

internal sealed class DiscordEmbedFieldDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("inline")]
    public bool Inline { get; init; }
}

internal sealed class DiscordFooterDto
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; init; }
}

internal sealed class DiscordMediaDto
{
    public DiscordMediaDto(string url) => Url = url;

    [JsonPropertyName("url")]
    public string Url { get; }
}

internal sealed class DiscordAttachmentDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("filename")]
    public required string Filename { get; init; }
}
