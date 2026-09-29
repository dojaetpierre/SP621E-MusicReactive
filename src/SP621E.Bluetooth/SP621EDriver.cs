namespace SP621E.Bluetooth;

using System.Threading.Channels;
using SP621E.Core.Controllers;
using SP621E.Core.LedFrame;
using SP621E.Bluetooth.Wire;

/// <summary>Pacing/robustness parameters for an <see cref="SP621EDriver"/>.</summary>
public sealed record DriverOptions
{
    /// <summary>Minimum gap between consecutive write packets (protocol pacing; device-dependent, set from HCI evidence).</summary>
    public TimeSpan InterPacketDelay { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Real SP621E BLE driver.
///
/// The driver itself is protocol-agnostic and hardware-free: it serializes commands
/// (one in flight, paced), chunks oversize payloads to the transport MTU, and
/// converts every high-level call through <see cref="ISp621eWireFormat"/> — the ONLY
/// component allowed to produce device bytes.
///
/// Until a wire format built from Milestone-3 HCI evidence is supplied,
/// <see cref="Capabilities"/> is <see cref="ControllerCapability.None"/> and every
/// command throws with a pointer to the evidence, so nothing is ever sent blind.
/// </summary>
public sealed class SP621EDriver : ILedController, IParametricController, IDisposable
{
    private sealed record WireJob(ReadOnlyMemory<byte>[] Packets, TaskCompletionSource Tcs, CancellationToken Cancellation);

    private readonly IBleTransport _transport;
    private readonly ISp621eWireFormat? _wire;
    private readonly DriverOptions _options;
    private readonly Channel<WireJob> _jobs = Channel.CreateUnbounded<WireJob>();
    private readonly object _pumpLock = new();
    private Task? _pump;

    public SP621EDriver(int pixelCount, IBleTransport transport, ISp621eWireFormat? wireFormat, DriverOptions? options = null)
    {
        if (pixelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelCount), "Pixel count must be positive.");
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _wire = wireFormat;
        _options = options ?? new DriverOptions();
        PixelCount = wireFormat?.PixelBufferSize ?? pixelCount;
        _transport.DataReceived += Transport_DataReceived;
    }

    public string DisplayName => "SP621E (BLE)";

    public bool IsConnected => _transport.IsConnected;

    public int PixelCount { get; }

    public ControllerCapability Capabilities => _wire switch
    {
        null => ControllerCapability.None,
        { SupportsFrameStreaming: true, SupportsParametric: true } => ControllerCapability.FrameStreaming | ControllerCapability.Parametric,
        { SupportsFrameStreaming: true } => ControllerCapability.FrameStreaming,
        { SupportsParametric: true } => ControllerCapability.Parametric,
        _ => ControllerCapability.None,
    };

    /// <summary>Device-&gt;host data (notifications) surfaced for diagnostics and protocol forensics.</summary>
    public event EventHandler<ReadOnlyMemory<byte>>? DeviceDataReceived;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _transport.ConnectAsync(ct).ConfigureAwait(false);
        EnsurePumpStarted();
    }

    public async Task DisconnectAsync()
    {
        _jobs.Writer.TryComplete();
        if (_pump is not null)
        {
            try { await _pump.ConfigureAwait(false); } catch (OperationCanceledException) { }
            _pump = null;
        }
        await _transport.DisconnectAsync().ConfigureAwait(false);
    }

    public Task SendFrameAsync(RgbFrame frame, CancellationToken ct = default)
    {
        if (!Capabilities.HasFlag(ControllerCapability.FrameStreaming))
            return Task.FromException(NoWireFormat("send frames"));
        if (frame is null)
            throw new ArgumentNullException(nameof(frame));
        ArgumentNullException.ThrowIfNull(_wire);
        return EnqueueAsync(_wire.EncodeFrame(frame), ct);
    }

    public IReadOnlyList<string> EffectNames => Array.Empty<string>();

