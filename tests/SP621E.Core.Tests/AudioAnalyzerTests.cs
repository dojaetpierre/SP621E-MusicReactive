using SP621E.Core.Audio.Analysis;
using SP621E.Core.Audio.BeatDetection;
using SP621E.Core.Effects;
using SP621E.Core.LedFrame;

namespace SP621E.Core.Tests;

public class AudioAnalyzerTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void Silence_ProducesNearZeroFeatures()
    {
        var analyzer = new AudioAnalyzer();
        var samples = new float[2048];
        var data = analyzer.Analyze(samples, SampleRate, 1, DateTimeOffset.UtcNow);

        Assert.InRange(data.Rms, 0.0, 0.001);
        Assert.InRange(data.Bass, 0.0, 0.001);
        Assert.InRange(data.Mid, 0.0, 0.001);
        Assert.InRange(data.Treble, 0.0, 0.001);
    }

    [Fact]
    public void PureSine_LandsInExpectedBand()
    {
        // 100 Hz tone -> dominant in the bass band (< 250 Hz), near-zero treble.
        var analyzer = new AudioAnalyzer();
        var samples = new float[2048];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * 100.0 * i / SampleRate));

        var data = analyzer.Analyze(samples, SampleRate, 1, DateTimeOffset.UtcNow);

        Assert.True(data.Bass > data.Mid * 3, $"Bass {data.Bass} should dominate mid {data.Mid}");
        Assert.True(data.Treble < data.Bass, $"Treble {data.Treble} should be well below bass {data.Bass}");
        Assert.InRange(data.Rms, 0.3, 0.4); // 0.5 * 1/sqrt(2) ≈ 0.354 for a 0.5-amplitude sine
    }

    [Fact]
    public void OnsetSpike_RaisesOnsetStrengthAboveBaseline()
    {
        var analyzer = new AudioAnalyzer();

        // 12 quiet blocks to prime the history.
        var quiet = new float[1024];
        for (var i = 0; i < 12; i++)
            analyzer.Analyze(quiet, SampleRate, 1, DateTimeOffset.UtcNow);

        // One loud block.
        var loud = new float[1024];
        for (var i = 0; i < loud.Length; i++)
            loud[i] = 0.9f;

        // The first loud block primes the history; a later one must spike.
        analyzer.Analyze(loud, SampleRate, 1, DateTimeOffset.UtcNow);
        var data2 = analyzer.Analyze(loud, SampleRate, 1, DateTimeOffset.UtcNow);
        Assert.True(data2.OnsetStrength > 1.0, $"Onset strength {data2.OnsetStrength} should spike after loud block");
    }
}

public class BeatDetectorTests
{
    [Fact]
    public void SteadyBeats_DriveBpmIntoMusicalRange()
    {
        var detector = new BeatDetector();
        var t0 = DateTimeOffset.UtcNow;

        // Music-like onset: mild body (0.35) every 100 ms, a strong 3.0 spike every
        // 500 ms (120 BPM). The decaying-threshold detector must lock in ~120 BPM.
        AudioData? last = null;
        for (var frame = 0; frame < 200; frame++)
        {
            var isStrong = frame % 5 == 0;
            var data = new AudioData(t0 + TimeSpan.FromMilliseconds(frame * 100))
            {
                OnsetStrength = isStrong ? 3.0 : 0.35,
            };
            detector.Update(data);
            last = data;
        }

        Assert.InRange(last!.EstimatedBpm, 108, 132); // 120 ± 10%
    }

    [Fact]
    public void Silence_NeverFiresBeat()
    {
        var detector = new BeatDetector();
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 30; i++)
        {
            var data = new AudioData(t0 + TimeSpan.FromMilliseconds(i * 50));
            detector.Update(data);
            Assert.False(data.IsBeat);
        }
    }
}

public class EffectsEngineTests
{
    [Fact]
    public void NoEffect_RendersAllOff()
    {
        var engine = new EffectsEngine(8) { ActiveEffect = null };
        var frame = engine.Render(new AudioData(DateTimeOffset.UtcNow), TimeSpan.Zero);
        Assert.All(frame.Pixels, p => Assert.Equal(new Rgb(0, 0, 0), p));
    }

    [Fact]
    public void SpectrumEffect_FillsFrameWithoutOverflow()
    {
        var engine = new EffectsEngine(48);
        engine.ActiveEffect = new SP621E.Core.Effects.EffectLibrary.SpectrumEffect();
        var audio = new AudioData(DateTimeOffset.UtcNow) { Bass = 0.6, Mid = 0.4, Treble = 0.2 };
        var frame = engine.Render(audio, TimeSpan.FromSeconds(1));

        Assert.Equal(48, frame.PixelCount);
        Assert.All(frame.Pixels, p => Assert.True(p.R >= 0 && p.R <= 255 && p.G >= 0 && p.G <= 255 && p.B >= 0 && p.B <= 255));

        // The bass segment (high energy) must contain brighter pixels than the silent sections.
        var brightest = frame.Pixels.Max(p => p.R + p.G + p.B);
        var dimmest = frame.Pixels.Min(p => p.R + p.G + p.B);
        Assert.True(brightest > dimmest, $"spectrum bars should vary, got {dimmest}..{brightest}");
    }

    [Fact]
    public void Intensity_Brightness_StackOnTopOfEffect()
    {
        var engine = new EffectsEngine(10);
        engine.ActiveEffect = new SP621E.Core.Effects.EffectLibrary.SolidEffect { Color = new Rgb(200, 100, 50) };
        engine.GlobalBrightness = 0.5;

        var frame = engine.Render(new AudioData(DateTimeOffset.UtcNow), TimeSpan.Zero);
        Assert.All(frame.Pixels, p => Assert.Equal(new Rgb(100, 50, 25), p));
    }
}