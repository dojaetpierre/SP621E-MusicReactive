namespace SP621E.Screen;

using SP621E.Core.LedFrame;

/// <summary>
/// Turns <see cref="ScreenSnapshot"/> values into LED frames for the chosen
/// <see cref="ScreenSyncMode"/>, with per-LED EMA smoothing to suppress flicker.
/// Pure and side-effect free (holds only the previous smoothed frame), so it is
/// fully unit-testable without a display.
/// </summary>
public sealed class ScreenSyncPipeline
{
    private readonly int _ledCount;
    private Rgb[]? _smoothed;

    public ScreenSyncPipeline(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount), "LED count must be positive.");
        _ledCount = ledCount;
    }

    public int LedCount => _ledCount;

    /// <summary>
    /// Maps one grab to a frame. <paramref name="smoothness"/> in [0,1] (0 = instant,
    /// 1 = very laggy/smooth); <paramref name="brightness"/> in [0,1] scales the output.
    /// </summary>
    public RgbFrame Step(ScreenSnapshot snapshot, ScreenSyncMode mode, double smoothness, double brightness)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var targets = ComputeTargets(snapshot, mode);
        var t = Math.Clamp(1.0 - smoothness * 0.9, 0.05, 1.0); // smoothness 0 -> 1 (instant), 1 -> 0.1 (slow)
        var prev = _smoothed;

        var stored = new Rgb[_ledCount];
        var output = new Rgb[_ledCount];
        var gain = Math.Clamp(brightness, 0.0, 1.0);

        for (var i = 0; i < _ledCount; i++)
        {
            var target = targets[i];
            var smoothed = prev is null ? target : Rgb.Lerp(prev[i], target, t);
            stored[i] = smoothed;
            output[i] = Math.Abs(gain - 1.0) < 0.001 ? smoothed : smoothed.Scale(gain);
        }

        _smoothed = stored;
        return new RgbFrame(_ledCount, output, snapshot.Timestamp);
    }

    /// <summary>Drops smoothing history (call when switching modes or LEDs).</summary>
    public void Reset() => _smoothed = null;

    private Rgb[] ComputeTargets(ScreenSnapshot s, ScreenSyncMode mode)
    {
        var targets = new Rgb[_ledCount];
        switch (mode)
        {
            case ScreenSyncMode.Average:
                Array.Fill(targets, s.Average);
                break;
            case ScreenSyncMode.BottomEdge:
                var band = Math.Max(1, s.Height / 8);
                FillColumnZones(targets, s, Math.Max(0, s.Height - band), s.Height);
                break;
            case ScreenSyncMode.Columns:
                FillColumnZones(targets, s, 0, s.Height);
                break;
            default:
                Array.Fill(targets, s.Average);
                break;
        }
        return targets;
    }

    /// <summary>Fills one LED-color per horizontal column band within [startRow, endRow).</summary>
    private void FillColumnZones(Rgb[] targets, ScreenSnapshot s, int startRow, int endRow)
    {
        if (s.Width <= 0 || s.Height <= 0 || startRow >= endRow)
        {
            Array.Fill(targets, s.Average);
            return;
        }

        var rows = endRow - startRow;
        for (var led = 0; led < _ledCount; led++)
        {
            var col0 = (long)led * s.Width / _ledCount;
            var col1 = (long)(led + 1) * s.Width / _ledCount;
            if (col1 <= col0)
                col1 = col0 + 1;

            long r = 0, g = 0, b = 0;
            var count = 0;
            for (var row = startRow; row < endRow; row++)
            {
                for (var col = (int)col0; col < col1; col++)
                {
                    var c = s.Pixels[row * s.Width + col];
                    r += c.R;
                    g += c.G;
                    b += c.B;
                    count++;
                }
            }
            if (count == 0)
            {
                targets[led] = s.Average;
                continue;
            }
            targets[led] = new Rgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
        }
    }
}