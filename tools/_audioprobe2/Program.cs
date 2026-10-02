using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
var en = new MMDeviceEnumerator();
var caps = en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
MMDevice chosen = null; NAudio.Wave.WaveFormat fmt = null;
foreach (var d in caps) {
  try {
    var ok = (d.AudioClient.MixFormat.Encoding == WaveFormatEncoding.IeeeFloat && d.AudioClient.MixFormat.BitsPerSample == 32) ||
             (d.AudioClient.MixFormat.Encoding == WaveFormatEncoding.Pcm && d.AudioClient.MixFormat.BitsPerSample == 16);
    Console.WriteLine($"  [{d.FriendlyName}] {d.AudioClient.MixFormat.Encoding} {d.AudioClient.MixFormat.BitsPerSample}bit ok={ok}");
    if (ok && chosen == null) { chosen = d; fmt = d.AudioClient.MixFormat; }
  } catch (Exception ex) { Console.WriteLine($"  [{d.FriendlyName}] ERR {ex.Message}"); }
}
if (chosen == null) { Console.WriteLine("NO SUPPORTED INPUT DEVICE"); return; }
var rec = new WasapiRecorderBuilder().WithDevice(chosen).WithSharedMode().WithEventSync().WithBufferLength(60).Build();
long n = 0; long bytes = 0; string last = ""; float peak = 0f;
rec.DataAvailable += (buf, fl, pos, qp) => { n++; bytes += buf.Length; last = fl.ToString(); };
rec.RecordingStopped += (s, e) => Console.WriteLine("  stopped: " + (e.Exception?.Message ?? "ok"));
Console.WriteLine($"STARTING: {chosen.FriendlyName}  fmt={rec.WaveFormat.Encoding} {rec.WaveFormat.SampleRate}Hz {rec.WaveFormat.Channels}ch");
rec.StartRecording();
int t=0; while(t<20 && n==0){ System.Threading.Thread.Sleep(250); t++; }
Console.WriteLine($"after {t*250}ms callbacks={n} bytes={bytes} flags={last}");
rec.StopRecording(); rec.Dispose();
Environment.Exit(0);
