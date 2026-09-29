namespace SP621E.Audio.Capture;

/// <summary>A single render (output) audio endpoint as seen by Windows.</summary>
public sealed record AudioDeviceInfo(string Id, string FriendlyName);

/// <summary>
/// Enumerates render endpoints available for WASAPI loopback (what the machine is
/// playing), plus device lookup by id.
/// </summary>
public static class AudioDeviceEnumerator
{
    public static IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(
            NAudio.CoreAudioApi.DataFlow.Render,
            NAudio.CoreAudioApi.DeviceState.Active);
        var list = new List<AudioDeviceInfo>();
        foreach (var device in endpoints)
            list.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
        return list;
    }

    /// <summary>Finds an active render endpoint by id; null if not present.</summary>
    public static NAudio.CoreAudioApi.MMDevice? GetRenderDevice(string id)
    {
        using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        try
        {
            var device = enumerator.GetDevice(id);
            return device.DataFlow == NAudio.CoreAudioApi.DataFlow.Render ? device : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}