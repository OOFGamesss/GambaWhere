namespace GambaWhere.Discord;

/// <summary>Value types describing the parts of a Discord multipart webhook request.</summary>
internal readonly record struct DiscordMultipartFilePart(byte[] Data, string Filename, int Slot);
