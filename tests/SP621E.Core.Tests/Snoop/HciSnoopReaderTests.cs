using System.IO;
using SP621E.Bluetooth.Snoop;

namespace SP621E.Bluetooth.Tests.Snoop;

public class HciSnoopReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "snoop-tests-" + Guid.NewGuid().ToString("N"));

    public HciSnoopReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void ExtractsWriteCommand_WithHandleAndValue()
    {
        var attHandle = new byte[] { 0x00, 0x09 };
        var attValue = new byte[] { 0xAA, 0xBB, 0xCC };
        var att = new byte[] { 0x52 }.Concat(attHandle).Concat(attValue).ToArray();

        var file = WriteSnoopFile(
            (0, true, BuildAclPacket(0x0040, att)),
            (0, false, BuildAclPacket(0x0040, att)));

        var packets = new HciSnoopReader().Read(file);

        // One host->controller and one controller->host copy.
        Assert.Equal(2, packets.Count(p => p.Opcode == 0x52));
        var hostSent = Assert.Single(packets, p => p.Opcode == 0x52 && p.SentByHost);
        Assert.Equal(0x0009, hostSent.AttributeHandle);
        Assert.Equal("AABBCC", hostSent.ValueHex);
    }

    [Fact]
    public void ExtractsWriteRequest_AndNotification()
    {
        var writeAtt = new byte[] { 0x12, 0x00, 0x03, 0x01, 0x00 };
        var notifAtt = new byte[] { 0x18, 0x00, 0x03, 0xFF, 0x00, 0xFF };

        var file = WriteSnoopFile(
            (1, true, BuildAclPacket(0x0040, writeAtt)),
            (1, false, BuildAclPacket(0x0040, notifAtt)));

        var packets = new HciSnoopReader().Read(file);

        var write = Assert.Single(packets, p => p.Opcode == 0x12);
        Assert.Equal(0x0003, write.AttributeHandle);
        Assert.Equal("0100", write.ValueHex);

        var notif = Assert.Single(packets, p => p.Opcode == 0x18);
        Assert.Equal(0x0003, notif.AttributeHandle);
        Assert.Equal("FF00FF", notif.ValueHex);
        Assert.False(notif.SentByHost);
    }

    [Fact]
    public void UnsupportedDatalink_ThrowsClearMessage()
    {
        var file = Path.Combine(_tempDir, "not-snoop.bin");
        File.WriteAllBytes(file, BuildGlobalHeader(datalink: 1) );
        var ex = Assert.Throws<InvalidDataException>(() => new HciSnoopReader().Read(file));
        Assert.Contains("1002", ex.Message);
    }

    [Fact]
    public void EmptyFile_ThrowsClearMessage()
    {
        var file = Path.Combine(_tempDir, "empty.btsnoop");
        File.WriteAllBytes(file, []);
        var ex = Assert.Throws<InvalidDataException>(() => new HciSnoopReader().Read(file));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void HeaderOnlyCapture_ReturnsEmpty()
    {
        var file = Path.Combine(_tempDir, "header-only.btsnoop");
        File.WriteAllBytes(file, BuildGlobalHeader());
        Assert.Empty(new HciSnoopReader().Read(file));
    }

    [Fact]
    public void GarbageFile_ThrowsInvalidData()
    {
        var file = Path.Combine(_tempDir, "garbage.bin");
        File.WriteAllBytes(file, Enumerable.Repeat((byte)0x42, 20).ToArray());
        var ex = Assert.Throws<InvalidDataException>(() => new HciSnoopReader().Read(file));
        Assert.Contains("not a btsnoop capture", ex.Message);
    }

    private string WriteSnoopFile(params (uint timeUs, bool sentByHost, byte[] acl)[] records)
    {
        var path = Path.Combine(_tempDir, "capture.btsnoop");
        using var writer = new BinaryWriter(File.Create(path));

        writer.Write(BuildGlobalHeader());

        foreach (var (timeUs, sentByHost, acl) in records)
        {
            // origLen(4) inclLen(4) flags(4) drops(4) secs(4) usecs(4), then data
            var usec = timeUs % 1_000_000;
            var sec = timeUs / 1_000_000;
            writer.Write(Be32((uint)acl.Length));
            writer.Write(Be32((uint)acl.Length));
            writer.Write(Be32(sentByHost ? 1u : 0u));
            writer.Write(Be32(0));
            writer.Write(Be32((uint)sec));
            writer.Write(Be32((uint)usec));
            writer.Write(acl);
        }

        writer.Flush();
        return path;
    }

    private static byte[] BuildGlobalHeader(uint datalink = 1002)
    {
        // 8-byte magic "btsnoop" + NUL, 4-byte version (BE=1), 4-byte datalink (BE=1002).
        var header = new byte[16];
        var magic = System.Text.Encoding.ASCII.GetBytes("btsnoop");
        Array.Copy(magic, header, magic.Length);
        Array.Copy(Be32(1), 0, header, 8, 4);
        Array.Copy(Be32(datalink), 0, header, 12, 4);
        return header;
    }

    private static byte[] BuildAclPacket(ushort connHandle, byte[] attPdu)
    {
        // L2CAP: length(2) CID(2) then ATT. LE controllers typically use length=0.
        var l2capPayload = new byte[] { 0x00, 0x00, 0x04, 0x00 }.Concat(attPdu).ToArray();
        // HCI ACL: type(1) handle(2) length(2)(LE) payload
        var header = new byte[]
        {
            0x02,
            (byte)(connHandle & 0xFF),
            (byte)((connHandle >> 8) & 0x0F), // + flag nibble 0 (first fragment)
            (byte)(l2capPayload.Length & 0xFF),
            (byte)((l2capPayload.Length >> 8) & 0xFF),
        };
        return header.Concat(l2capPayload).ToArray();
    }

    private static byte[] Be32(uint value) => new[]
    {
        (byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF),
    };
}