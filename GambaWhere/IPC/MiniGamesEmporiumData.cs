namespace GambaWhere.IPC;

public sealed class MiniGamesEmporiumData
{
    public string GameLabel { get; set; } = string.Empty;
    public long BoostedPot { get; set; }
    public long TotalPot { get; set; }
    public long CostPerRoll { get; set; }
    public int PlayersPlayed { get; set; }
    public int? Queue { get; set; }
}
