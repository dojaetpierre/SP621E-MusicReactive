namespace SP621E.Core.LedFrame;

/// <summary>
/// A single RGB color. This type is final (Milestone 6 may add helpers, not fields).
/// </summary>
public readonly struct Rgb(byte r, byte g, byte b) : IEquatable<Rgb>
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;

    public override bool Equals(object? obj) => obj is Rgb other && Equals(other);

    public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;

    public override int GetHashCode() => HashCode.Combine(R, G, B);

    public static bool operator ==(Rgb left, Rgb right) => left.Equals(right);

    public static bool operator !=(Rgb left, Rgb right) => !left.Equals(right);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Returns this color scaled by <paramref name="f"/> (component values clamped to byte range).</summary>
    public Rgb Scale(double f) => new(
        (byte)Math.Min(byte.MaxValue, Math.Max(0, Math.Round(R * f))),
        (byte)Math.Min(byte.MaxValue, Math.Max(0, Math.Round(G * f))),
        (byte)Math.Min(byte.MaxValue, Math.Max(0, Math.Round(B * f))));

    public static Rgb Lerp(Rgb a, Rgb b, double t) => new(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>HSV to RGB. <paramref name="h"/> in [0, 360), s/v in [0, 1].</summary>
    public static Rgb FromHsv(double h, double s, double v)
    {
        var hue = ((h % 360.0) + 360.0) % 360.0;
        var c = v * s;
        var x = c * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
        var m = v - c;

        var (r, g, b) = hue switch
        {
            < 60.0 => (c, x, 0.0),
            < 120.0 => (x, c, 0.0),
            < 180.0 => (0.0, c, x),
            < 240.0 => (0.0, x, c),
            < 300.0 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new Rgb(
            (byte)Math.Round((r + m) * 255.0),
            (byte)Math.Round((g + m) * 255.0),
            (byte)Math.Round((b + m) * 255.0));
    }
}