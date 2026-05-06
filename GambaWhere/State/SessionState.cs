using System.Collections.Generic;
using System.Threading;

namespace GambaWhere.State;

public class SessionState
{
    public bool IsActive { get; set; }

    public string SessionToken { get; set; } = string.Empty;

    public string CharacterName { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string? VenueName { get; set; }

    public Dictionary<string, object>? ActiveRules { get; set; }

    public bool UsesAutomaticHostRules { get; set; }

    public CancellationTokenSource? LoopCts { get; set; }

    public void Clear()
    {
        IsActive = false;
        SessionToken = string.Empty;
        CharacterName = string.Empty;
        Location = string.Empty;
        GameType = string.Empty;
        VenueName = null;
        ActiveRules = null;
        UsesAutomaticHostRules = false;
        LoopCts = null;
    }
}
