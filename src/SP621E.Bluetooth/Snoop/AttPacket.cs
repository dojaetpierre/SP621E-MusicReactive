namespace SP621E.Bluetooth.Snoop;

/// <summary>
/// An extracted ATT (GATT) packet with its connection context and direction.
/// </summary>
public sealed class AttPacket
{
    public required ushort ConnectionHandle { get; init; }
    public required bool SentByHost { get; init; }
    public required TimeSpan RelativeTime { get; init; }
    public required byte Opcode { get; init; }
    public required string OpcodeName { get; init; }
    public required byte[] Pdu { get; init; }
    public ushort AttributeHandle { get; init; }
    public byte[] Value { get; init; } = [];

    public string ValueHex => Value.Length == 0 ? string.Empty : Convert.ToHexString(Value);

    public override string ToString()
    {
        var dir = SentByHost ? "HOST->CTRL" : "CTRL->HOST";
        var ctx = AttributeHandle != 0 ? $" handle={AttributeHandle:x4} val={ValueHex}" : string.Empty;
        return $"{RelativeTime.TotalSeconds,8:0.000}s [{dir}] {OpcodeName} (0x{Opcode:x2}){ctx}";
    }

    /// <summary>ATT opcode name lookup; unknown opcodes get a hex name.</summary>
    public static string OpcodeNameOf(byte opcode) =>
        OpcodeNames.TryGetValue(opcode, out var name) ? name : $"Unknown(0x{opcode:x2})";

    private static readonly Dictionary<byte, string> OpcodeNames = new()
    {
        [0x01] = "Error Response",
        [0x02] = "Exchange MTU Request",
        [0x03] = "Exchange MTU Response",
        [0x04] = "Find Information Request",
        [0x05] = "Find Information Response",
        [0x06] = "Find By Type Value Request",
        [0x07] = "Find By Type Value Response",
        [0x08] = "Read By Type Request",
        [0x09] = "Read By Type Response",
        [0x0A] = "Read Request",
        [0x0B] = "Read Response",
        [0x0C] = "Read Blob Request",
        [0x0D] = "Read Blob Response",
        [0x0E] = "Read Multiple Request",
        [0x0F] = "Read Multiple Response",
        [0x10] = "Read By Group Type Request",
        [0x11] = "Read By Group Type Response",
        [0x12] = "Write Request",
        [0x13] = "Write Response",
        [0x14] = "Prepare Write Request",
        [0x15] = "Prepare Write Response",
        [0x16] = "Execute Write Request",
        [0x17] = "Execute Write Response",
        [0x18] = "Handle Value Notification",
        [0x19] = "Handle Value Indication",
        [0x1A] = "Handle Value Confirmation",
        [0x52] = "Write Command (Write Without Response)",
    };
}