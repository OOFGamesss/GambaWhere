using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleRoulette.Data;

public sealed class GameInfoIPC
{
    public int PlayerCount { get; set; }
    public int? MaxBetInner { get; set; }
    public int? MaxBetOuter { get; set; }
    public int? MaxBetInnerVIP { get; set; }
    public int? MaxBetOuterVIP { get; set; }
}
