using System.IO;
using SP621E.Bluetooth.Snoop;

namespace SP621E.Bluetooth.Tests.Snoop;

public class SampleCaptureWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "sample-capture-tests-" + Guid.NewGuid().ToString("N"));

    public SampleCaptureWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void GeneratedCapture_RoundTripsThroughTheSharedParser()
    {
        var path = Path.Combine(_tempDir, "sample.btsnoop");
        SampleCaptureWriter.Write(path);

        var packets = new HciSnoopReader().Read(path);

        // MTU request + response, 3 write commands, 1 notification.
        Assert.Equal(2, packets.Count(p => p.Opcode is 0x02 or 0x03));
        Assert.Equal(3, packets.Count(p => p.Opcode == 0x52));
        Assert.Equal(1, packets.Count(p => p.Opcode == 0x18));

        // Direction bits are honored: writes came from the host, the notification from the device.
        Assert.All(packets.Where(p => p.Opcode == 0x52), p => Assert.True(p.SentByHost));
        var notif = Assert.Single(packets, p => p.Opcode == 0x18);
        Assert.False(notif.SentByHost);
        Assert.Equal(SampleCaptureWriter.NotifyHandle, notif.AttributeHandle);

        // Timestamps are sequential in time.
        var times = packets.Select(p => p.RelativeTime).ToArray();
        for (var i = 1; i < times.Length; i++)
            Assert.True(times[i] > times[i - 1], $"record {i} not later than record {i - 1}");
    }
}