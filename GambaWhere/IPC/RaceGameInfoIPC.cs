using System;

namespace GambaWhere.IPC;

/// <summary>
/// Serialisable race settings snapshot from Chocobo Racing (active preset plus live party size).
/// </summary>
[Serializable]
public sealed class RaceGameInfoIPC
{
    public int ChocoboRunners { get; set; }

    public int RaceTrackLength { get; set; }

    public long MaxBetPerChocobo { get; set; }

    public float PayoutOdds { get; set; }

    public float PerfectRaceOdds { get; set; }

    public int CurrentPlayers { get; set; }
}
