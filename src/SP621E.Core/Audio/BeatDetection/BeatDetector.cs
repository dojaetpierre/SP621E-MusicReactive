namespace SP621E.Core.Audio.BeatDetection;

using SP621E.Core.Audio.Analysis;

/// <summary>
/// Onset-driven beat detector using a decaying adaptive threshold:
/// a beat fires when onset strength exceeds the current threshold; on a beat the
/// threshold snaps up to the onset, between beats it decays toward a noise floor.
/// This is robust across quiet and loud music. BPM is estimated from inter-beat
/// intervals, clamped to a musically useful range and smoothed.
/// </summary>
public sealed class BeatDetector
{
    private const double ThresholdFloor = 0.10;
    private const double ThresholdDecay = 0.75;
    private const double MinBpm = 60.0;
    private const double MaxBpm = 200.0;

    private double _threshold = 0.20;

    private DateTimeOffset? _lastBeatAt;
    private double? _smoothedBpm;

    /// <summary>Mutates <paramref name="data"/> setting IsBeat and EstimatedBpm; returns whether a beat fired.</summary>
    public bool Update(AudioData data)
    {
        var onset = data.OnsetStrength;
        var isBeat = onset > _threshold;

        if (isBeat)
        {
            _threshold = Math.Max(_threshold, onset);
            if (_lastBeatAt is { } last)
            {
                var afterBeatRefractory = (data.Timestamp - last).TotalSeconds > (60.0 / MaxBpm);
                if (afterBeatRefractory)
                {
                    var intervalSec = (data.Timestamp - last).TotalSeconds;
                    if (intervalSec > 0.0)
                    {
                        var bpm = 60.0 / intervalSec;
                        _smoothedBpm = _smoothedBpm is { } prev ? prev * 0.7 + bpm * 0.3 : bpm;
                    }
                }
            }
            _lastBeatAt = data.Timestamp;
        }
        else
        {
            _threshold = Math.Max(_threshold * ThresholdDecay, ThresholdFloor);
        }

        data.IsBeat = isBeat;
        data.EstimatedBpm = _smoothedBpm ?? 0.0;
        return isBeat;
    }

    public void Reset()
    {
        _threshold = 0.20;
        _lastBeatAt = null;
        _smoothedBpm = null;
    }
}