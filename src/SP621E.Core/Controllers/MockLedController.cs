namespace SP621E.Core.Controllers;

using SP621E.Core.LedFrame;

/// <summary>
/// Software-only controller used for development, tests, and demos.
/// Records frames so tests can assert on output with zero hardware.
/// Functional implementation lands at Milestone 6; this stub keeps the seam honest.
/// </summary>
public sealed class MockLedController : ILedController
{
    public MockLedController(int pixelCount)
    {
        PixelCount = pixelCount;
    }

    public string DisplayName => $"Mock controller ({PixelCount} LEDs)";

    public bool IsConnected { get; private set; }

    public int PixelCount { get; }

    public ControllerCapability Capabilities => ControllerCapability.FrameStreaming;

    public IReadOnlyList<RgbFrame> ReceivedFrames { get; private set; } = Array.Empty<RgbFrame>();

    public Task ConnectAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(RgbFrame frame, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Mock controller is not connected.");
        ReceivedFrames = [.. ReceivedFrames, frame];
        return Task.CompletedTask;
    }
}