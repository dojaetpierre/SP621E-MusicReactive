namespace SP621E.Core.Configuration;

/// <summary>
/// Persisted application settings. Real load/save (JSON under %LocalAppData%)
/// lands with the configuration milestone; for now the type defines the shape.
/// </summary>
public sealed class AppSettings
{
    public int LedCount { get; set; } = 50;

    public string? LastDeviceName { get; set; }

    public string? LastDeviceAddress { get; set; }

    public string? LastAudioDeviceId { get; set; }

    public bool IsInputAudioSource { get; set; }

    public string? LastEffectName { get; set; }

    public double LastBrightness { get; set; } = 1.0;

    public bool ScreenSyncEnabled { get; set; }

    public string? ScreenSyncMode { get; set; }

    public double ScreenSyncSmoothness { get; set; } = 0.5;

    public double ScreenSyncBrightness { get; set; } = 1.0;
}