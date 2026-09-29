namespace SP621E.Screen;

using SP621E.Core.LedFrame;

/// <summary>
/// One downscaled screen grab: raw pixels plus the display-wide average. Pixels are
/// row-major, top-down, one <see cref="Rgb"/> per sample cell of the small capture
/// (e.g. 96 x 54) — small enough that every mapping below is cheap.
/// </summary>
public sealed class ScreenSnapshot
{
    public DateTimeOffset Timestamp { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public required IReadOnlyList<Rgb> Pixels { get; init; }

    /// <summary>Whole-display average color.</summary>
    public Rgb Average { get; init; }
}