using System.Reflection;

var lines = new List<string>();
Assembly? wasapi = null;
Assembly? core = null;
foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "NAudio*.dll"))
{
    try
    {
        var a = Assembly.LoadFrom(dll);
        if (a.GetName().Name == "NAudio.Wasapi") wasapi = a;
        if (a.GetName().Name == "NAudio.Core") core = a;
    }
    catch { }
}

void DumpExtras(string typeName)
{
    Type? t = wasapi?.GetType(typeName) ?? core?.GetType(typeName);
    lines.Add($"=== {typeName} ===" + (t is null ? "  MISSING" : "") + "   (statics/ctors/enums)");
    if (t is null) return;
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        lines.Add("  STATIC " + m);
    foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        lines.Add("  CTOR " + c);
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        lines.Add("  FIELD " + f);
}

void DumpAudioBuffer(string typeName)
{
    var t = wasapi?.GetType(typeName);
    lines.Add($"=== {typeName} ===" + (t is null ? "  MISSING" : ""));
    if (t is null) return;
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
        lines.Add("  " + m);
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        lines.Add($"  PROP {p.PropertyType} {p.Name}");
}

DumpExtras("NAudio.Wave.WasapiRecorder");
DumpExtras("NAudio.Wave.WasapiRecorderBuilder");
DumpExtras("NAudio.CoreAudioApi.MMDeviceEnumerator");
DumpAudioBuffer("NAudio.Wave.AudioBuffer");
lines.Add("=== CaptureDataAvailableHandler delegate ===");
var del = wasapi?.GetType("NAudio.Wave.CaptureDataAvailableHandler");
if (del is not null) lines.Add("  " + del);
lines.Add("=== CaptureState enum ===");
var st = wasapi?.GetType("NAudio.Wave.CaptureState");
if (st is not null) lines.Add("  " + string.Join(", ", Enum.GetNames(st)));

System.IO.File.WriteAllLines(@"C:\Users\swapn\AppData\Local\Temp\opencode\naudio-api.txt", lines);
Console.WriteLine($"dumped {lines.Count} lines");