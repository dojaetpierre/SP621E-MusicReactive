namespace SP621E.Screen;

using System.Runtime.InteropServices;
using SP621E.Core.LedFrame;

/// <summary>
/// Minimal GDI screen grab: stretches the virtual desktop (all monitors) into a small
/// 32bpp top-down DIB in one <c>StretchBlt</c>, so a 4K display becomes a ~96x54 cell
/// buffer in a few milliseconds. Pure P/Invoke — no System.Drawing dependency.
/// </summary>
internal static class NativeScreen
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private const uint SrcCopy = 0x00CC0020;
    private const uint DibRgbColors = 0;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hDc, ref BitmapInfo pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObj);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(IntPtr hDcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hDcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool GdiFlush();

    /// <summary>
    /// Captures the virtual desktop scaled to <paramref name="targetWidth"/> cells wide.
    /// Returns null when the desktop bounds are unusable (no session, zero-size screen).
    /// </summary>
    public static ScreenSnapshot? Capture(int targetWidth, DateTimeOffset timestamp)
    {
        if (targetWidth <= 0)
            return null;

        var vx = GetSystemMetrics(SmXVirtualScreen);
        var vy = GetSystemMetrics(SmYVirtualScreen);
        var vw = GetSystemMetrics(SmCxVirtualScreen);
        var vh = GetSystemMetrics(SmCyVirtualScreen);
        if (vw <= 0 || vh <= 0)
            return null;

        var tw = targetWidth;
        var th = Math.Max(1, (int)Math.Round(targetWidth * (double)vh / vw));

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            return null;

        IntPtr hdcMem = IntPtr.Zero, dib = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hdcMem = CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
                return null;

            var bmi = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = 40,
                    Width = tw,
                    Height = -th, // top-down
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                },
            };

            dib = CreateDIBSection(hdcMem, ref bmi, DibRgbColors, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero)
                return null;

            old = SelectObject(hdcMem, dib);
            if (old == IntPtr.Zero)
                return null;

            if (!StretchBlt(hdcMem, 0, 0, tw, th, hdcScreen, vx, vy, vw, vh, SrcCopy))
                return null;
            GdiFlush();

            var bytes = new byte[tw * th * 4];
            Marshal.Copy(bits, bytes, 0, bytes.Length);
            return ToSnapshot(bytes, tw, th, timestamp);
        }
        finally
        {
            if (old != IntPtr.Zero && hdcMem != IntPtr.Zero)
                SelectObject(hdcMem, old);
            if (dib != IntPtr.Zero)
                DeleteObject(dib);
            if (hdcMem != IntPtr.Zero)
                DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    /// <summary>Turns a raw BGRA top-down buffer into a snapshot with a computed average.</summary>
    private static ScreenSnapshot ToSnapshot(byte[] bgra, int width, int height, DateTimeOffset timestamp)
    {
        var pixels = new Rgb[width * height];
        long rSum = 0, gSum = 0, bSum = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            var o = i * 4;
            var b = bgra[o];
            var g = bgra[o + 1];
            var r = bgra[o + 2];
            rSum += r;
            gSum += g;
            bSum += b;
            pixels[i] = new Rgb(r, g, b);
        }

        var n = pixels.Length;
        return new ScreenSnapshot
        {
            Timestamp = timestamp,
            Width = width,
            Height = height,
            Pixels = pixels,
            Average = new Rgb(
                (byte)(rSum / n),
                (byte)(gSum / n),
                (byte)(bSum / n)),
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }
}