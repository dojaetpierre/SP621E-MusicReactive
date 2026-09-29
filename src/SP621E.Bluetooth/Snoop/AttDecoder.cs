namespace SP621E.Bluetooth.Snoop;

/// <summary>
/// Decodes ATT PDUs, extracting the payloads that matter for protocol forensics:
/// writes (0x12, 0x52), notifications/indications (0x18, 0x19), and the
/// service/characteristic discovery responses that reveal handle-to-UUID maps.
/// </summary>
public static class AttDecoder
{
    /// <summary>
    /// Decodes one ATT PDU (opcode + payload, as carried in an L2CAP LE credit SDU).
    /// </summary>
    public static AttPacket Decode(ushort connectionHandle, bool sentByHost, TimeSpan time, byte[] pdu)
    {
        if (pdu.Length == 0)
        {
            return new AttPacket
            {
                ConnectionHandle = connectionHandle,
                SentByHost = sentByHost,
                RelativeTime = time,
                Opcode = 0x00,
                OpcodeName = "Empty PDU",
                Pdu = pdu,
            };
        }

        var opcode = pdu[0];
        var payload = pdu[1..];
        ushort attributeHandle = 0;
        byte[] value = [];

        // Ops carrying [handle(2)] [+ value...]
        switch (opcode)
        {
            case 0x12: // Write Request
            case 0x52: // Write Command
            case 0x14: // Prepare Write Request
            case 0x18: // Handle Value Notification
            case 0x19: // Handle Value Indication
                if (payload.Length >= 2)
                {
                    attributeHandle = (ushort)((payload[0] << 8) | payload[1]);
                    value = payload[2..];
                }
                break;
        }

        return new AttPacket
        {
            ConnectionHandle = connectionHandle,
            SentByHost = sentByHost,
            RelativeTime = time,
            Opcode = opcode,
            OpcodeName = AttPacket.OpcodeNameOf(opcode),
            Pdu = pdu,
            AttributeHandle = attributeHandle,
            Value = value,
        };
    }
}