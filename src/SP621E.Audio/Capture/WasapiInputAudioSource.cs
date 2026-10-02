namespace SP621E.Audio.Capture;

using NAudio.CoreAudioApi;
using NAudio.Wave;
using SP621E.Core.Audio.Capture;

/// <summary>
/// Captures audio from a WASAPI input endpoint (microphone, line-in, rear-TOSLINK/HDMI
/// audio, a USB HDMI capture card, ...) and raises <see cref="AudioSamplesAvailableEventArgs"/>
/// with interleaved float samples normalized to [-1, 1], one sample per channel, frame-major
/// ordering ([f0c0, f0c1, f1c0, f1c1, ...]). This is what makes the app react to a TV/console
/// that produces its own audio instead of this machine playing it.
/// </summary>
public sealed class WasapiInputAudioSource : IAudioSource, IDisposable
{
    private readonly string _deviceId;
    private readonly string _deviceName;
    private NAudio.CoreAudioApi.MMDevice? _device;
    private WasapiRecorder? _recorder;
    private Func<ReadOnlySpan<byte>, float[]>? _conversion;
    private int _sampleRate;
    private int _channelCount;
    private volatile bool _disposed;

    public WasapiInputAudioSource(string deviceId, string deviceName)
    {
        _deviceId = deviceId;
        _deviceName = deviceName;
    }

    public int SampleRate => _sampleRate;
    public int ChannelCount => _channelCount;
    public string CurrentDeviceName => _deviceName;

    public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;
    public event EventHandler<AudioFailureEventArgs>? Failed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_recorder is not null)
            return;

        _device = AudioDeviceEnumerator.GetInputDevice(_deviceId)
                  ?? throw new InvalidOperationException($"Input endpoint '{_deviceName}' is no longer available.");

        var recorder = new WasapiRecorderBuilder()
            .WithDevice(_device)
            .WithSharedMode()
            .WithEventSync()
            .WithBufferLength(60)
            .Build();

        var conversion = WasapiSampleConverter.CreateFor(recorder.WaveFormat)
                         ?? throw new InvalidOperationException(
                             $"Unsupported input format: {recorder.WaveFormat.Encoding} {recorder.WaveFormat.BitsPerSample} bit.");

        _sampleRate = recorder.WaveFormat.SampleRate;
        _channelCount = recorder.WaveFormat.Channels;
        _conversion = conversion;

        recorder.DataAvailable += OnDataAvailable;
        recorder.RecordingStopped += OnRecordingStopped;

        _recorder = recorder;
        recorder.StartRecording();
    }

    public void Stop()
    {
        var recorder = _recorder;
        if (recorder is null)
            return;
        try
        {
            recorder.StopRecording();
        }
        catch (Exception)
        {
            // The endpoint may already be gone; teardown below still runs.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        var recorder = _recorder;
        _recorder = null;
        if (recorder is not null)
        {
            recorder.DataAvailable -= OnDataAvailable;
            recorder.RecordingStopped -= OnRecordingStopped;
            recorder.Dispose();
        }
        _device?.Dispose();
        _device = null;
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var conversion = _conversion;
        var sampleRate = _sampleRate;
        var channelCount = _channelCount;
        if (conversion is null || (flags & AudioClientBufferFlags.Silent) != 0 || buffer.IsEmpty)
            return;

        var samples = conversion(buffer);
        if (samples.Length == 0)
            return;

        SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(
            samples, sampleRate, channelCount, DateTimeOffset.UtcNow));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Failed?.Invoke(this, new AudioFailureEventArgs(
                $"WASAPI input capture stopped unexpectedly: {e.Exception.Message}", e.Exception));
            return;
        }
        Failed?.Invoke(this, new AudioFailureEventArgs("WASAPI input capture stopped."));
    }
}