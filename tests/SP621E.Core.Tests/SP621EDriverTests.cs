namespace SP621E.Bluetooth.Tests;

using System.Diagnostics;
using SP621E.Bluetooth;
using SP621E.Bluetooth.Tests.Transport;
using SP621E.Bluetooth.Tests.Wire;
using SP621E.Core.Controllers;
using SP621E.Core.LedFrame;

public class SP621EDriverTests
{
    private static RgbFrame Frame(params Rgb[] pixels)
    {
        var list = pixels.ToList();
        return new RgbFrame(list.Count, list, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task WithoutWireFormat_CapabilitiesAreNone_AndCommandsFailWithGuidance()
    {
        var transport = new FakeBleTransport();
        using var driver = new SP621EDriver(12, transport, wireFormat: null);
        Assert.Equal(ControllerCapability.None, driver.Capabilities);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => driver.SendFrameAsync(Frame(new Rgb(1, 2, 3))));
        Assert.Contains("wire format", ex.Message);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public async Task SendFrameAsync_DeliversEncodedPacketsInOrder()
    {
        var transport = new FakeBleTransport();
        using var driver = new SP621EDriver(12, transport, new FakeSp621eWireFormat());
        await driver.ConnectAsync();

        await driver.SendFrameAsync(Frame(new Rgb(10, 20, 30), new Rgb(200, 0, 50)));

        Assert.Equal(2, transport.Writes.Count);
        Assert.Equal(new byte[] { 0xA1, 0x02 }, transport.Writes[0]);
        Assert.Equal(new byte[] { 0xA2, 0x0A, 0x14, 0x1E, 0xC8, 0x00, 0x32 }, transport.Writes[1]);
    }

    [Fact]
    public async Task SendsAreSerialized_AndPaced()
    {
        var transport = new FakeBleTransport();
        const int packetCount = 10;
        var paced = new FakeSp621eWireFormat
        {
            PixelBufferSize = 12,
        };
        using var driver = new SP621EDriver(12, transport, paced,
            new DriverOptions { InterPacketDelay = TimeSpan.FromMilliseconds(15) });
        await driver.ConnectAsync();

        var watch = Stopwatch.StartNew();
        // Enqueue 10 frames speculatively (beyond the pacing capacity).
        var tasks = Enumerable.Range(0, packetCount).Select(_ =>
            driver.SendFrameAsync(Frame(new Rgb(1, 1, 1)))).ToArray();
        await Task.WhenAll(tasks);
        watch.Stop();

        Assert.Equal(packetCount * 2, transport.Writes.Count);
        Assert.True(watch.ElapsedMilliseconds >= (packetCount * 2 - 1) * 15,
            $"Expected pacing to hold ~ {packetCount * 2} gaps of 15ms, took {watch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task OversizedPacket_IsChunkedToTransportMaxPayload()
    {
        var transport = new FakeBleTransport { MaxPayloadBytes = 8 };
        var wire = new FakeSp621eWireFormat();
        using var driver = new SP621EDriver(12, transport, wire);
        await driver.ConnectAsync();

        // EncodeFrame emits header [0xA1,12] then 0xA2 + 36 pixel bytes = 37 B -> 8+8+8+8+5.
        await driver.SendFrameAsync(new RgbFrame(12, Enumerable.Repeat(new Rgb(0xFF, 0, 0), 12).ToList(), DateTimeOffset.UtcNow));

        Assert.Equal(6, transport.Writes.Count);            // 1 header + 5 chunks
        Assert.Equal(new byte[] { 0xA1, 0x0C }, transport.Writes[0]);

        var chunks = transport.Writes.Skip(1).ToList();
        Assert.Equal([8, 8, 8, 8, 5], chunks.Select(c => c.Length).ToArray());
        var reassembled = chunks.SelectMany(c => c).ToArray();
        Assert.Equal(37, reassembled.Length);
        Assert.Equal(0xA2, reassembled[0]);
        // 12 x pixel (0xFF, 0x00, 0x00)
        var expected = Enumerable.Repeat(new byte[] { 0xFF, 0x00, 0x00 }, 12).SelectMany(b => b);
        Assert.True(reassembled.Skip(1).SequenceEqual(expected));
    }

    [Fact]
    public async Task ParametricCommands_EncodeCorrectly()
    {
        var transport = new FakeBleTransport();
        using var driver = new SP621EDriver(12, transport, new FakeSp621eWireFormat());
        await driver.ConnectAsync();

        Assert.True(driver.Capabilities.HasFlag(ControllerCapability.Parametric));
        await driver.SetPowerAsync(true);
        await driver.SetColorAsync(new Rgb(5, 250, 10));
        await driver.SetBrightnessAsync(0.5);
        await driver.SetEffectAsync("fun");

        Assert.Equal(new byte[] { 0xA3, 0x01 }, transport.Writes[0]);
        Assert.Equal(new byte[] { 0xA4, 0x05, 0xFA, 0x0A }, transport.Writes[1]);
        Assert.Equal(new byte[] { 0xA5, 128 }, transport.Writes[2]);
        Assert.Equal(new byte[] { 0xA6, (byte)'f', (byte)'u', (byte)'n' }, transport.Writes[3]);
    }

    [Fact]
    public async Task MidStreamWriteFailure_FailsTheAwaitingCommand()
    {
        var transport = new FakeBleTransport { FailWrites = true };
        using var driver = new SP621EDriver(12, transport, new FakeSp621eWireFormat());
        await driver.ConnectAsync();

        await Assert.ThrowsAsync<IOException>(async () =>
            await driver.SendFrameAsync(Frame(new Rgb(1, 1, 1))).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DeviceDataReceived_IsForwardedForDiagnostics()
    {
        var transport = new FakeBleTransport();
        using var driver = new SP621EDriver(12, transport, new FakeSp621eWireFormat());
        byte[]? received = null;
        driver.DeviceDataReceived += (_, data) => received = data.ToArray();

        transport.RaiseData([0x04, 0xFF]);

        Assert.Equal(new byte[] { 0x04, 0xFF }, received);
    }

    [Fact]
    public async Task SendBeforeConnect_ThrowsClearError()
    {
        var transport = new FakeBleTransport();
        using var driver = new SP621EDriver(12, transport, new FakeSp621eWireFormat());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.SendFrameAsync(Frame(new Rgb(0, 0, 0))));
        Assert.Contains("not connected", ex.Message);
    }
}