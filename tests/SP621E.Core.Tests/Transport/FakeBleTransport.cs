namespace SP621E.Bluetooth.Tests.Transport;

using SP621E.Bluetooth;

/// <summary>In-memory transport for driver tests. Records every write; can be told to fail.</summary>
public sealed class FakeBleTransport : IBleTransport
{
    public List<byte[]> Writes { get; } = [];

    public bool IsConnected { get; private set; }

    public int MaxPayloadBytes { get; set; } = 20;

    /// <summary>When set, WriteAsync throws (simulates link loss mid-stream).</summary>
    public bool FailWrites { get; set; }

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
        if (FailWrites)
            throw new IOException("simulated link failure");
        Writes.Add(data.ToArray());
        return Task.CompletedTask;
    }

    public void RaiseData(byte[] data) => DataReceived?.Invoke(this, data);

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}