    public Task SetPowerAsync(bool on, CancellationToken ct = default)
    {
        if (!Capabilities.HasFlag(ControllerCapability.Parametric))
            return Task.FromException(NoWireFormat("send power commands"));
        ArgumentNullException.ThrowIfNull(_wire);
        return EnqueueAsync(_wire.EncodePower(on), ct);
    }

    public Task SetColorAsync(Rgb color, CancellationToken ct = default)
    {
        if (!Capabilities.HasFlag(ControllerCapability.Parametric))
            return Task.FromException(NoWireFormat("send color commands"));
        ArgumentNullException.ThrowIfNull(_wire);
        return EnqueueAsync(_wire.EncodeColor(color), ct);
    }

    public Task SetBrightnessAsync(double brightness, CancellationToken ct = default)
    {
        if (!Capabilities.HasFlag(ControllerCapability.Parametric))
            return Task.FromException(NoWireFormat("send brightness commands"));
        ArgumentNullException.ThrowIfNull(_wire);
        return EnqueueAsync(_wire.EncodeBrightness(brightness), ct);
    }

    public Task SetEffectAsync(string effectName, CancellationToken ct = default)
    {
        if (!Capabilities.HasFlag(ControllerCapability.Parametric))
            return Task.FromException(NoWireFormat("select effects"));
        ArgumentNullException.ThrowIfNull(_wire);
        return EnqueueAsync(_wire.EncodeEffect(effectName), ct);
    }

    private NotSupportedException NoWireFormat(string what)
    {
        var reference = _wire?.EvidenceReference ?? "not supplied";
        return new NotSupportedException(
            $"SP621E driver cannot {what}: no confirmed wire format. " +
            $"Wire format reference: {reference}. Requires Milestone-3 HCI capture evidence (HARDWARE_PROTOCOL.md §5) — the protocol bytes are never guessed.");
    }

    private void EnsurePumpStarted()
    {
        lock (_pumpLock)
        {
            if (_pump is null || _pump.IsCompleted)
                _pump = RunPumpAsync();
        }
    }

    private async Task EnqueueAsync(IEnumerable<ReadOnlyMemory<byte>> packets, CancellationToken ct)
    {
        if (!_transport.IsConnected)
            throw new InvalidOperationException("SP621E driver is not connected.");

        var job = new WireJob(packets.ToArray(),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), ct);
        EnsurePumpStarted();
        using var registration = ct.Register(() => job.Tcs.TrySetCanceled(ct));
        if (!_jobs.Writer.TryWrite(job))
            throw new InvalidOperationException("Driver write queue is closed.");
        await job.Tcs.Task.ConfigureAwait(false);
    }

    private async Task RunPumpAsync()
    {
        try
        {
            while (await _jobs.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_jobs.Reader.TryRead(out var job))
                {
                    try
                    {
                        foreach (var packet in job.Packets)
                        {
                            job.Cancellation.ThrowIfCancellationRequested();
                            await WriteChunkedAsync(packet, job.Cancellation).ConfigureAwait(false);
                            if (_options.InterPacketDelay > TimeSpan.Zero)
                                await Task.Delay(_options.InterPacketDelay, job.Cancellation).ConfigureAwait(false);
                        }
                        job.Tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        job.Tcs.TrySetException(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Channel closed during disconnect.
        }
    }

    private async Task WriteChunkedAsync(ReadOnlyMemory<byte> packet, CancellationToken ct)
    {
        var max = _transport.MaxPayloadBytes > 0 ? _transport.MaxPayloadBytes : 20;
        while (packet.Length > max)
        {
            await _transport.WriteAsync(packet[..max], ct).ConfigureAwait(false);
            packet = packet[max..];
            if (_options.InterPacketDelay > TimeSpan.Zero)
                await Task.Delay(_options.InterPacketDelay, ct).ConfigureAwait(false);
        }
        await _transport.WriteAsync(packet, ct).ConfigureAwait(false);
    }

    private void Transport_DataReceived(object? sender, ReadOnlyMemory<byte> data) =>
        DeviceDataReceived?.Invoke(this, data);

    public void Dispose()
    {
        _transport.DataReceived -= Transport_DataReceived;
        _jobs.Writer.TryComplete();
    }
}