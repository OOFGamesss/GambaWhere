using System.Collections.Generic;

namespace GambaWhere.Games;

/// <summary>Models for a Game and the rule-field schema (categories live in GameCategories.cs). A RuleField is a manual
/// field on a category (Kind/Label/Default/Min/Max/Options drive the editor) or an automatic field on a game
/// (Source/Multiplier/SkipIf*/SpacesFromUnderscores/AutoType map a partner IPC JSON value onto a rule key). Name is the
/// shared rule key.</summary>

public enum RuleValueType
{
    String,
    Int,
    Long,
    Float,
    Bool
}

public enum RuleKind
{
    None,
    Money,
    Int,
    Float,
    Toggle,
    Text,
    Combo,
    ItemSearch
}

public sealed record RuleField(
    string Name,
    RuleKind Kind = RuleKind.None,
    string Label = "",
    object? Default = null,
    double? Min = null,
    double? Max = null,
    IReadOnlyList<string>? Options = null,
    int TextMax = 64,
    string? Source = null,
    double Multiplier = 1.0,
    bool SkipIfMissing = false,
    bool SkipIfZero = false,
    bool SpacesFromUnderscores = false,
    RuleValueType? AutoType = null)
{
    public string SourceKey => Source ?? Name;

    public RuleValueType ResolvedAutoType => AutoType ?? Kind switch
    {
        RuleKind.Money => RuleValueType.Long,
        RuleKind.Int => RuleValueType.Int,
        RuleKind.Float => RuleValueType.Float,
        RuleKind.Toggle => RuleValueType.Bool,
        _ => RuleValueType.String
    };
}

public sealed record AutomaticRuleShape(
    string GetInfoKey,
    IReadOnlyList<RuleField> Fields);

public sealed record Game(
    string Category,
    string CompanionPlugin,
    string Description,
    string Creator,
    string Url,
    string IconFile,
    IReadOnlyList<RuleField>? AutomaticFields = null,
    string? IpcBaseName = null,
    bool UsesGameJoined = false,
    IReadOnlyList<AutomaticRuleShape>? AutomaticShapesOverride = null,
    IReadOnlyList<string>? InvalidationKeysOverride = null)
{
    public bool HasIpc => !string.IsNullOrEmpty(IpcBaseName);

    public string WindowOpenedKey => $"{IpcBaseName}.WindowOpened";

    public IReadOnlyList<AutomaticRuleShape> RuleShapes
    {
        get
        {
            if (AutomaticShapesOverride != null)
                return AutomaticShapesOverride;

            return AutomaticFields is { Count: > 0 }
                ? new[] { new AutomaticRuleShape($"{IpcBaseName}.GetGameInfo", AutomaticFields) }
                : System.Array.Empty<AutomaticRuleShape>();
        }
    }

    public IReadOnlyList<string> InvalidationKeys =>
        InvalidationKeysOverride
        ?? (UsesGameJoined ? new[] { $"{IpcBaseName}.GameJoined" } : System.Array.Empty<string>());

    public bool HasAutomaticRules => RuleShapes.Count > 0;
}
