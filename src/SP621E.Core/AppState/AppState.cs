namespace SP621E.Core.AppState;

public enum ConnectionStatus
{
    Disconnected,
    Scanning,
    Connecting,
    Connected,
    Failed,
}

public enum PipelineStatus
{
    Stopped,
    Running,
}

/// <summary>
/// Mutable session state shared between the pipeline and the UI.
/// Thread confinement: mutated by the pipeline, read by the UI via the dispatcher.
/// </summary>
public sealed class AppState
{
    public ConnectionStatus Connection { get; set; } = ConnectionStatus.Disconnected;
    public PipelineStatus Pipeline { get; set; } = PipelineStatus.Stopped;
    public string? ConnectedDeviceName { get; set; }
    public string? ActiveEffectName { get; set; }
    public double GlobalBrightness { get; set; } = 1.0;
    public string? LastError { get; set; }
}