namespace SP621E.Core.Effects;

/// <summary>
/// Contract for a music-reactive effect. Pure function: (AudioData + parameters) -&gt; RgbFrame.
/// Full signature (frame output, parameter model, per-effect state) lands at Milestone 10-11.
/// </summary>
public interface IEffect
{
    string Name { get; }
    string Description { get; }

    /// <summary>Renders the next frame of output. Purely functional; no I/O.</summary>
    /// <param name="audio">The current audio feature frame.</param>
    /// <param name="pixelCount">The controller's LED count.</param>
    /// <param name="time">Audio-clock time of this render (used for time-based animation).</param>
    /// <returns>The rendered frame.</returns>
    LedFrame.RgbFrame Render(LedFrame.RgbFrame? previous, Audio.Analysis.AudioData audio, int pixelCount, TimeSpan time);
}