using System.Numerics;

namespace GambaWhere.UI.Components;

/// <summary>
/// Holds the host panel's accent colours for the current frame so that shared field helpers can theme themselves.
/// </summary>
internal static class HostFieldTheme
{
    internal static Vector4 Primary { get; set; } = new(0.5f, 0.3f, 0.8f, 1f);

    internal static Vector4 Secondary { get; set; } = new(0.8f, 0.65f, 1f, 1f);
}
