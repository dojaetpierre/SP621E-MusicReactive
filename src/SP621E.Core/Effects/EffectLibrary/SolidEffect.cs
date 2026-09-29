namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>Static solid color (audio only feeds brightness decay on silence).</summary>
public sealed class SolidEffect : IEffect
{
    public Rgb Color { get; set; } = new(255, 40, 20);

    public string Name => "Solid color";

    public string Description => "A single flat color across the strip.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
        => RgbFrame.FromUniform(pixelCount, Color, audio.Timestamp);
}