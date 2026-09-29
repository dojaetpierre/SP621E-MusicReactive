namespace VirtualSp621e;

using SP621E.Bluetooth.Wire;
using SP621E.Core.LedFrame;

/// <summary>
/// A synthetic wire encoder for the <c>VirtualSp621e</c> simulator only.
///
/// The bytes in here are INVENTED for exercising the driver pipeline (framing,
/// chunking, pacing, parametric commands) with zero hardware. It is explicitly
/// NOT the SP621E protocol: it must never be used against a real device and must
/// never be promoted to evidence. Tagged VIRTUAL in <see cref="EvidenceReference"/>.
/// </summary>
public sealed class VirtualSp621eWireFormat : ISp621eWireFormat
{
    private readonly int _pixelCount;

    public VirtualSp621eWireFormat(int pixelCount) => _pixelCount = pixelCount;

    public string EvidenceReference => "VIRTUAL TESTER (tools/VirtualSp621e) — invented bytes, NOT SP621E protocol evidence";

    public bool SupportsFrameStreaming => true;

    public bool SupportsParametric => true;

    public int? PixelBufferSize => _pixelCount;

    public IEnumerable<ReadOnlyMemory<byte>> EncodeFrame(RgbFrame frame)
    {
        // Header packet, then the pixel payload as one packet (the driver chunks it
        // to the transport MTU — that is exactly what we want to exercise).
        yield return new byte[] { 0xF0, (byte)frame.PixelCount };
        var payload = new byte[1 + frame.Pixels.Count * 3];
        payload[0] = 0xF1;
        var i = 1;
        foreach (var p in frame.Pixels)
        {
            payload[i++] = p.R;
            payload[i++] = p.G;
            payload[i++] = p.B;
        }
        yield return payload;
    }

    public IEnumerable<ReadOnlyMemory<byte>> EncodePower(bool on) => [new byte[] { 0x20, on ? (byte)0x01 : (byte)0x00 }];

    public IEnumerable<ReadOnlyMemory<byte>> EncodeColor(Rgb color) => [new byte[] { 0x21, color.R, color.G, color.B }];

    public IEnumerable<ReadOnlyMemory<byte>> EncodeBrightness(double brightness)
    {
        var clamped = Math.Clamp(brightness, 0.0, 1.0);
        return [new byte[] { 0x22, (byte)Math.Round(clamped * 255.0) }];
    }

    public IEnumerable<ReadOnlyMemory<byte>> EncodeEffect(string effectName)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(effectName);
        var packet = new byte[payload.Length + 1];
        packet[0] = 0x23;
        Array.Copy(payload, 0, packet, 1, payload.Length);
        return [packet];
    }
}