namespace SP621E.Core.Effects.EffectLibrary;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Breathe: smooth pulsing between two colors, its rate locked to the estimated BPM
/// (or a default 90 BPM until a tempo is locked).
/// </summary>
public sealed class BreatheEffect : IEffect
{
    public Rgb LowColor { get; set; } = new(10, 10, 40);

    public Rgb HighColor { get; set; } = new(0, 200, 255);

    public string Name => "Breathe";

    public string Description => "Slow color breathing synced to the BPM estimate.";

    public RgbFrame Render(RgbFrame? previous, AudioData audio, int pixelCount, TimeSpan time)
    {
        var bpm = audio.EstimatedBpm > 0 ? audio.EstimatedBpm : 90.0;
        var phase = time.TotalSeconds * (bpm / 60.0) % 1.0;
        var t = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase);
        var color = Rgb.Lerp(LowColor, HighColor, t);
        return RgbFrame.FromUniform(pixelCount, color, audio.Timestamp);
    }
}