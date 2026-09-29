namespace SP621E.Core.Effects;

using SP621E.Core.Audio.Analysis;
using SP621E.Core.LedFrame;

/// <summary>
/// Host loop core: holds the active effect and the user-level gain/time knobs and
/// turns each audio feature frame into an <see cref="RgbFrame"/>, applying global
/// intensity and effective speed. Pure: no I/O; the caller decides where frames go
/// (preview, mock controller, or the SP621E driver).
/// </summary>
public sealed class EffectsEngine
{
    public EffectsEngine(int pixelCount)
    {
        if (pixelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelCount));
        PixelCount = pixelCount;
    }

    public int PixelCount { get; }

    public EffectRegistry Registry { get; } = EffectRegistry.CreateDefault();

    /// <summary>Active effect; null means "pixels off".</summary>
    public IEffect? ActiveEffect
    {
        get => Registry.ActiveEffect;
        set => Registry.ActiveEffect = value;
    }

    public string? ActiveEffectName => ActiveEffect?.Name;

    /// <summary>Global output multiplier applied after the effect (0..&gt;1).</summary>
    public double Intensity { get; set; } = 1.0;

    /// <summary>Multiplier applied to the effect clock (1.0 = normal speed).</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>Master brightness (0..1), stacked on top of Intensity.</summary>
    public double GlobalBrightness { get; set; } = 1.0;

    /// <summary>
    /// Renders one frame. <paramref name="time"/> is the effect-clock elapsed time
    /// (pre-multiplied by <see cref="Speed"/>). Returns an all-off frame when no
    /// effect is selected.
    /// </summary>
    public RgbFrame Render(AudioData audio, TimeSpan time)
    {
        var effect = ActiveEffect;
        if (effect is null)
            return RgbFrame.FromUniform(PixelCount, new Rgb(0, 0, 0), audio.Timestamp);

        var frame = effect.Render(null, audio, PixelCount, time);
        var gain = Intensity * GlobalBrightness;
        if (Math.Abs(gain - 1.0) > 0.001)
            frame = frame.Map((pixel, _) => pixel.Scale(Math.Clamp(gain, 0.0, 1.0)));
        return frame;
    }
}