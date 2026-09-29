namespace SP621E.Core.Audio.Analysis;

/// <summary>
/// Structured, timestamped feature frame produced from raw audio.
/// Full field set (band energies, RMS, onset strength) lands at Milestone 8.
/// </summary>
public sealed class AudioData(DateTimeOffset timestamp)
{
    public DateTimeOffset Timestamp { get; } = timestamp;
    public double Rms { get; set; }
    public double Bass { get; set; }
    public double Mid { get; set; }
    public double Treble { get; set; }
    public double OnsetStrength { get; set; }
    public bool IsBeat { get; set; }
    public double EstimatedBpm { get; set; }
}