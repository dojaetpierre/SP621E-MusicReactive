namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Three-band equalizer: the strip is divided into bass / mid / treble thirds and
/// each segment lights proportionally to its band energy, from left (bass) to right
/// (treble). The classic music-LED look and the reference for Milestone-12 visuals.
/// </summary>
public sealed class SpectrumEffect : IEffect
{
    public Rgb BassColor { get; set; } = new(255, 40, 0);

    public Rgb MidColor { get; set; } = new(80, 255, 0);

    public Rgb TrebleColor { get; set; } = new(40, 120, 255);

    public double Sensitivity { get; set; } = 2.0;

    public string Name => "Spectrum bars";

    public string Description => "Left third = bass, middle = mid, right = treble; segment height follows energy.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var pixels = new Rgb[pixelCount];
        var third = Math.Max(1, pixelCount / 3);

        Fill(pixels, 0, third, BassColor, audio.Bass);
        Fill(pixels, third, 2 * third, MidColor, audio.Mid);
        Fill(pixels, 2 * third, pixelCount, TrebleColor, audio.Treble);

        return new RgbFrame(pixelCount, pixels, audio.Timestamp);
    }

    private void Fill(Rgb[] pixels, int start, int end, Rgb color, double energy)
    {
        var height = (int)Math.Clamp(energy * Sensitivity * (end - start), 0, end - start);
        // Bars grow from the bottom; silence -> a dim base line so the effect is visible.
        for (var i = start; i < end; i++)
        {
            var fromBottom = i - start;
            var lit = fromBottom < height;
            pixels[i] = lit ? color : color.Scale(0.10);
        }
    }
}