namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>Full-strip flash on beat/bass hit, off otherwise.</summary>
public sealed class StrobeEffect : IEffect
{
    public Rgb Color { get; set; } = Rgb.FromHsv(0, 0, 1.0);

    public double Threshold { get; set; } = 0.08;

    public string Name => "Strobe";

    public string Description => "Full-strip flashes on each detected beat.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var flash = audio.IsBeat || audio.OnsetStrength > Threshold;
        var color = flash ? Color : new Rgb(0, 0, 0);
        return RgbFrame.FromUniform(pixelCount, color, audio.Timestamp);
    }
}