namespace VirtualSp621e;

using SP621E.Bluetooth;

/// <summary>
/// In-memory stand-in for the BLE transport. Reports itself connected, records
/// every write, counts bytes, and forwards any decoded frame bytes to the
/// attached <see cref="VirtualStrip"/> so the simulation can show the "hardware".
/// </summary>
public sealed class VirtualBleTransport : IBleTransport
{
    private readonly object _gate = new();

    public VirtualBleTransport(int maxPayloadBytes = 20) => MaxPayloadBytes = maxPayloadBytes;

    /// <summary>The "virtual strip" fed with decoded writes; may be null (writes counted only).</summary>
    public VirtualStrip? Strip { get; set; }

    public bool IsConnected { get; private set; }

    /// <summary>ATT payload bytes per write (MTU - 3). Set low to exercise driver chunking.</summary>
    public int MaxPayloadBytes { get; }

    public int WriteCount { get; private set; }

    public long BytesWritten { get; private set; }

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

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Virtual transport is not connected.");
            WriteCount++;
            BytesWritten += data.Length;
            Strip?.OnWrite(data);
        }
        return Task.CompletedTask;
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public void RaiseData(byte[] data) => DataReceived?.Invoke(this, data);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}