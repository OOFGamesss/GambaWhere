namespace GambaWhere.Discord;

internal readonly record struct DiscordMultipartFilePart(byte[] Data, string Filename, int Slot);
