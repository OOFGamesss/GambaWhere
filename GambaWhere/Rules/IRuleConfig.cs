using System.Collections.Generic;

namespace GambaWhere.Rules;

/// <summary>Contract for a game's editable rule configuration.</summary>
public interface IRuleConfig
{

    string GameType { get; }

    void Draw();

    Dictionary<string, object> ToApiPayload();

    void LoadFromPreset(Dictionary<string, object> values);

    Dictionary<string, object> SaveToPreset();
}
