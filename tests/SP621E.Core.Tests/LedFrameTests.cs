using SP621E.Core.LedFrame;

namespace SP621E.Core.Tests;

public class LedFrameTests
{
    [Fact]
    public void Rgb_Equality_Is_Componentwise()
    {
        Assert.Equal(new Rgb(1, 2, 3), new Rgb(1, 2, 3));
        Assert.NotEqual(new Rgb(1, 2, 3), new Rgb(1, 2, 4));
    }

    [Fact]
    public void RgbFrame_Rejects_Wrong_Pixel_Count()
    {
        var pixels = new[] { new Rgb(0, 0, 0), new Rgb(1, 1, 1) };
        Assert.Throws<ArgumentException>(() => new RgbFrame(3, pixels, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RgbFrame_Accepts_Well_Formed_Frame()
    {
        var pixels = new[] { new Rgb(10, 20, 30) };
        var frame = new RgbFrame(1, pixels, DateTimeOffset.UtcNow);
        Assert.Equal(1, frame.PixelCount);
        Assert.Equal(new Rgb(10, 20, 30), frame.Pixels[0]);
    }
}