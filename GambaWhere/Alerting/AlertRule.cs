using System;
using System.Collections.Generic;

namespace GambaWhere.Alerting;

[Serializable]
public class AlertRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public HashSet<string> GameTypes { get; set; } = new();
    public HashSet<string> DataCentres { get; set; } = new();
    public HashSet<string> VenueNames { get; set; } = new();

    public bool IsInert => GameTypes.Count == 0 && DataCentres.Count == 0 && VenueNames.Count == 0;
}
