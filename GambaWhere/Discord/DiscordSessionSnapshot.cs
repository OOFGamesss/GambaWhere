using System.Collections.Generic;

namespace GambaWhere.Discord;

/// <summary>Immutable snapshot of session state used to build Discord webhook payloads.</summary>
internal readonly record struct DiscordSessionSnapshot(
    bool IsActive,
    string CharacterName,
    string GameType,
    string? VenueName,
    string Location,
    IReadOnlyDictionary<string, object>? Rules,
    string? DiscordUrl,
    string? ImageUrl);
