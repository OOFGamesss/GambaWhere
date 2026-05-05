using System.Collections.Generic;

namespace GambaWhere.Rules;

/// <summary>Optional capability for games whose live rules can be read from another plugin via IPC.</summary>
public interface IAutomaticHostRuleSource
{
    /// <summary>Short name of the plugin supplying automatic rules (shown in the host UI).</summary>
    string AutomaticRulesPluginName { get; }

    /// <summary>Builds the rules dictionary for the API when automatic mode is active.</summary>
    /// <returns>False when <paramref name="ipcContext"/> is missing or invalid.</returns>
    bool TryGetAutomaticApiRules(object? ipcContext, out Dictionary<string, object>? rules);

    /// <summary>Draws read-only automatic-rule details (ImGui) for the host panel.</summary>
    void DrawAutomaticRulesSummary(object? ipcContext);
}
