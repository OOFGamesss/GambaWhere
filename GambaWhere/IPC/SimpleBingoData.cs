using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleBingo.Data;

/// <summary>Data contracts mirrored from the Simple Bingo plugin IPC.</summary>
public sealed class GameInfoIPC
{
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public GameTypeEnum GameType { get; set; }
    public float BoostedPot { get; set; }
    public long TotalPot { get; set; }
    public bool ChaosMode { get; set; }
    public bool MultiWinner { get; set; }
    public int CardCost { get; set; }
    public int CardsSold { get; set; }
    public int PlayerCount { get; set; }
}

public enum GameTypeEnum
{
    Progressive = 0,
    Progressive_Clear = 1,
    Progressive_2 = 2,
    Progressive_2_Clear = 3,
    Progressive_3 = 4,
    Progressive_3_Clear = 5,
    One_Line = 6,
    Two_Lines = 7,
    Full_Board = 8,
    Four_Corners = 9,
    Outside_Edge = 10,
}
