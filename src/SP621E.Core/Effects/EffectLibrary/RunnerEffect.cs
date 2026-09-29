namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// A bright comet loops around the strip with a fading trail; the mid band speeds
/// it up and beat hits make the head white-hot.
/// </summary>
public sealed class RunnerEffect : IEffect
{
    public Rgb HeadColor { get; set; } = Rgb.FromHsv(200, 1.0, 1.0);

    public int TrailLength { get; set; } = 8;

    public string Name => "Runner";

    public string Description => "A comet races around the strip, sped up by mid-frequency energy.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var speed = 1.0 + audio.Mid * 3.0;
        var position = time.TotalSeconds * speed * 4.0 % pixelCount;

        var pixels = new Rgb[pixelCount];
        var head = (int)position;
        for (var i = 0; i < pixelCount; i++)
        {
            var distance = (i - head + pixelCount) % pixelCount;
            if (distance == 0)
            {
                pixels[i] = audio.IsBeat ? Rgb.FromHsv(0, 0, 1.0) : HeadColor;
            }
            else if (distance < TrailLength)
            {
                var t = 1.0 - (double)distance / TrailLength;
                pixels[i] = HeadColor.Scale(0.6 * t);
            }
            else
            {
                pixels[i] = new Rgb(0, 0, 0);
            }
        }

        return new RgbFrame(pixelCount, pixels, audio.Timestamp);
    }
}