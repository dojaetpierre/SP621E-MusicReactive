using System;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
var en = new MMDeviceEnumerator();
var caps = en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
var dev = caps[0];
var rec = new WasapiRecorderBuilder().WithDevice(dev).WithSharedMode().WithEventSync().WithBufferLength(60).Build();
long n = 0; long bytes = 0; string last = "";
rec.DataAvailable += (buf, fl, pos, qp) => { n++; bytes += buf.Length; last = fl.ToString(); };
rec.RecordingStopped += (s, e) => Console.WriteLine("  stopped: " + (e.Exception?.Message ?? "ok"));
Console.WriteLine($"DEVICE: {dev.FriendlyName}   fmt={rec.WaveFormat.Encoding} {rec.WaveFormat.SampleRate}Hz {rec.WaveFormat.Channels}ch");
rec.StartRecording();
int t=0; while(t<20 && n==0){ System.Threading.Thread.Sleep(250); t++; }
Console.WriteLine($"after {t*250}ms callbacks={n} bytes={bytes} flags={last}");
rec.StopRecording(); rec.Dispose();
