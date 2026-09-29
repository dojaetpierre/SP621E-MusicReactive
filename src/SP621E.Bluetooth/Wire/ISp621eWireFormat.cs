namespace SP621E.Bluetooth.Wire;

using SP621E.Core.LedFrame;

/// <summary>
/// An evidence-backed encoder that turns high-level intents (frames, parametric
/// commands) into the concrete ATT payload bytes accepted by the device.
///
/// The encoder is the ONLY place protocol bytes exist, and it must be constructed
/// from Milestone-3 HCI capture evidence — never from guesswork. `EvidenceReference`
/// names the CONFIRMED source in HARDWARE_PROTOCOL.md that justifies the bytes.
/// The non-streaming path (if supported) replaces a vanishingly small encoder used
/// only when the effects engine runs in device mode.
/// </summary>
public interface ISp621eWireFormat
{
    /// <summary>Pointer to the evidence that justifies these bytes (e.g. "HARDWARE_PROTOCOL.md §5 R#3, CONFIRMED 2026-09-21").</summary>
    string EvidenceReference { get; }

    bool SupportsFrameStreaming { get; }

    bool SupportsParametric { get; }

    /// <summary>Pixel address space of the device (0/unknown if not streaming), used for frame matching.</summary>
    int? PixelBufferSize { get; }

    /// <summary>Encode a rendered frame as one or more write packets (each ≤ transport MaxPayloadBytes).</summary>
    IEnumerable<ReadOnlyMemory<byte>> EncodeFrame(RgbFrame frame);

    /// <summary>Encode a power command (only when <see cref="SupportsParametric"/>).</summary>
    IEnumerable<ReadOnlyMemory<byte>> EncodePower(bool on);

    /// <summary>Encode a solid-color command (only when <see cref="SupportsParametric"/>).</summary>
    IEnumerable<ReadOnlyMemory<byte>> EncodeColor(Rgb color);

    /// <summary>Encode a brightness command, value in [0,1] (only when <see cref="SupportsParametric"/>).</summary>
    IEnumerable<ReadOnlyMemory<byte>> EncodeBrightness(double brightness);

    /// <summary>Encode an effect-select command (only when <see cref="SupportsParametric"/>).</summary>
    IEnumerable<ReadOnlyMemory<byte>> EncodeEffect(string effectName);
}