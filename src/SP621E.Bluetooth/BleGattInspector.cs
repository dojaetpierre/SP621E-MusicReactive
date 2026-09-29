namespace SP621E.Bluetooth;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

/// <summary>
/// Connects to a BLE peripheral and enumerates its full GATT topology:
/// services, characteristics, characteristic properties, and negotiated PDU size.
///
/// UNVERIFIED until a human operator runs this against the real SP621E and returns
/// the produced <see cref="GattReport"/> (see HARDWARE_PROTOCOL.md §5 Request #1).
/// </summary>
public sealed class BleGattInspector
{
    /// <summary>
    /// Connects and enumerates GATT for the device at <paramref name="address"/>.
    /// Never throws: every failure mode is captured as a line in the report so the
    /// operator can return evidence even when things go wrong.
    /// </summary>
    public async Task<GattReport> InspectAsync(ulong address)
    {
        var report = new GattReport();
        report.AddSplitLine();
        report.AddLine($"Target : {BleDeviceScanner.FormatAddress(address)}");

        try
        {
            var adapter = await Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync();
            report.AddLine($"Adapter: {(adapter is null ? "NONE PRESENT" : "present")}");
            if (adapter is null)
            {
                report.AddLine("ERROR: No Bluetooth adapter is available on this machine. Cannot inspect.");
                return report;
            }

            report.AddLine($"LE     : supported={adapter.IsLowEnergySupported} classic={adapter.IsClassicSupported}");

            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            report.AddLine($"Device : {(device is null ? "<null> (FromBluetoothAddressAsync returned null)" : device.Name + " | " + device.BluetoothDeviceId.Id)}");
            if (device is null)
            {
                report.AddLine("ERROR: Could not resolve the device. Check the address and try again.");
                return report;
            }

            report.AddLine($"Connect: {device.ConnectionStatus}");

            GattSession? session = null;
            try
            {
                session = await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId);
                if (session is not null)
                {
                    session.MaintainConnection = true;
                    report.AddLine($"Session: MaintainConnection=true");
                }
            }
            catch (Exception ex)
            {
                report.AddLine($"WARN  : GattSession unavailable ({ex.GetType().Name}) — continuing without explicit session.");
            }

            var servicesResult = await device.GetGattServicesAsync();
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                report.AddLine($"ERROR : GetGattServicesAsync returned status={servicesResult.Status}, protocolError={servicesResult.ProtocolError}.");
                return report;
            }

            report.AddLine($"MTU    : session MaxPduSize={(session?.MaxPduSize ?? 0)} bytes (negotiated MTU ~ {((session?.MaxPduSize ?? 0) > 0 ? (session!.MaxPduSize + 3) : 0)} bytes)");
            report.AddSplitLine();
            report.AddLine($"Services discovered: {servicesResult.Services.Count}");

            foreach (var service in servicesResult.Services)
            {
                report.AddLine($"");
                report.AddLine($"SERVICE {service.Uuid}");
                try
                {
                    var charsResult = await service.GetCharacteristicsAsync();
                    if (charsResult.Status != GattCommunicationStatus.Success)
                    {
                        report.AddLine($"  ERROR: GetCharacteristicsAsync status={charsResult.Status}, protocolError={charsResult.ProtocolError}");
                        continue;
                    }

                    foreach (var characteristic in charsResult.Characteristics)
                    {
                        report.AddLine($"    CHAR  {characteristic.Uuid}");
                        report.AddLine($"      properties : {FormatProperties(characteristic.CharacteristicProperties)}");
                        var descriptorText = await ListDescriptors(characteristic);
                        if (descriptorText.Length > 0)
                            report.AddLine($"      descriptors: {descriptorText}");
                        if ((characteristic.CharacteristicProperties & GattCharacteristicProperties.Read) != 0)
                        {
                            try
                            {
                                var read = await characteristic.ReadValueAsync();
                                report.AddLine($"      read       : status={read.Status} value={FormatBytes(read.Value)}");
                            }
                            catch (Exception ex)
                            {
                                report.AddLine($"      read       : EXCEPTION {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.AddLine($"  ERROR enumerating characteristics: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    service.Dispose();
                }
            }

            report.AddSplitLine();
            report.AddLine("END OF REPORT");
        }
        catch (Exception ex)
        {
            report.AddLine("EXCEPTION: " + ex);
        }

        return report;
    }

    private static string FormatProperties(GattCharacteristicProperties properties)
    {
        var flags = new List<string>();
        if (properties.HasFlag(GattCharacteristicProperties.Broadcast)) flags.Add("Broadcast");
        if (properties.HasFlag(GattCharacteristicProperties.Read)) flags.Add("Read");
        if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)) flags.Add("WriteNoResp");
        if (properties.HasFlag(GattCharacteristicProperties.Write)) flags.Add("Write");
        if (properties.HasFlag(GattCharacteristicProperties.Notify)) flags.Add("Notify");
        if (properties.HasFlag(GattCharacteristicProperties.Indicate)) flags.Add("Indicate");
        if (properties.HasFlag(GattCharacteristicProperties.AuthenticatedSignedWrites)) flags.Add("SignedWrite");
        if (properties.HasFlag(GattCharacteristicProperties.ExtendedProperties)) flags.Add("Extended");
        if (properties.HasFlag(GattCharacteristicProperties.ReliableWrites)) flags.Add("ReliableWrites");
        if (properties.HasFlag(GattCharacteristicProperties.WritableAuxiliaries)) flags.Add("WritableAux");
        return flags.Count == 0 ? "(none)" : string.Join(", ", flags);
    }

    private static async Task<string> ListDescriptors(GattCharacteristic characteristic)
    {
        try
        {
            var result = await characteristic.GetDescriptorsAsync();
            if (result.Status != GattCommunicationStatus.Success || result.Descriptors.Count == 0)
                return string.Empty;
            return string.Join(", ", result.Descriptors.Select(d => d.Uuid.ToString()));
        }
        catch
        {
            return "(error reading descriptors)";
        }
    }

    private static string FormatBytes(Windows.Storage.Streams.IBuffer buffer)
    {
        if (buffer is null || buffer.Length == 0)
            return "(empty)";

        var data = new byte[buffer.Length];
        Windows.Storage.Streams.DataReader.FromBuffer(buffer).ReadBytes(data);
        return Convert.ToHexString(data);
    }
}