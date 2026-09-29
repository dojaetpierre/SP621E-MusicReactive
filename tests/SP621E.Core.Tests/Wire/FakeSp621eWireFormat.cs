namespace SP621E.Bluetooth.Tests.Wire;

using SP621E.Bluetooth.Wire;
using SP621E.Core.LedFrame;

/// <summary>
/// Deterministic, evidence-tagged encoder used to prove the driver pipeline
/// ("test probe": prefix + payload, no real protocol semantics).
/// </summary>
public sealed class FakeSp621eWireFormat : ISp621eWireFormat
{
    public string EvidenceReference => "UNIT-TEST-PROBE";

    public bool SupportsFrameStreaming { get; set; } = true;

    public bool SupportsParametric { get; set; } = true;

    public int? PixelBufferSize { get; set; } = 12;

    public IEnumerable<ReadOnlyMemory<byte>> EncodeFrame(RgbFrame frame)
    {
        yield return new byte[] { 0xA1, (byte)frame.PixelCount };
        var all = new byte[1 + frame.Pixels.Count * 3];
        all[0] = 0xA2;
        var i = 1;
        foreach (var p in frame.Pixels)
        {
            all[i++] = p.R;
            all[i++] = p.G;
            all[i++] = p.B;
        }
        yield return all;
    }

    public IEnumerable<ReadOnlyMemory<byte>> EncodePower(bool on) => [new byte[] { 0xA3, on ? (byte)1 : (byte)0 }];

    public IEnumerable<ReadOnlyMemory<byte>> EncodeColor(Rgb color) => [new byte[] { 0xA4, color.R, color.G, color.B }];

    public IEnumerable<ReadOnlyMemory<byte>> EncodeBrightness(double brightness)
    {
        var clamped = Math.Clamp(brightness, 0.0, 1.0);
        return [new byte[] { 0xA5, (byte)Math.Round(clamped * 255.0) }];
    }

    public IEnumerable<ReadOnlyMemory<byte>> EncodeEffect(string effectName)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(effectName);
        var packet = new byte[payload.Length + 1];
        packet[0] = 0xA6;
        Array.Copy(payload, 0, packet, 1, payload.Length);
        return [packet];
    }
}