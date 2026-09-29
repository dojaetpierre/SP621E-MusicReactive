using SP621E.Core.LedFrame;
using SP621E.Screen;

namespace SP621E.Bluetooth.Tests.Screen;

public class ScreenSyncPipelineTests
{
    private const int W = 16;
    private const int H = 8;
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static ScreenSnapshot Snapshot(Func<int, int, Rgb> pixel, Rgb? average = null)
    {
        var pixels = new Rgb[W * H];
        for (var r = 0; r < H; r++)
            for (var c = 0; c < W; c++)
                pixels[r * W + c] = pixel(r, c);
        return new ScreenSnapshot
        {
            Timestamp = Now,
            Width = W,
            Height = H,
            Pixels = pixels,
            Average = average ?? pixel(0, 0),
        };
    }

    [Fact]
    public void AverageMode_FillsEveryLedWithTheDisplayAverage()
    {
        var snap = Snapshot((_, _) => new Rgb(10, 20, 30), average: new Rgb(10, 20, 30));
        var frame = new ScreenSyncPipeline(4).Step(snap, ScreenSyncMode.Average, 0.0, 1.0);

        Assert.Equal(4, frame.PixelCount);
        Assert.All(frame.Pixels, p => Assert.Equal(new Rgb(10, 20, 30), p));
    }

    [Fact]
    public void ColumnsMode_SplitsLeftToRightAcrossLedCount()
    {
        // Left half red, right half blue.
        var red = new Rgb(255, 0, 0);
        var blue = new Rgb(0, 0, 255);
        var snap = Snapshot((_, c) => c < W / 2 ? red : blue);

        var frame = new ScreenSyncPipeline(4).Step(snap, ScreenSyncMode.Columns, 0.0, 1.0);

        // Column bands (16 wide / 4 LEDs): 0-3 red, 4-7 red, 8-11 blue, 12-15 blue.
        Assert.Equal(red, frame.Pixels[0]);
        Assert.Equal(red, frame.Pixels[1]);
        Assert.Equal(blue, frame.Pixels[2]);
        Assert.Equal(blue, frame.Pixels[3]);
    }

    [Fact]
    public void BottomEdgeMode_IgnoresUpperRows()
    {
        // Top row white, everything else pure green -> bottom edge must be green.
        var green = new Rgb(0, 255, 0);
        var snap = Snapshot((r, _) => r == 0 ? new Rgb(255, 255, 255) : green);

        var frame = new ScreenSyncPipeline(2).Step(snap, ScreenSyncMode.BottomEdge, 0.0, 1.0);

        Assert.All(frame.Pixels, p => Assert.Equal(green, p));
    }

    [Fact]
    public void Smoothing_MovesTowardTargetGentlyThenConverges()
    {
        var black = new Rgb(0, 0, 0);
        var white = new Rgb(255, 255, 255);
        var warm = Snapshot((_, _) => black, average: black);
        var to = Snapshot((_, _) => white, average: white);
        var pipeline = new ScreenSyncPipeline(4);

        pipeline.Step(warm, ScreenSyncMode.Average, 1.0, 1.0);               // warm-up: prev = black
        var first = pipeline.Step(to, ScreenSyncMode.Average, 1.0, 1.0);     // one 10% step toward white
        var second = pipeline.Step(to, ScreenSyncMode.Average, 1.0, 1.0);    // another 10% of the remainder

        Assert.True(first.Pixels[0].R > 0, "first smoothed frame leaves pure black");
        Assert.True(second.Pixels[0].R > first.Pixels[0].R, "second frame is closer to target");
        Assert.True(second.Pixels[0].R < 255, "slow smoothing has not fully converged");
    }

    [Fact]
    public void Brightness_ScalesOutput()
    {
        var snap = Snapshot((_, _) => new Rgb(200, 100, 0), average: new Rgb(200, 100, 0));
        var frame = new ScreenSyncPipeline(2).Step(snap, ScreenSyncMode.Average, 0.0, 0.5);

        Assert.Equal(new Rgb(100, 50, 0), frame.Pixels[0]);
    }

    [Fact]
    public void Reset_ClearsSmoothingHistory()
    {
        var snap = Snapshot((_, _) => new Rgb(255, 255, 255), average: new Rgb(255, 255, 255));
        var pipeline = new ScreenSyncPipeline(2);

        pipeline.Step(snap, ScreenSyncMode.Average, 1.0, 1.0);
        pipeline.Reset();
        var after = pipeline.Step(snap, ScreenSyncMode.Average, 0.0, 1.0);

        Assert.Equal(new Rgb(255, 255, 255), after.Pixels[0]);
    }
}