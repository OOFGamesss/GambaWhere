using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GambaWhere.Utility;

namespace GambaWhere.Rules;

public class BingoRules : IRuleConfig
{
    public string GameType => "Bingo";

    private int _maxTickets = 10;
    private int _pricePerTicket = 10000;
    private int _bingoPot = 100000;
    private int _maxWinnersPerRound = 1;

    private static readonly string[] Labels =
    {
        "Max Tickets", "Price per Ticket (gil)", "Bingo Pot (gil)", "Max Winners per Round"
    };

    public void Draw()
    {
        var offset = Labels.Max(l => ImGui.CalcTextSize(l).X) + 16f * ImGuiHelpers.GlobalScale;

        ImGui.Text("Max Tickets");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxTickets", ref _maxTickets);

        ImGui.Text("Price per Ticket (gil)");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##PricePerTicket", ref _pricePerTicket);

        ImGui.Text("Bingo Pot (gil)");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##BingoPot", ref _bingoPot);

        ImGui.Text("Max Winners per Round");
        ImGui.SameLine(offset);
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("##MaxWinnersPerRound", ref _maxWinnersPerRound);

        _maxTickets = RuleClamp.Min(_maxTickets, 1);
        _pricePerTicket = RuleClamp.Min(_pricePerTicket, 0);
        _bingoPot = RuleClamp.Min(_bingoPot, 0);
        _maxWinnersPerRound = RuleClamp.Min(_maxWinnersPerRound, 1);
    }

    public Dictionary<string, object> ToApiPayload() => new()
    {
        { "maxTickets", _maxTickets },
        { "pricePerTicket", _pricePerTicket },
        { "bingoPot", _bingoPot },
        { "maxWinnersPerRound", _maxWinnersPerRound }
    };

    public void LoadFromPreset(Dictionary<string, object> values)
    {
        _maxTickets = PresetReader.Int(values, "maxTickets", _maxTickets);
        _pricePerTicket = PresetReader.Int(values, "pricePerTicket", _pricePerTicket);
        _bingoPot = PresetReader.Int(values, "bingoPot", _bingoPot);
        _maxWinnersPerRound = PresetReader.Int(values, "maxWinnersPerRound", _maxWinnersPerRound);
    }

    public Dictionary<string, object> SaveToPreset() => ToApiPayload();
}
