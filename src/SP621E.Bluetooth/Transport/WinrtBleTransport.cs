namespace SP621E.Bluetooth.Transport;

using System.Runtime.CompilerServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

/// <summary>
/// Optional pinning so the transport can be locked to the exact GATT shape BanlanX uses once confirmed by capture evidence.
/// </summary>
public sealed record WinrtBleTransportOptions
{
    /// <summary>Service UUID to restrict discovery to (null = pick the first service containing a usable write characteristic).</summary>
    public Guid? ServiceUuid { get; init; }

    /// <summary>Write characteristic UUID (null = auto-detect: first Write/WriteWithoutResponse characteristic).</summary>
    public Guid? WriteCharacteristicUuid { get; init; }

    /// <summary>Notification characteristic UUID (null = auto-detect the device's notification endpoint, if any).</summary>
    public Guid? NotifyCharacteristicUuid { get; init; }
}

/// <summary>
/// Real Windows GATT transport over <c>Windows.Devices.Bluetooth</c>.
///
/// Connects to a physical SP621E peripheral, resolves the write/notify
/// characteristics (pinned UUIDs when known from evidence, otherwise
/// auto-detected), streams frames to the write characteristic and forwards
/// notifications out of <see cref="IBleTransport.DataReceived"/>.
/// </summary>
public sealed class WinrtBleTransport : IBleTransport
{
    private readonly ulong _address;
    private readonly WinrtBleTransportOptions _options;
    private readonly object _gate = new();
    private BluetoothLEDevice? _device;
    private GattSession? _session;
    private GattCharacteristic? _writeCharacteristic;
    private GattCharacteristic? _notifyCharacteristic;
    private bool _disposed;

    public WinrtBleTransport(ulong address, WinrtBleTransportOptions? options = null)
    {
        _address = address;
        _options = options ?? new WinrtBleTransportOptions();
    }

    public string DeviceAddressText => BleDeviceScanner.FormatAddress(_address);

    public bool IsConnected
    {
        get
        {
            lock (_gate)
                return _device is not null
                    && _device.ConnectionStatus == BluetoothConnectionStatus.Connected
                    && _writeCharacteristic is not null;
        }
    }

    public int MaxPayloadBytes
    {
        get
        {
            lock (_gate)
            {
                if (_session is not null && _session.MaxPduSize > 3)
                    return _session.MaxPduSize - 3;
                return 20;
            }
        }
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        BluetoothLEDevice device;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_device is not null)
                throw new InvalidOperationException("Transport is already connected (or connecting).");
        }

        device = await BluetoothLEDevice.FromBluetoothAddressAsync(_address).AsTask(ct).ConfigureAwait(false)
                 ?? throw new InvalidOperationException($"No BLE device found at {DeviceAddressText}. Is the SP621E powered and in range?");

        GattSession? session = null;
        try
        {
            session = await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(ct).ConfigureAwait(false);
            if (session is not null)
                session.MaintainConnection = true;
        }
        catch
        {
            // Older stacks may not surface a session; MaxPayloadBytes falls back to 20.
        }

        var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(ct).ConfigureAwait(false);
        if (servicesResult.Status != GattCommunicationStatus.Success)
        {
            session?.Dispose();
            device.Dispose();
            throw new InvalidOperationException(
                $"GATT service discovery failed (status={servicesResult.Status}, protocolError={servicesResult.ProtocolError}).");
        }

        var writeCharacteristic = ResolveWriteCharacteristic(servicesResult.Services)
                                  ?? throw new InvalidOperationException(
                                      "No writable characteristic found on this device. Collect the GATT map first (HARDWARE_PROTOCOL.md §5 Request #1).");
        var notifyCharacteristic = ResolveNotifyCharacteristic(servicesResult.Services);

        lock (_gate)
        {
            if (_disposed)
            {
                session?.Dispose();
                device.Dispose();
                throw new ObjectDisposedException(nameof(WinrtBleTransport));
            }
            _device = device;
            _session = session;
            _writeCharacteristic = writeCharacteristic;
            _notifyCharacteristic = notifyCharacteristic;
        }

        _writeCharacteristic.ValueChanged += OnValueChanged;
        if (_notifyCharacteristic is not null && _notifyCharacteristic != _writeCharacteristic)
            _notifyCharacteristic.ValueChanged += OnValueChanged;

        if (_notifyCharacteristic is not null)
        {
            var status = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(ct).ConfigureAwait(false);
            if (status != GattCommunicationStatus.Success)
                _notifyCharacteristic = null;
        }
    }

    public Task DisconnectAsync()
    {
        lock (_gate)
        {
            var device = _device;
            _device = null;
            var session = _session;
            _session = null;

            var writeCharacteristic = _writeCharacteristic;
            _writeCharacteristic = null;

            if (writeCharacteristic is not null)
                writeCharacteristic.ValueChanged -= OnValueChanged;
            if (_notifyCharacteristic is not null && _notifyCharacteristic != writeCharacteristic)
                _notifyCharacteristic.ValueChanged -= OnValueChanged;
            _notifyCharacteristic = null;

            if (device is not null)
            {
                try
                {
                    device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    device.Dispose();
                }
                catch
                {
                }
            }
            session?.Dispose();
        }
        return Task.CompletedTask;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        GattCharacteristic? characteristic;
        lock (_gate)
        {
            characteristic = _writeCharacteristic;
            if (characteristic is null)
                throw new InvalidOperationException("Not connected to the SP621E.");
        }

        var buffer = ToWinRtBuffer(data);
        var option = (characteristic.CharacteristicProperties & GattCharacteristicProperties.WriteWithoutResponse) != 0
            ? GattWriteOption.WriteWithoutResponse
            : GattWriteOption.WriteWithResponse;

        var result = await characteristic.WriteValueAsync(buffer, option).AsTask(ct).ConfigureAwait(false);
        if (result != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"BLE write failed (status={result}).");
    }

    private static IBuffer ToWinRtBuffer(ReadOnlyMemory<byte> data)
    {
        using var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.WriteBytes(data.ToArray());
        return writer.DetachBuffer();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        await DisconnectAsync().ConfigureAwait(false);
    }

    private GattCharacteristic? ResolveWriteCharacteristic(IReadOnlyList<GattDeviceService> services)
    {
        foreach (var service in services)
        {
            if (_options.ServiceUuid is { } pinned && service.Uuid != pinned)
                continue;

            try
            {
                var characteristics = service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
                if (characteristics.Status != GattCommunicationStatus.Success)
                    continue;

                foreach (var characteristic in characteristics.Characteristics)
                {
                    if (MatchesPinned(characteristic, _options.WriteCharacteristicUuid))
                        continue;

                    var properties = characteristic.CharacteristicProperties;
                    if ((properties & (GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse)) != 0)
                        return characteristic;
                }
            }
            finally
            {
                service.Dispose();
            }
        }
        return null;
    }

    private GattCharacteristic? ResolveNotifyCharacteristic(IReadOnlyList<GattDeviceService> services)
    {
        foreach (var service in services)
        {
            if (_options.ServiceUuid is { } pinned && service.Uuid != pinned)
                continue;

            try
            {
                var characteristics = service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
                if (characteristics.Status != GattCommunicationStatus.Success)
                    continue;

                foreach (var characteristic in characteristics.Characteristics)
                {
                    if (!MatchesPinned(characteristic, _options.NotifyCharacteristicUuid))
                        continue;
                    if ((characteristic.CharacteristicProperties & (GattCharacteristicProperties.Notify | GattCharacteristicProperties.Indicate)) != 0)
                        return characteristic;
                }
            }
            finally
            {
                service.Dispose();
            }
        }
        return null;
    }

    /// <summary>True when the characteristic should be considered for this slot (pinned match, or nothing pinned).</summary>
    private static bool MatchesPinned(GattCharacteristic characteristic, Guid? pinned)
        => pinned is null || characteristic.Uuid == pinned;

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var data = new byte[args.CharacteristicValue.Length];
        DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
        DataReceived?.Invoke(this, data);
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args) { }
}

internal static class WinRTTasks
{
    /// <summary>Wraps a WinRT IAsyncOperation in a cancellable Task.</summary>
    public static async Task<T> AsTask<T>(this Windows.Foundation.IAsyncOperation<T> operation, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() =>
        {
            operation.Cancel();
            tcs.TrySetCanceled(ct);
        });
        operation.Completed = (asyncInfo, status) =>
        {
            switch (status)
            {
                case Windows.Foundation.AsyncStatus.Completed:
                    tcs.TrySetResult(asyncInfo.GetResults());
                    break;
                case Windows.Foundation.AsyncStatus.Canceled:
                    tcs.TrySetCanceled();
                    break;
                case Windows.Foundation.AsyncStatus.Error:
                    tcs.TrySetException(asyncInfo.ErrorCode);
                    break;
            }
        };
        return await tcs.Task.ConfigureAwait(false);
    }
}

internal static class WinRTTasksNonGeneric
{
    /// <summary>Wraps a WinRT IAsyncAction in a cancellable Task.</summary>
    public static async Task AsTask(this Windows.Foundation.IAsyncAction action, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() =>
        {
            action.Cancel();
            tcs.TrySetCanceled(ct);
        });
        action.Completed = (asyncInfo, status) =>
        {
            switch (status)
            {
                case Windows.Foundation.AsyncStatus.Completed:
                    tcs.TrySetResult();
                    break;
                case Windows.Foundation.AsyncStatus.Canceled:
                    tcs.TrySetCanceled();
                    break;
                case Windows.Foundation.AsyncStatus.Error:
                    tcs.TrySetException(asyncInfo.ErrorCode);
                    break;
            }
        };
        await tcs.Task.ConfigureAwait(false);
    }
}