using System.Collections.Generic;

namespace GambaWhere.Models;

/// <summary>Public IPC v2 contract for the automatic rules a partner plugin pushes via GambaWhere.SubmitRules.</summary>
public sealed class AutomaticRulesPayload
{
    public List<AutomaticRuleEntry> Rules { get; set; } = new();
}

public sealed class AutomaticRuleEntry
{
    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }
}
