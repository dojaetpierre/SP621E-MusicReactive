namespace SP621E.Core.Audio.Capture;

/// <summary>
/// Carries one block of captured PCM samples. <see cref="Samples"/> is frame-major
/// interleaved ([f0c0, f0c1, f1c0, f1c1, ...], one sample per channel per frame).
/// Values are normalized to [-1, 1]. Shape finalized at Milestone 7 alongside the
/// loopback implementation.
/// </summary>
public sealed class AudioSamplesAvailableEventArgs(float[] samples, int sampleRate, int channelCount, DateTimeOffset capturedAt)
    : EventArgs
{
    public float[] Samples { get; } = samples;
    public int SampleRate { get; } = sampleRate;
    public int ChannelCount { get; } = channelCount;
    public DateTimeOffset CapturedAt { get; } = capturedAt;

    public int FrameCount => Samples.Length / Math.Max(1, ChannelCount);
}