namespace SP621E.Bluetooth;

/// <summary>
/// Minimal BLE/GATT transport seam. The driver logic is tested against a fake
/// transport here; the real WinRT-backed implementation wires up Windows
/// GATT characteristics once the GATT map is confirmed (Milestone 12).
/// </summary>
public interface IBleTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Maximum usable ATT payload bytes per write (MTU - 3). Driver chunks above this.</summary>
    int MaxPayloadBytes { get; }

    Task ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync();

    /// <summary>Send one ATT write to the device write characteristic.</summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>Device-&gt;host data on the notification/indication characteristic.</summary>
    event EventHandler<ReadOnlyMemory<byte>>? DataReceived;
}