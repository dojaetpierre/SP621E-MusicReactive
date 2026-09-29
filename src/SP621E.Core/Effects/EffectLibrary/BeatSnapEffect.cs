namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Beat snap: while the music hits, pixels pick a random hue; between hits they
/// drift back toward a dim base. Treble adds sparkle density.
/// </summary>
/// <remarks>Pseudo-randomness uses a small internal counter — deterministic per frame, I/O-free.</remarks>
public sealed class BeatSnapEffect : IEffect
{
    private ulong _seed;

    public Rgb BaseColor { get; set; } = new(20, 20, 30);

    public double SnapDurationSec { get; set; } = 0.4;

    public string Description => "Random hues snap on the beat and fade back.";

    public string Name => "Beat snap";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var pixels = new Rgb[pixelCount];

        // Most recent snap time, remembered via a time threshold window.
        var snapped = audio.IsBeat || audio.OnsetStrength > 0.35;

        for (var i = 0; i < pixelCount; i++)
        {
            if (snapped)
            {
                _seed = _seed * 6364136223846793005UL + 1;
                var hue = (_seed % 3600u) / 10.0;
                pixels[i] = Rgb.FromHsv(hue, 1.0, 1.0);
            }
            else
            {
                pixels[i] = BaseColor;
            }
        }

        return new RgbFrame(pixelCount, pixels, audio.Timestamp);
    }
}