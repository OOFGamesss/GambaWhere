using System.Collections.Generic;

namespace GambaWhere.IPC;

/// <summary>Public IPC v2 contract a partner plugin mirrors and passes to the GambaWhere.SubmitRules gate: up to ten rule entries, each carrying a display label and a value that must be a string, bool, int, long or double.</summary>
public sealed class AutomaticRulesPayload
{
    public List<AutomaticRuleEntry> Rules { get; set; } = new();
}

public sealed class AutomaticRuleEntry
{
    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }
}
