namespace SP621E.Audio.Capture;

using NAudio = NAudio.CoreAudioApi;

/// <summary>A single audio endpoint (output/render or input/capture) as seen by Windows.</summary>
public sealed record AudioDeviceInfo(string Id, string FriendlyName, bool IsInput);

/// <summary>
/// Enumerates render endpoints (for WASAPI loopback of what this machine plays) and capture
/// endpoints (for WASAPI input capture of a microphone, line-in, or TV/console audio), plus
/// device lookup by id.
/// </summary>
public static class AudioDeviceEnumerator
{
    public static IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        return Enumerate(NAudio.DataFlow.Render, isInput: false);
    }

    public static IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        return Enumerate(NAudio.DataFlow.Capture, isInput: true);
    }

    /// <summary>Finds an active render endpoint by id; null if not present.</summary>
    public static NAudio.MMDevice? GetRenderDevice(string id)
    {
        return GetDevice(id, NAudio.DataFlow.Render);
    }

    /// <summary>Finds an active capture endpoint by id; null if not present.</summary>
    public static NAudio.MMDevice? GetInputDevice(string id)
    {
        return GetDevice(id, NAudio.DataFlow.Capture);
    }

    private static IReadOnlyList<AudioDeviceInfo> Enumerate(NAudio.DataFlow flow, bool isInput)
    {
        using var enumerator = new NAudio.MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(flow, NAudio.DeviceState.Active);
        var list = new List<AudioDeviceInfo>(endpoints.Count);
        foreach (var device in endpoints)
            list.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, isInput));
        return list;
    }

    private static NAudio.MMDevice? GetDevice(string id, NAudio.DataFlow flow)
    {
        using var enumerator = new NAudio.MMDeviceEnumerator();
        try
        {
            var device = enumerator.GetDevice(id);
            return device.DataFlow == flow ? device : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}