namespace VirtualSp621e;

using SP621E.Core.LedFrame;

/// <summary>
/// The simulated LED strip. Decodes the VIRTUAL wire packets arriving on the
/// transport (frame 0xF1, solid color 0x21, brightness 0x22, power 0x20) into a
/// live pixel buffer and renders an ANSI truecolor line for the console.
/// </summary>
public sealed class VirtualStrip
{
    private readonly object _gate = new();
    private readonly byte[] _r;
    private readonly byte[] _g;
    private readonly byte[] _b;
    private int _brightness = 255;
    private bool _powered = true;

    public VirtualStrip(int pixelCount)
    {
        _r = new byte[pixelCount];
        _g = new byte[pixelCount];
        _b = new byte[pixelCount];
    }

    public int PixelCount => _r.Length;

    /// <summary>Processes one decoded write packet from the virtual transport.</summary>
    public void OnWrite(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length == 0)
            return;

        lock (_gate)
        {
            switch (span[0])
            {
                case 0xF1:
                    var i = 1;
                    for (var p = 0; p < _r.Length && i + 2 < span.Length; p++, i += 3)
                    {
                        _r[p] = span[i];
                        _g[p] = span[i + 1];
                        _b[p] = span[i + 2];
                    }
                    break;
                case 0x21 when span.Length >= 4:
                    for (var p = 0; p < _r.Length; p++)
                    {
                        _r[p] = span[1];
                        _g[p] = span[2];
                        _b[p] = span[3];
                    }
                    break;
                case 0x22 when span.Length >= 2:
                    _brightness = span[1];
                    break;
                case 0x20 when span.Length >= 2:
                    _powered = span[1] != 0;
                    break;
            }
        }
    }

    public string SnapshotLine()
    {
        lock (_gate)
        {
            if (!_powered)
                return new string(' ', PixelCount * 2) + "  (power off)";
            var sb = new System.Text.StringBuilder(PixelCount * 2);
            for (var p = 0; p < PixelCount; p++)
            {
                var r = (byte)(_r[p] * _brightness / 255);
                var g = (byte)(_g[p] * _brightness / 255);
                var b = (byte)(_b[p] * _brightness / 255);
                sb.Append('\x1b').Append('[').Append("48;2;").Append(r).Append(';').Append(g).Append(';').Append(b).Append('m').Append("  ");
            }
            sb.Append("\x1b[0m");
            return sb.ToString();
        }
    }

    public void Describe()
    {
        lock (_gate)
        {
            var lit = 0;
            foreach (var v in _r)
                if (v > 0)
                    lit++;
            System.Console.WriteLine($"Virtual strip: {PixelCount} px, {lit} lit, brightness {_brightness}.");
        }
    }

    public static string RgbOf(Rgb c) => c.ToString();
}