using SP621E.Screen;

namespace SP621E.Bluetooth.Tests.Screen;

/// <summary>
/// Live smoke test for the GDI grab: needs an interactive desktop session
/// (true in this environment). Verifies the capture returns cells and that
/// repeated grabs do not leak/hang.
/// </summary>
public class ScreenSamplerCaptureTests
{
    [Fact]
    public void Capture_ReturnsNonEmptySnapshot()
    {
        var sampler = new ScreenSampler();
        var snap = sampler.Capture();

        Assert.Null(sampler.LastError);
        Assert.NotNull(snap);
        Assert.True(snap!.Width > 0, "capture width must be positive");
        Assert.True(snap.Height > 0, "capture height must be positive");
        Assert.Equal(snap.Width * snap.Height, snap.Pixels.Count);
    }

    [Fact]
    public void Capture_RepeatedGrabsAreIndependent()
    {
        var sampler = new ScreenSampler();
        var first = sampler.Capture();
        var second = sampler.Capture();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(second!.Timestamp >= first!.Timestamp);
        Assert.NotSame(first.Pixels, second.Pixels);
    }
}