namespace SP621E.Bluetooth.Snoop;

/// <summary>
/// Writes a structurally valid "btsnoop" HCI capture (big-endian, datalink 1002
/// HCI UART) containing sample ATT traffic — an MTU exchange, host writes, and a
/// device notification — so the analysis path (SnoopDecode CLI, the
/// VirtualSp621e tool, or the app's Advanced tab btsnoop import) can be exercised
/// end-to-end with zero hardware.
///
/// This is SAMPLE data, not SP621E evidence: the values are invented to prove the
/// tooling round-trips. A real BanlanX capture still comes from §5 Request #2.
/// </summary>
public static class SampleCaptureWriter
{
    /// <summary>ATT service handle used for frame/command writes.</summary>
    public const ushort WriteHandle = 0x000C;

    /// <summary>ATT notify handle used for device→host status.</summary>
    public const ushort NotifyHandle = 0x000D;

    public static void Write(string path)
    {
        var records = new List<(bool HostToDevice, byte[] Hci)>
        {
            // Host requests MTU 48.
            (true, Acl(Pdu(0x02, 0x30, 0x00))),
            // Device agrees (MTU 48).
            (false, Acl(Pdu(0x03, 0x30, 0x00))),
            // Host: Write Command to write handle — a frame header packet.
            (true, Acl(Pdu(0x52, 0x00, 0x0C, 0xF0, 0x08))),
            // Host: Write Command — frame payload (8 pixels x RGB, red→green run).
            (true, Acl(Pdu(0x52, 0x00, 0x0C, 0xF1,
                0xFF, 0x00, 0x00, 0xDD, 0x22, 0x00, 0xBB, 0x44, 0x00, 0x99, 0x66, 0x00,
                0x44, 0x99, 0x00, 0x22, 0xBB, 0x00, 0x00, 0xDD, 0x00, 0x00, 0xFF, 0x00))),
            // Host: Write Command — solid red (0x21).
            (true, Acl(Pdu(0x52, 0x00, 0x0C, 0x21, 0xFF, 0x00, 0x00))),
            // Device: Handle Value Notification on notify handle — status bytes.
            (false, Acl(Pdu(0x18, 0x00, 0x0D, 0x80, 0x01, 0x00))),
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);

        // Global header (16 bytes, big-endian): magic "btsnoop" + NUL, version 0, datalink 1002.
        stream.Write([0x62, 0x74, 0x73, 0x6E, 0x6F, 0x6F, 0x70, 0x00]);
        WriteU32(w, 0);
        WriteU32(w, 1002);

        // One record every 250 ms.
        var baseSeconds = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        for (var i = 0; i < records.Count; i++)
        {
            var (hostToDevice, hci) = records[i];
            WriteU32(w, (uint)hci.Length);                 // original length
            WriteU32(w, (uint)hci.Length);                 // included length
            WriteU32(w, hostToDevice ? 1u : 0u);           // packet flags (direction bit 0)
            WriteU32(w, 0);                                // cumulative drops
            var elapsedUs = i * 250_000L;
            WriteU32(w, (uint)(baseSeconds + elapsedUs / 1_000_000));
            WriteU32(w, (uint)(elapsedUs % 1_000_000));
            w.Write(hci);
        }
    }

    private static byte[] Pdu(params byte[] v) => v;

    private static byte[] Acl(byte[] attPdu)
    {
        var bytes = new byte[9 + attPdu.Length];
        bytes[0] = 0x02;                                   // HCI ACL Data
        bytes[1] = 0x08;                                   // connection handle low
        bytes[2] = 0x10;                                   // flags (start fragment) + handle high nibble
        bytes[3] = 0x00;                                   // HCI ACL length low (unused by reader)
        bytes[4] = 0x00;                                   // HCI ACL length high
        bytes[5] = 0x00;                                   // L2CAP length low (LE-controller style: 0 = one SDU)
        bytes[6] = 0x00;                                   // L2CAP length high
        bytes[7] = 0x04;                                   // L2CAP CID low (ATT 0x0004)
        bytes[8] = 0x00;                                   // L2CAP CID high
        Array.Copy(attPdu, 0, bytes, 9, attPdu.Length);
        return bytes;
    }

    private static void WriteU32(BinaryWriter w, uint value)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)(value >> 24);
        bytes[1] = (byte)(value >> 16);
        bytes[2] = (byte)(value >> 8);
        bytes[3] = (byte)value;
        w.Write(bytes);
    }
}