namespace SP621E.Core.Audio.Analysis;

using System.Numerics;

/// <summary>
/// Converts raw PCM blocks into structured <see cref="AudioData"/> features:
/// mono mix, RMS, Hann-windowed FFT, and bass/mid/treble band energies plus an
/// onset-strength estimate. Pure and dependency-free so the pipeline is testable
/// with synthetic audio.
///
/// Band definitions (ASSUMPTION, design): bass 20–250 Hz, mid 250–4000 Hz,
/// treble 4k–Nyquist. At 48 kHz with a 1024-point FFT the bin width is 46.9 Hz.
/// </summary>
public sealed class AudioAnalyzer
{
    public const int DefaultFftSize = 1024;

    private readonly int _fftSize;
    private readonly double[] _window;
    private readonly int _historyLength;
    private readonly double[] _energyHistory;
    private int _historyWrite;

    /// <param name="fftSize">Power of two used for the spectrum (default 1024).</param>
    /// <param name="historyLength">Frames of history used for onset-strength normalization (default 8).</param>
    public AudioAnalyzer(int fftSize = DefaultFftSize, int historyLength = 8)
    {
        if ((fftSize & (fftSize - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(fftSize), "FFT size must be a power of two.");
        _fftSize = fftSize;
        _window = MakeHannWindow(fftSize);
        _historyLength = Math.Max(1, historyLength);
        _energyHistory = new double[_historyLength];
    }

    /// <summary>
    /// Analyzes one captured block. <paramref name="samples"/> is interleaved
    /// ([ch0..chN-1][ch0..chN-1]...), normalized to [-1,1].
    /// </summary>
    public AudioData Analyze(ReadOnlySpan<float> samples, int sampleRate, int channelCount, DateTimeOffset timestamp)
    {
        var frameCount = samples.Length / Math.Max(1, channelCount);
        var data = new AudioData(timestamp);

        if (frameCount < _fftSize)
        {
            // Too small to fill one FFT block: scale RMS to block length and zero bands.
            data.Rms = ComputeRms(samples, channelCount);
            return data;
        }

        // Use the most recent _fftSize frames (sliding block) for low latency.
        var blockSamples = new float[_fftSize];
        var srcOffset = (frameCount - _fftSize) * channelCount;
        MixToMono(samples.Slice(srcOffset, _fftSize * channelCount), channelCount, blockSamples);

        data.Rms = ComputeRms(samples, channelCount);

        var spectrum = new Complex[_fftSize];
        for (var i = 0; i < _fftSize; i++)
            spectrum[i] = new Complex(blockSamples[i] * _window[i], 0.0);
        Fft.Transform(spectrum);

        var mags = new double[_fftSize / 2];
        for (var i = 0; i < mags.Length; i++)
            mags[i] = spectrum[i].Magnitude / (_fftSize / 2);

        var nyquist = sampleRate / 2.0;
        data.Bass = MeanBand(mags, sampleRate, 20.0, Math.Min(250.0, nyquist));
        data.Mid = MeanBand(mags, sampleRate, 250.0, Math.Min(4000.0, nyquist));
        data.Treble = MeanBand(mags, sampleRate, 4000.0, nyquist);

        data.OnsetStrength = ComputeOnset(mags);
        return data;
    }

    /// <summary>Per-frame RMS over the full block, channels averaged first.</summary>
    private static double ComputeRms(ReadOnlySpan<float> samples, int channelCount)
    {
        if (samples.Length == 0)
            return 0.0;

        if (channelCount == 1)
        {
            var sum = 0.0;
            foreach (var s in samples)
                sum += (double)s * s;
            return Math.Sqrt(sum / samples.Length);
        }

        // Average channels per frame, then RMS.
        var frames = samples.Length / channelCount;
        double mixedSum = 0.0;
        for (var f = 0; f < frames; f++)
        {
            double mix = 0.0;
            for (var c = 0; c < channelCount; c++)
                mix += samples[f * channelCount + c];
            mix /= channelCount;
            mixedSum += mix * mix;
        }
        return Math.Sqrt(mixedSum / frames);
    }

    private static void MixToMono(ReadOnlySpan<float> interleaved, int channelCount, Span<float> mono)
    {
        var frames = interleaved.Length / channelCount;
        for (var f = 0; f < frames; f++)
        {
            double mix = 0.0;
            for (var c = 0; c < channelCount; c++)
                mix += interleaved[f * channelCount + c];
            mono[f] = (float)(mix / channelCount);
        }
    }

    private static double MeanBand(double[] mags, int sampleRate, double lowHz, double highHz)
    {
        if (highHz <= lowHz)
            return 0.0;

        var binWidth = (double)sampleRate / (mags.Length * 2);
        var first = Math.Max(0, (int)Math.Floor(lowHz / binWidth));
        var last = Math.Min(mags.Length - 1, (int)Math.Ceiling(highHz / binWidth));

        double sum = 0.0;
        var count = 0;
        for (var i = first; i <= last; i++)
        {
            sum += mags[i];
            count++;
        }
        return count == 0 ? 0.0 : sum / count;
    }

    private double ComputeOnset(double[] mags)
    {
        if (mags.Length == 0)
            return 0.0;

        double energy = 0.0;
        foreach (var m in mags)
            energy += m * m;

        double baseline = 0.0;
        foreach (var e in _energyHistory)
            baseline += e;
        baseline /= _historyLength;

        var index = _historyWrite % _historyLength;
        _energyHistory[index] = energy;
        _historyWrite++;

        if (baseline <= 1e-9)
            return 0.0;
        return Math.Max(0.0, (energy - baseline) / baseline);
    }

    private static double[] MakeHannWindow(int size)
    {
        var window = new double[size];
        for (var i = 0; i < size; i++)
            window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (size - 1)));
        return window;
    }
}