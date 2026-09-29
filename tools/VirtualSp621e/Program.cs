using System.Diagnostics;
using SP621E.Bluetooth;
using SP621E.Bluetooth.Snoop;
using SP621E.Core.Audio.Analysis;
using SP621E.Core.Audio.BeatDetection;
using SP621E.Core.Effects;
using SP621E.Core.LedFrame;
using VirtualSp621e;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
    {
        PrintHelp();
        return args.Length == 0 ? 1 : 0;
    }

    var pixelCount = GetInt(args, "--pixels", 50);
    var seconds = GetDouble(args, "--seconds", 10.0);
    var bpm = GetDouble(args, "--bpm", 120.0);
    var mtu = GetInt(args, "--mtu", 20);
    var delayMs = GetDouble(args, "--delay", 5.0);
    var effectName = GetString(args, "--effect", "Spectrum bars");
    var capturePath = GetStringOption(args, "--gen-capture");

    if (capturePath is not null)
    {
        SampleCaptureWriter.Write(capturePath);
        Console.WriteLine($"Wrote synthetic capture: {Path.GetFullPath(capturePath)}");
        Console.WriteLine("Verifying it round-trips through the shared parser...");
        var packets = new HciSnoopReader().Read(capturePath);
        Console.WriteLine($"Parsed {packets.Count} ATT packets — import path OK (SnoopDecode / app Advanced tab).");
        var writes = packets.Count(p => p.Opcode is 0x12 or 0x52 or 0x14);
        var notifs = packets.Count(p => p.Opcode is 0x18 or 0x19);
        Console.WriteLine($"  writes={writes} notifications={notifs}");
        Console.WriteLine("Note: SAMPLE traffic only — not SP621E evidence.");
        return 0;
    }

    return await RunPipelineAsync(pixelCount, seconds, bpm, mtu, delayMs, effectName).ConfigureAwait(false);
}

