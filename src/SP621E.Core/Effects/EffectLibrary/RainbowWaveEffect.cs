namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Rainbow wave: hue cycles along the strip position and rotates with time; audio
/// energy brightens the whole thing, so louder music makes it more vivid.
/// </summary>
public sealed class RainbowWaveEffect : IEffect
{
    public double BaseSpeed { get; set; } = 0.5;

    public double Saturation { get; set; } = 0.9;

    public string Name => "Rainbow wave";

    public string Description => "A flowing rainbow whose brightness tracks the music level.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var pixels = new Rgb[pixelCount];
        var brightness = 0.35 + 0.65 * Math.Clamp(audio.Rms * 2.0, 0.0, 1.0);
        var energy = Math.Clamp(audio.Rms * 3.0, 0.3, 1.0);

        for (var i = 0; i < pixelCount; i++)
        {
            var hue = (i * 360.0 / pixelCount) + (time.TotalSeconds * 360.0 * BaseSpeed);
            pixels[i] = Rgb.FromHsv(hue, Saturation, energy * brightness);
        }

        return new RgbFrame(pixelCount, pixels, audio.Timestamp);
    }
}