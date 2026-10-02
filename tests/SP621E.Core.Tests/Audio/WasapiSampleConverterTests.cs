namespace SP621E.Core.Tests.Audio;

using System.Buffers.Binary;
using NAudio.Wave;
using SP621E.Audio.Capture;

public sealed class WasapiSampleConverterTests
{
    [Fact]
    public void CreateFor_16BitPcm_ReturnsInt16Converter()
    {
        var converter = WasapiSampleConverter.CreateFor(new WaveFormat(48000, 16, 2));
        Assert.NotNull(converter);

        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, 0);          //  0.0
        BinaryPrimitives.WriteInt16LittleEndian(buffer[2..], short.MaxValue); // ~1.0
        BinaryPrimitives.WriteInt16LittleEndian(buffer[4..], short.MinValue); // -1.0
        BinaryPrimitives.WriteInt16LittleEndian(buffer[6..], (short)16384);   //  0.5

        var samples = converter(buffer);
        Assert.Equal(4, samples.Length);
        Assert.Equal(0f, samples[0], 3);
        Assert.Equal(1f, samples[1], 3);
        Assert.Equal(-1f, samples[2], 3);
        Assert.Equal(0.5f, samples[3], 3);
    }

    [Fact]
    public void CreateFor_32BitFloat_ReturnsFloatConverter()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        var converter = WasapiSampleConverter.CreateFor(format);
        Assert.NotNull(converter);

        float[] values = [0.5f, -0.25f, 1.0f, 0.0f];
        Span<byte> buffer = stackalloc byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(i * 4)..], BitConverter.SingleToInt32Bits(values[i]));

        var samples = converter(buffer);
        Assert.Equal(values.Length, samples.Length);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], samples[i], 5);
    }

    [Fact]
    public void CreateFor_32BitExtensible_ReturnsFloatConverter()
    {
        var format = new WaveFormatExtensible(48000, 32, 2);
        var converter = WasapiSampleConverter.CreateFor(format);
        Assert.NotNull(converter);

        float[] values = [-1.0f, 0.25f, 0.75f];
        Span<byte> buffer = stackalloc byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(i * 4)..], BitConverter.SingleToInt32Bits(values[i]));

        var samples = converter(buffer);
        Assert.Equal(values.Length, samples.Length);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], samples[i], 5);
    }

    [Fact]
    public void CreateFor_24Bit_ReturnsNull()
    {
        Assert.Null(WasapiSampleConverter.CreateFor(new WaveFormat(48000, 24, 2)));
    }
}