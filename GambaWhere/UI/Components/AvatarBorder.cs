namespace GambaWhere.UI.Components;

/// <summary>Resolves the bundled image path for a profile avatar border style.</summary>
internal static class AvatarBorder
{
    internal static string? ImagePath(string? borderStyle, bool boosterFallback)
    {
        var effective = borderStyle ?? (boosterFallback ? "booster" : "none");
        return effective switch
        {
            "booster" => "Profile Borders/boosterborder.png",
            "gwbeta"  => "Profile Borders/gwbetaborder.png",
            _         => null,
        };
    }
}
