namespace SP621E.Core.Audio.Capture;

/// <summary>
/// Abstraction over any real-time audio source. The WASAPI loopback implementation
/// lives in SP621E.Audio (Windows-only) and is wired up at Milestone 7.
/// The analysis/effects layers depend only on this interface.
/// </summary>
public interface IAudioSource
{
    int SampleRate { get; }
    int ChannelCount { get; }
    string CurrentDeviceName { get; }

    event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;
    event EventHandler<AudioFailureEventArgs>? Failed;

    void Start();
    void Stop();
}