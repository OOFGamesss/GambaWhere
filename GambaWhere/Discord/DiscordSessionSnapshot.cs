using System.Collections.Generic;

namespace GambaWhere.Discord;

internal readonly record struct DiscordSessionSnapshot(
    bool IsActive,
    string CharacterName,
    string GameType,
    string? VenueName,
    string Location,
    IReadOnlyDictionary<string, object>? Rules,
    string? DiscordUrl,
    string? ImageUrl);