static async Task<int> RunPipelineAsync(int pixelCount, double seconds, double bpm, int mtu, double delayMs, string effectName)
{
    Console.WriteLine();
    Console.WriteLine("========================================================================");
    Console.WriteLine("  SP621E VIRTUAL TESTER — software-only drill of the full pipeline");
    Console.WriteLine("========================================================================");
    Console.WriteLine("  WIRE FORMAT ..... VIRTUAL (bytes invented for this tool)");
    Console.WriteLine("  TRANSPORT ....... VirtualBleTransport (in-memory, no radio)");
    Console.WriteLine("  EVIDENCE ........ NONE - a real SP621E wire format comes only from");
    Console.WriteLine("                    HARDWARE_PROTOCOL.md section 5 (BanlanX HCI capture).");
    Console.WriteLine("========================================================================");
    Console.WriteLine();

    var wire = new VirtualSp621eWireFormat(pixelCount);
    var strip = new VirtualStrip(pixelCount);
    var transport = new VirtualBleTransport(mtu) { Strip = strip };
    var driver = new SP621EDriver(
        pixelCount,
        transport,
        wire,
        new DriverOptions { InterPacketDelay = TimeSpan.FromMilliseconds(delayMs) });

    driver.DeviceDataReceived += (_, data) =>
        Console.WriteLine($"  [notify] device->host {data.Length} bytes: {Convert.ToHexString(data.Span)}");

    await driver.ConnectAsync().ConfigureAwait(false);
    transport.RaiseData([0x80, 0x01, 0x02]); // virtual "device online" notification
    Console.WriteLine($"Connected. Capabilities = {driver.Capabilities} | PixelCount = {driver.PixelCount} | MTU payload = {mtu} B");

    await driver.SetPowerAsync(true).ConfigureAwait(false);
    await driver.SetColorAsync(new Rgb(255, 40, 0)).ConfigureAwait(false);
    await driver.SetBrightnessAsync(0.9).ConfigureAwait(false);
    await driver.SetEffectAsync(effectName).ConfigureAwait(false);

    var engine = new EffectsEngine(pixelCount);
    engine.ActiveEffect = ResolveEffect(engine.Registry, effectName) ?? engine.Registry.Effects[0];
    engine.Intensity = 1.0;
    engine.Speed = 1.2;
    engine.GlobalBrightness = 1.0;
    Console.WriteLine($"Effect active: {engine.ActiveEffect?.Name} ({engine.ActiveEffect?.GetType().Name})");
    Console.WriteLine();

    var analyzer = new AudioAnalyzer();
    var beat = new BeatDetector();
    var synth = new SyntheticAudioSource(bpm);
    var clock = Stopwatch.StartNew();
    var sw = Stopwatch.StartNew();
    long frames = 0;
    var lastSnapshot = -0.5;

    while (sw.Elapsed.TotalSeconds < seconds)
    {
        var audio = analyzer.Analyze(synth.NextBlock(), synth.SampleRate, synth.ChannelCount, DateTimeOffset.UtcNow);
        beat.Update(audio);
        var frame = engine.Render(audio, clock.Elapsed * engine.Speed);
        await driver.SendFrameAsync(frame).ConfigureAwait(false);
        frames++;

        if (sw.Elapsed.TotalSeconds - lastSnapshot >= 0.5)
        {
            Console.WriteLine(
                $"[{sw.Elapsed.TotalSeconds,6:0.00}s] beat={(audio.IsBeat ? "*" : "-")} " +
                $"bpm={audio.EstimatedBpm,4:0} bass={audio.Bass:F3} mid={audio.Mid:F3} treble={audio.Treble:F3}");
            Console.WriteLine($"   {strip.SnapshotLine().TrimEnd()}");
            Console.WriteLine($"   frames={frames,4} packets={transport.WriteCount,5} bytes={transport.BytesWritten,7} " +
                              $"rate={(frames / Math.Max(sw.Elapsed.TotalSeconds, 0.001)),5:0.0} fps");
            lastSnapshot = sw.Elapsed.TotalSeconds;
        }
    }

    var elapsed = sw.Elapsed.TotalSeconds;
    await driver.SetPowerAsync(false).ConfigureAwait(false);
    await driver.DisconnectAsync().ConfigureAwait(false);

    Console.WriteLine();
    Console.WriteLine("========================================================================");
    Console.WriteLine("  SUMMARY — virtual pipeline run");
    Console.WriteLine("========================================================================");
    Console.WriteLine($"  Frames streamed .......... {frames}  ({(frames / elapsed):0.0} fps avg over {elapsed:0.00}s)");
    Console.WriteLine($"  Write packets ............ {transport.WriteCount}  (payload MTU {mtu}B, pacing {delayMs:0.#} ms)");
    Console.WriteLine($"  Bytes written ............ {transport.BytesWritten}");
    Console.WriteLine($"  Chunking exercised ....... {(transport.BytesWritten > 0 ? "yes" : "no")}");
    Console.WriteLine($"  Parametric commands ...... power/color/brightness/effect sent OK");
    Console.WriteLine($"  BLE notifications handled  1 (virtual online packet)");
    Console.Write("  ");
    strip.Describe();
    Console.WriteLine("========================================================================");
    Console.WriteLine("  VERDICT: software pipeline (B2, B3, B4, B5) runs end-to-end.");
    Console.WriteLine("  Hardware (B1 GATT map, real wire format) still needs section-5 evidence.");
    Console.WriteLine("========================================================================");
    return 0;
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        VirtualSp621e — software-only SP621E pipeline drill (no BLE hardware).

        Usage:
          VirtualSp621e [run] [options]          full virtual pipeline (default)
          VirtualSp621e --gen-capture <path>     write a synthetic btsnoop capture, verify it
                                                 round-trips through the shared parser, exit
          VirtualSp621e --help

        Options:
          --pixels N        LED count to simulate            (default 50)
          --seconds S       run duration in seconds          (default 10)
          --bpm N           synthetic track tempo            (default 120)
          --mtu N           ATT payload bytes per write      (default 20; low values exercise chunking)
          --delay MS        inter-packet pacing in ms        (default 5)
          --effect NAME     effect for the show              (default "Spectrum bars")
          --gen-capture P   also write sample capture (run mode) OR generate/verify/exit (alone)

        All output is VIRTUAL — bytes are simulated, never SP621E evidence.
        """);

    Console.WriteLine("        Effect names: '" + string.Join("', '", new[]
    {
        "Solid color", "Bass pulse", "Strobe", "Spectrum bars",
        "Rainbow wave", "Breathe", "Beat snap", "Runner",
    }) + "' (or any type name, e.g. SpectrumEffect)");
}

static int GetInt(string[] args, string name, int def)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : def;
}

static double GetDouble(string[] args, string name, double def)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], out var v) ? v : def;
}

static string GetString(string[] args, string name, string def)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
}

static string? GetStringOption(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static IEffect? ResolveEffect(EffectRegistry registry, string name) =>
    registry.Effects.FirstOrDefault(f =>
        f.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        f.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase));