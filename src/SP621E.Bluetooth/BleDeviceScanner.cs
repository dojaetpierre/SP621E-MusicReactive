namespace SP621E.Bluetooth;

using Windows.Devices.Bluetooth.Advertisement;

/// <summary>
/// Passive/active BLE advertiser watcher. Raises <see cref="DeviceDiscovered"/> for
/// every advertisement received. UNVERIFIED against real hardware until a human
/// operator runs it and confirms the target SP621E appears.
/// </summary>
public sealed class BleDeviceScanner : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher = new()
    {
        ScanningMode = BluetoothLEScanningMode.Active,
    };

    private readonly Dictionary<ulong, BleDeviceInfo> _devices = new();

    public bool IsScanning => _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    public event EventHandler<BleDeviceDiscoveredEventArgs>? DeviceDiscovered;
    public event EventHandler<string>? ScanFailed;

    public IReadOnlyList<BleDeviceInfo> Devices => _devices.Values.ToArray();

    public void Start()
    {
        if (IsScanning)
            return;

        _devices.Clear();
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;
        _watcher.Start();
    }

    public void Stop()
    {
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Stopped)
            return;

        _watcher.Stop();
        _watcher.Received -= OnReceived;
        _watcher.Stopped -= OnStopped;
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (sender.Status == BluetoothLEAdvertisementWatcherStatus.Aborted)
            ScanFailed?.Invoke(this, $"Advertisement scan aborted (error 0x{(int)args.Error:x8}).");
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
            ? "(unnamed)"
            : args.Advertisement.LocalName;

        var device = new BleDeviceInfo(name, FormatAddress(args.BluetoothAddress), args.BluetoothAddress, args.RawSignalStrengthInDBm);

        if (_devices.TryGetValue(device.AddressValue, out var existing))
        {
            // Prefer a stronger signal and a real name over "(unnamed)".
            if (existing.Name == "(unnamed)" && device.Name != "(unnamed)")
                _devices[device.AddressValue] = device;
            else if (device.Rssi > existing.Rssi)
                _devices[device.AddressValue] = device with { Name = existing.Name };
        }
        else
        {
            _devices[device.AddressValue] = device;
        }

        DeviceDiscovered?.Invoke(this, new BleDeviceDiscoveredEventArgs(_devices[device.AddressValue]));
    }

    public void Dispose()
    {
        if (IsScanning)
            Stop();
    }

    public static string FormatAddress(ulong address)
    {
        var hex = address.ToString("X12");
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }
}