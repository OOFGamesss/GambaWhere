using System;
using System.Collections.Generic;

namespace GambaWhere.Config;

/// <summary>
/// A locally stored host profile. The character name is never stored here; the
/// API derives it from the event's character_name when a profile is attached.
/// </summary>
[Serializable]
public class GambaProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? ImageFileName { get; set; }

    public string Bio { get; set; } = string.Empty;

    public List<string> PreferredGames { get; set; } = new();

    public bool Booster { get; set; }

    public string BorderStyle { get; set; } = "none";

    public string CardEffectStyle { get; set; } = "none";

    public string? UploadedImageUrl { get; set; }

    public string? UploadedImageHash { get; set; }

    public const int BioMaxLength = 255;
}
