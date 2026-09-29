namespace VirtualSp621e;

/// <summary>
/// Generates a synthetic, beat-driven music signal so the whole pipeline
/// (capture block → FFT → beat/BPM → effects) runs without hardware.
///
/// Content: a kick drum on every beat (strong bass transient so the beat detector
/// fires), a short hi-hat on the offbeats, a two-note bass line, and a soft pad.
/// Output is frame-major interleaved stereo float samples in [-1, 1].
/// </summary>
public sealed class SyntheticAudioSource
{
    private readonly int _sampleRate = 48000;
    private readonly int _channelCount = 2;
    private readonly double _beatPeriod;      // seconds per beat
    private readonly Random _rng = new(20260922);
    private readonly int _blockFrames;
    private double _elapsed;                  // seconds rendered so far

    public SyntheticAudioSource(double bpm, int blockFrames = 2880)
    {
        _beatPeriod = 60.0 / bpm;
        _blockFrames = blockFrames;
    }

    public int SampleRate => _sampleRate;
    public int ChannelCount => _channelCount;
    public int BlockFrames => _blockFrames;

    /// <summary>Returns the next interleaved stereo block of normalized floats.</summary>
    public float[] NextBlock()
    {
        var mono = new float[_blockFrames];
        var dt = 1.0 / _sampleRate;

        for (var i = 0; i < _blockFrames; i++)
        {
            var t = _elapsed + i * dt;
            var beatStep = t / _beatPeriod;
            var beatIndex = Math.Floor(beatStep);
            var timeSinceBeat = (beatStep % 1.0) * _beatPeriod;
            double s = 0.0;

            // Kick drum: decaying 50 Hz thump on every beat.
            if (timeSinceBeat < 0.12)
            {
                var env = Math.Exp(-timeSinceBeat * 45.0);
                s += 0.9 * env * Math.Sin(2.0 * Math.PI * 50.0 * timeSinceBeat);
            }

            // Hi-hat: short noise burst on the offbeat.
            var timeSinceHat = ((beatStep + 0.5) % 1.0) * _beatPeriod;
            if (timeSinceHat < 0.03)
                s += 0.25 * Math.Exp(-timeSinceHat * 180.0) * (_rng.NextDouble() * 2.0 - 1.0);

            // Two-note bass line (alternates every two beats) + soft chord pad.
            var noteFreq = (Math.Floor(beatIndex / 2.0) % 2.0 == 0) ? 98.0 : 123.5;
            s += 0.18 * Math.Sin(2.0 * Math.PI * noteFreq * t);
            s += 0.06 * Math.Sin(2.0 * Math.PI * 261.63 * t);
            s += 0.05 * Math.Sin(2.0 * Math.PI * 329.63 * t);
            s += 0.04 * Math.Sin(2.0 * Math.PI * 392.00 * t);

            mono[i] = (float)Math.Clamp(s, -1.0, 1.0);
        }

        _elapsed += _blockFrames * dt;

        var interleaved = new float[_blockFrames * _channelCount];
        for (var i = 0; i < _blockFrames; i++)
        {
            interleaved[i * 2] = mono[i];
            interleaved[i * 2 + 1] = mono[i];
        }
        return interleaved;
    }
}