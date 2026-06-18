using System;
using System.Collections.Generic;

namespace GambaWhere.IPC;

/// <summary>Runtime state for one IPC v2 partner: its display name, current category, last pushed rules and the timestamps that drive channel liveness, the thirty-second rules expiry and the auto-session prompt debounce.</summary>
internal sealed class PartnerChannelV2
{
    private const long LiveTtlMs = 45_000;
    private const long RulesTtlMs = 45_000;

    private Dictionary<string, object>? _rules;
    private long _lastSeenTick;
    private long _lastRulesTick;
    private long _lastPromptTick;

    public PartnerChannelV2(string pluginName, string category, uint linkId)
    {
        PluginName = pluginName;
        Category = category;
        LinkId = linkId;
    }

    public string PluginName { get; }

    public uint LinkId { get; }

    public string Category { get; set; }

    public bool IsLive => Environment.TickCount64 - _lastSeenTick < LiveTtlMs;

    public void Touch() => _lastSeenTick = Environment.TickCount64;

    public bool TryMarkPrompted(long debounceMs)
    {
        var now = Environment.TickCount64;
        if (_lastPromptTick != 0 && now - _lastPromptTick < debounceMs)
            return false;

        _lastPromptTick = now;
        return true;
    }

    public void SetRules(Dictionary<string, object> rules)
    {
        _rules = rules;
        _lastRulesTick = Environment.TickCount64;
        _lastSeenTick = _lastRulesTick;
    }

    public Dictionary<string, object>? GetRules() =>
        _rules != null && Environment.TickCount64 - _lastRulesTick < RulesTtlMs ? _rules : null;
}
