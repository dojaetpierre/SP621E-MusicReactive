using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

var en = new MMDeviceEnumerator();
var caps = en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
MMDevice chosen = null;
foreach (var d in caps)
{
    try
    {
        var mf = d.AudioClient.MixFormat;
        var ok = mf.BitsPerSample == 16 || mf.BitsPerSample == 32;
        Console.WriteLine($"  [{d.FriendlyName}] {mf.Encoding} {mf.BitsPerSample}bit ok={ok}");
        if (ok && chosen == null) chosen = d;
    }
    catch (Exception ex) { Console.WriteLine($"  [{d.FriendlyName}] ERR {ex.Message}"); }
}

if (chosen == null) { Console.WriteLine("NO SUPPORTED INPUT DEVICE"); return; }

var rec = new WasapiRecorderBuilder().WithDevice(chosen).WithSharedMode().WithEventSync().WithBufferLength(60).Build();
long n = 0; long bytes = 0; string last = "";
rec.DataAvailable += (buf, fl, pos, qp) => { n++; bytes += buf.Length; last = fl.ToString(); };
rec.RecordingStopped += (s, e) => Console.WriteLine("  stopped: " + (e.Exception?.Message ?? "ok"));
Console.WriteLine($"STARTING: {chosen.FriendlyName}  fmt={rec.WaveFormat.Encoding} {rec.WaveFormat.SampleRate}Hz {rec.WaveFormat.Channels}ch");
rec.StartRecording();
int t = 0; while (t < 20 && n == 0) { System.Threading.Thread.Sleep(250); t++; }
Console.WriteLine($"after {t * 250}ms callbacks={n} bytes={bytes} flags={last}");
rec.StopRecording(); rec.Dispose();
Environment.Exit(0);