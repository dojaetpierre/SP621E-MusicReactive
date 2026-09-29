namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Bass pulse: full-strip brightness driven by the bass band with an exponential
/// decay, so each kick gives a visible "thump". Holds its own running level;
/// effects like this are single-pipeline, thread-confined, I/O-free by contract.
/// </summary>
public sealed class BassPulseEffect : IEffect
{
    private double _level;

    public Rgb Color { get; set; } = new(255, 80, 0);

    public double Attack { get; set; } = 1.0;

    public double Decay { get; set; } = 8.0;

    public string Name => "Bass pulse";

    public string Description => "Strip flashes with the bass drum and decays between kicks.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        // Decay toward silence with a reference 30 fps frame rate.
        _level *= Math.Exp(-Decay / 30.0);
        if (audio.Bass > _level)
            _level = audio.Bass;

        var scaled = Math.Clamp(_level * Attack, 0.0, 1.0);
        return RgbFrame.FromUniform(pixelCount, Color.Scale(scaled), audio.Timestamp);
    }
}