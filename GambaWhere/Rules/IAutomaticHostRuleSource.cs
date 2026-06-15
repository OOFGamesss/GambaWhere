using System.Collections.Generic;

namespace GambaWhere.Rules;

/// <summary>Optional capability for games whose live rules can be read from another plugin via IPC.</summary>
public interface IAutomaticHostRuleSource
{
    string AutomaticRulesPluginName { get; }

    bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules);
}
