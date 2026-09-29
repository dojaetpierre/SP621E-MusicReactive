namespace SP621E.Screen;

/// <summary>
/// Convenience wrapper around <see cref="NativeScreen"/>: returns a downscaled
/// snapshot or null (with <see cref="LastError"/>) if the display cannot be read.
/// Safe to call from any thread.
/// </summary>
public sealed class ScreenSampler
{
    public const int TargetWidth = 96;

    public string? LastError { get; private set; }

    public ScreenSnapshot? Capture()
    {
        try
        {
            var snapshot = NativeScreen.Capture(TargetWidth, DateTimeOffset.Now);
            LastError = snapshot is null ? "Display capture returned no data." : null;
            return snapshot;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }
}