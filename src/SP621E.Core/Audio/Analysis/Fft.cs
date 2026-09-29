namespace SP621E.Core.Audio.Analysis;

using System.Numerics;

/// <summary>
/// Iterative radix-2 FFT (Cooley-Tukey, bit-reversal + butterfly stages).
/// Input/output are <see cref="Complex"/> arrays whose length must be a power of two.
/// </summary>
internal static class Fft
{
    public static void Transform(Span<Complex> data)
    {
        var n = data.Length;
        if (n < 2 || (n & (n - 1)) != 0)
            throw new ArgumentException("FFT requires a power-of-two length.", nameof(data));
        TransformBody(data);
    }

    private static void TransformBody(Span<Complex> data)
    {
        var n = data.Length;
        var width = Log2(n);
        for (var i = 1; i < n; i++)
        {
            var j = BitReverse(i, width);
            if (j > i)
                (data[i], data[j]) = (data[j], data[i]);
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2.0 * Math.PI / length;
            var wLength = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (var i = 0; i < n; i += length)
            {
                var w = Complex.One;
                for (var k = 0; k < length / 2; k++)
                {
                    var u = data[i + k];
                    var v = data[i + k + length / 2] * w;
                    data[i + k] = u + v;
                    data[i + k + length / 2] = u - v;
                    w *= wLength;
                }
            }
        }
    }

    private static int BitReverse(int value, int width)
    {
        var result = 0;
        for (var i = 0; i < width; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    public static int Log2(int n)
    {
        var bits = 0;
        while (n > 1)
        {
            n >>= 1;
            bits++;
        }
        return bits;
    }
}