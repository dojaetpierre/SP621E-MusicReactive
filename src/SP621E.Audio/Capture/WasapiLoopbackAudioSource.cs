namespace SP621E.Audio.Capture;

using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SP621E.Core.Audio.Capture;

/// <summary>
/// Captures the system mix playing through a render endpoint (WASAPI loopback) and
/// raises <see cref="AudioSamplesAvailableEventArgs"/> with interleaved float samples
/// normalized to [-1, 1], one sample per channel and frame-major ordering
/// ([f0c0, f0c1, f1c0, f1c1, ...]). Backed by NAudio's modern <see cref="WasapiRecorder"/>.
/// </summary>
public sealed class WasapiLoopbackAudioSource : IAudioSource, IDisposable
{
    private readonly string _deviceId;
    private readonly string _deviceName;
    private NAudio.CoreAudioApi.MMDevice? _device;
    private WasapiRecorder? _recorder;
    private AudioConversion? _conversion;
    private int _sampleRate;
    private int _channelCount;
    private volatile bool _disposed;

    public WasapiLoopbackAudioSource(string deviceId, string deviceName)
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

        _device = AudioDeviceEnumerator.GetRenderDevice(_deviceId)
                  ?? throw new InvalidOperationException($"Render endpoint '{_deviceName}' is no longer available.");

        var recorder = new WasapiRecorderBuilder()
            .WithDevice(_device)
            .WithLoopbackCapture()
            .WithSharedMode()
            .WithEventSync()
            .WithBufferLength(60)
            .Build();

        var conversion = AudioConversion.For(recorder.WaveFormat)
                         ?? throw new InvalidOperationException(
                             $"Unsupported loopback format: {recorder.WaveFormat.Encoding} {recorder.WaveFormat.BitsPerSample} bit.");

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

        var samples = conversion.ToInterleavedFloats(buffer);
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
                $"WASAPI capture stopped unexpectedly: {e.Exception.Message}", e.Exception));
            return;
        }
        Failed?.Invoke(this, new AudioFailureEventArgs("WASAPI capture stopped."));
    }

    private sealed class AudioConversion
    {
        private readonly WaveFormatEncoding _encoding;
        private readonly int _bytesPerSample;

        private AudioConversion(WaveFormatEncoding encoding, int bitsPerSample)
        {
            _encoding = encoding;
            _bytesPerSample = bitsPerSample / 8;
        }

        /// <summary>Creates a converter for 16-bit PCM or 32-bit IEEE float; null otherwise.</summary>
        public static AudioConversion? For(WaveFormat format)
        {
            var bytes = format.BitsPerSample / 8;
            return bytes switch
            {
                2 => new AudioConversion(format.Encoding, format.BitsPerSample),
                4 when format.Encoding == WaveFormatEncoding.IeeeFloat => new AudioConversion(format.Encoding, format.BitsPerSample),
                _ => null,
            };
        }

        public float[] ToInterleavedFloats(ReadOnlySpan<byte> buffer)
        {
            var sampleCount = buffer.Length / _bytesPerSample;
            var result = new float[sampleCount];

            if (_encoding == WaveFormatEncoding.IeeeFloat && _bytesPerSample == 4)
            {
                for (var i = 0; i < sampleCount; i++)
                    result[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(i * 4, 4)));
            }
            else
            {
                for (var i = 0; i < sampleCount; i++)
                {
                    var v = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(i * 2, 2));
                    result[i] = v / 32768f;
                }
            }
            return result;
        }
    }
}