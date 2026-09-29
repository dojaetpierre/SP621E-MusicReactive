namespace SP621E.Core.Controllers;

using SP621E.Core.LedFrame;

/// <summary>
/// Contract between the effects engine / app and any LED controller.
/// The engine never knows or cares which controller it talks to.
/// </summary>
public interface ILedController
{
    string DisplayName { get; }

    bool IsConnected { get; }

    int PixelCount { get; }

    /// <summary>Which control surfaces this controller supports (streaming, parametric, or both).</summary>
    ControllerCapability Capabilities { get; }

    /// <summary>
    /// Transport a rendered frame to the controller.
    /// Throws <see cref="NotSupportedException"/> when the controller lacks
    /// <see cref="ControllerCapability.FrameStreaming"/> (never silently drops).
    /// </summary>
    Task SendFrameAsync(RgbFrame frame, CancellationToken ct = default);

    /// <summary>Connect and ready the link. Throws a human-readable exception on failure.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync();
}