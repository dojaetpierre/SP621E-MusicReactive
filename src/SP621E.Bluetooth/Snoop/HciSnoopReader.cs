using System.IO;

namespace SP621E.Bluetooth.Snoop;

/// <summary>
/// Reads Android "btsnoop" HCI capture files (as exported by the Bluetooth HCI
/// snoop log feature) and extracts the GATT ATT packet stream.
///
/// Format: global header (16B, magic "btsnoop" + 2 NUL), then 24-byte record
/// headers (datalink type 1002 = HCI UART H4), carrying HCI command/event/ACL
/// packets. Only HCI ACL Data packets are decoded (the carrier for L2CAP and ATT).
///
/// Assumptions (best-effort, flagged where relevant):
///  * HCI ACL handle/flags are decoded classic-style (12-bit handle, PB bit in the
///    upper nibble) — sufficient for the single-packet ATT PDUs these devices emit.
///  * L2CAP LE CID 0x0004 (ATT) starts at L2CAP header offset 4.
///  * LE controllers commonly set the L2CAP length field to 0, meaning one SDU per
///    ACL packet; when non-zero it is honoured for reassembly.
/// </summary>
public sealed class HciSnoopReader
{
    private readonly bool _printDebug;

    public HciSnoopReader(bool printDebug = false) => _printDebug = printDebug;

    public List<AttPacket> Read(string filePath)
    {
        var packets = new List<AttPacket>();
        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length < 16)
            throw new InvalidDataException("File too small to be a btsnoop capture.");

        // Byte-order detection from the magic: big-endian starts with 'b' (0x62);
        // little-endian files byte-reverse the identification and start with 0x00 0x70.
        bool bigEndian;
        if (bytes[0] == 0x62)
            bigEndian = true;
        else if (bytes[0] == 0x00 && bytes[1] == 0x70)
            bigEndian = false;
        else
            throw new InvalidDataException("File is not a btsnoop capture (bad magic).");

        var version = ReadU32(bytes, 8, bigEndian);
        if (version is not (0 or 1))
            throw new InvalidDataException($"Unsupported btsnoop version {version}.");

        var datalink = ReadU32(bytes, 12, bigEndian);
        if (datalink != 1002)
            throw new InvalidDataException(
                $"Unsupported datalink type {datalink} (expected 1002 = HCI UART H4). " +
                "Export the Android Bluetooth HCI snoop log (btsnoop_hci.log/.cfa) or convert your capture to btsnoop in Wireshark.");

        var offset = 16;
        var recordIndex = 0;
        var pending = new Dictionary<ushort, PendingSdu>();
        DateTimeOffset? start = null;

        while (offset + 24 <= bytes.Length)
        {
            var includedLength = ReadU32(bytes, offset + 4, bigEndian);
            var packetFlags = ReadU32(bytes, offset + 8, bigEndian);
            var seconds = ReadU32(bytes, offset + 16, bigEndian);
            var micros = ReadU32(bytes, offset + 20, bigEndian);
            offset += 24;

            if (includedLength == 0 || offset + includedLength > bytes.Length)
            {
                Debug($"Record {recordIndex}: bad included length {includedLength}, stopping.");
                break;
            }

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks((long)micros * 10);
            start ??= timestamp;
            var rel = timestamp - start.Value;

            var record = new ReadOnlySpan<byte>(bytes, (int)offset, (int)includedLength);
            offset += (int)includedLength;
            recordIndex++;

            var flagSentByHost = (packetFlags & 0x1) == 0x1;
            DecodeHci(record, flagSentByHost, rel, pending, packets);
        }

        return packets;
    }

    private void DecodeHci(
        ReadOnlySpan<byte> record,
        bool flagSentByHost,
        TimeSpan time,
        Dictionary<ushort, PendingSdu> pending,
        List<AttPacket> output)
    {
        if (record.Length == 0)
            return;

        var type = record[0];
        var payload = record[1..];

        switch (type)
        {
            case 0x02: // ACL Data
                DecodeAcl(payload, flagSentByHost, time, pending, output);
                break;
            default:
                Debug($"{time}: HCI type 0x{type:x2} (non-ACL) len={payload.Length}");
                break;
        }
    }

    private void DecodeAcl(
        ReadOnlySpan<byte> payload,
        bool flagSentByHost,
        TimeSpan time,
        Dictionary<ushort, PendingSdu> pending,
        List<AttPacket> output)
    {
        if (payload.Length < 4)
        {
            Debug($"{time}: ACL too short ({payload.Length}B)");
            return;
        }

        var handle = (ushort)(payload[0] | ((payload[1] & 0x0F) << 8));
        var flagsNibble = (byte)((payload[1] >> 4) & 0x0F);
        var l2cap = payload[4..];
        if (l2cap.Length < 4)
            return;

        var l2Length = l2cap[0] | (l2cap[1] << 8);
        var cid = (ushort)(l2cap[2] | (l2cap[3] << 8));
        var sdu = l2cap[4..];

        if (cid != 0x0004)
            return;

        if (!pending.TryGetValue(handle, out var existing))
        {
            existing = new PendingSdu();
            pending[handle] = existing;
        }

        var isNewFragment = (flagsNibble & 0x2) == 0;
        if (isNewFragment && existing.Data.Count > 0)
        {
            MaybeDecodeAtt(handle, existing.Bytes, existing.SentByHost, existing.LastTime, output);
            existing.Clear();
        }

        existing.Data.AddRange(sdu.ToArray());
        existing.LastTime = time;
        existing.SentByHost = flagSentByHost;

        if (l2Length > 0 && existing.Data.Count >= l2Length)
        {
            MaybeDecodeAtt(handle, existing.Data.Take(l2Length).ToArray(), flagSentByHost, existing.LastTime, output);
            existing.Clear();
        }
        else if (l2Length == 0)
        {
            // LE controllers commonly set length=0: one SDU per ACL packet.
            MaybeDecodeAtt(handle, existing.Data.ToArray(), flagSentByHost, time, output);
            existing.Clear();
        }
    }

    private void MaybeDecodeAtt(ushort handle, byte[] sdu, bool sentByHost, TimeSpan time, List<AttPacket> output)
    {
        if (sdu.Length == 0)
            return;

        var opcode = sdu[0];
        if (opcode is not (>= 0x01 and <= 0x1A) and not 0x52)
        {
            Debug($"{time}: ATT parse skipped, unrecognized opcode 0x{opcode:x2} len={sdu.Length}");
            return;
        }

        output.Add(AttDecoder.Decode(handle, sentByHost, time, sdu));
    }

    private void Debug(string message)
    {
        if (_printDebug)
            Console.WriteLine($"[snoop] {message}");
    }

    private sealed class PendingSdu
    {
        public readonly List<byte> Data = [];
        public TimeSpan LastTime;
        public bool SentByHost;

        public byte[] Bytes => Data.ToArray();

        public void Clear()
        {
            Data.Clear();
            LastTime = TimeSpan.Zero;
            SentByHost = false;
        }
    }

    private static uint ReadU32(byte[] buffer, int offset, bool bigEndian)
    {
        if (bigEndian)
            return ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) |
                   ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
        return buffer[offset] | ((uint)buffer[offset + 1] << 8) |
               ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);
    }
}