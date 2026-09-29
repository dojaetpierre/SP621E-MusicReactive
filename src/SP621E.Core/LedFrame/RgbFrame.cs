namespace SP621E.Core.LedFrame;

/// <summary>
/// A generic, controller-agnostic pixel frame: an array of RGB triples plus metadata.
/// Milestone 6 finalizes the full API (construction, cloning, timestamping). For now
/// this is a minimal, immutable bearer type so the pipeline seams exist.
/// </summary>
public sealed class RgbFrame
{
    public RgbFrame(int pixelCount, IReadOnlyList<Rgb> pixels, DateTimeOffset timestamp)
    {
        if (pixelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelCount), "Pixel count must be positive.");
        if (pixels is null || pixels.Count != pixelCount)
            throw new ArgumentException("Pixels array length must equal pixel count.", nameof(pixels));

        PixelCount = pixelCount;
        Pixels = pixels;
        Timestamp = timestamp;
    }

    public int PixelCount { get; }

    public IReadOnlyList<Rgb> Pixels { get; }

    public DateTimeOffset Timestamp { get; }

    public static RgbFrame FromUniform(int pixelCount, Rgb color, DateTimeOffset timestamp)
    {
        var pixels = new Rgb[pixelCount];
        Array.Fill(pixels, color);
        return new RgbFrame(pixelCount, pixels, timestamp);
    }

    public RgbFrame WithTimestamp(DateTimeOffset timestamp) => new(PixelCount, Pixels, timestamp);

    /// <summary>Returns a new frame where each pixel is transformed by <paramref name="mapper"/>.</summary>
    public RgbFrame Map(Func<Rgb, int, Rgb> mapper)
    {
        var pixels = new Rgb[PixelCount];
        for (var i = 0; i < PixelCount; i++)
            pixels[i] = mapper(Pixels[i], i);
        return new RgbFrame(PixelCount, pixels, Timestamp);
    }
}