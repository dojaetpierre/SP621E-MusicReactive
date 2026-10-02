namespace SP621E.Audio.Capture;

using System.Buffers.Binary;
using NAudio.Wave;

/// <summary>
/// Converts raw WASAPI endpoint buffer bytes into normalized float samples, frame-major
/// interleaved ([f0c0, f0c1, f1c0, f1c1, ...]) in [-1, 1]. Handles 16-bit PCM and 32-bit
/// IEEE float (both native and WAVE_FORMAT_EXTENSIBLE subchannel layouts).
/// </summary>
internal static class WasapiSampleConverter
{
    public static Func<ReadOnlySpan<byte>, float[]>? CreateFor(WaveFormat format)
    {
        var bytes = format.BitsPerSample / 8;
        var isFloat = bytes == 4 && (format.Encoding == WaveFormatEncoding.IeeeFloat ||
                                     format.Encoding == WaveFormatEncoding.Extensible);
        var isInt16 = bytes == 2;
        if (isFloat) return ToFloat;
        if (isInt16) return ToInt16;
        return null;
    }

    private static float[] ToFloat(ReadOnlySpan<byte> buffer)
    {
        var count = buffer.Length / 4;
        var result = new float[count];
        for (var i = 0; i < count; i++)
            result[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(i * 4, 4)));
        return result;
    }

    private static float[] ToInt16(ReadOnlySpan<byte> buffer)
    {
        var count = buffer.Length / 2;
        var result = new float[count];
        for (var i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(i * 2, 2)) / 32768f;
        return result;
    }
}