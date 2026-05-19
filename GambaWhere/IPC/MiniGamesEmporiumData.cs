namespace GambaWhere.IPC;

public sealed class BAR777Data
{
    public string GameLabel { get; set; } = string.Empty;
    public long BoostedPot { get; set; }
    public long TotalPot { get; set; }
    public long CostPerRoll { get; set; }
    public int MaxRolls { get; set; }
    public int PlayersPlayed { get; set; }
    public int? Queue { get; set; }
}

public sealed class DeathrollTournamentData
{
    public string GameLabel { get; set; } = string.Empty;
    public string Round { get; set; } = string.Empty;
    public long BoostedPot { get; set; }
    public long TotalPot { get; set; }
    public long EntryCost { get; set; }
    public int PlayersEntered { get; set; }
}
