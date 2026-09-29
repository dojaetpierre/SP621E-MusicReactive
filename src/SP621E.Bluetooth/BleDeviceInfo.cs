namespace SP621E.Bluetooth;

/// <summary>
/// A discovered Bluetooth Low Energy peripheral.
/// </summary>
public sealed record BleDeviceInfo(string Name, string Address, ulong AddressValue, int Rssi)
{
    public override string ToString() => $"{Name} [{Address}] RSSI {Rssi}dBm";
}

public sealed class BleDeviceDiscoveredEventArgs(BleDeviceInfo device) : EventArgs
{
    public BleDeviceInfo Device { get; } = device;
}