namespace SP621E.Screen;

/// <summary>
/// How screen colors are turned into LED strip colors.
/// </summary>
public enum ScreenSyncMode
{
    /// <summary>All LEDs follow the whole-display average color.</summary>
    Average = 0,

    /// <summary>A strip of columns sampled along the bottom edge of the display (classic ambilight).</summary>
    BottomEdge = 1,

    /// <summary>A strip of columns sampled across the full display height (left-to-right linearization).</summary>
    Columns = 2,
}