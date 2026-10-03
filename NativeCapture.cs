using System.Runtime.InteropServices;

namespace KeyMouse;

/// <summary>
/// Renders a window into pixels through PrintWindow.
///
/// Why not a screen grab: PrintWindow asks the window to paint itself, so the result
/// carries no text caret (measured: a screen grab picks the caret up and OCR reads it
/// as "|") and it works while the window sits in the background (a screen grab needs it
/// unobscured). A minimized or hung window may still refuse to paint, and a refusal is
/// reported as "could not read" - never retried as a screen grab, because that would be
/// silent guessing.
/// </summary>
internal static class NativeCapture
{
    // PW_RENDERFULLCONTENT: required for DirectComposition content (windowed UWP,
    // Chromium surfaces). Without it those windows just print blank.
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint flags);

    /// <summary>Top-down 32-bit BGRA pixels of a whole window.</summary>
    internal sealed class Frame
    {
        internal Frame(int width, int height, byte[] bgra)
        {
            Width = width;
            Height = height;
            Bgra = bgra;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal byte[] Bgra { get; }

        /// <summary>Copies a sub-rectangle out. Callers have already validated the bounds.</summary>
        internal Frame Crop(int x, int y, int width, int height)
        {
            var pixels = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                int source = ((y + row) * Width + x) * 4;
                Buffer.BlockCopy(Bgra, source, pixels, row * width * 4, width * 4);
            }
            return new Frame(width, height, pixels);
        }
    }

    /// <summary>
    /// Copies a screen rectangle straight out of the display.
    ///
    /// This is the default because it is the path that was actually verified to read text back:
    /// the corpus measurements (type into a window, read the region, compare) used a screen grab and
    /// read every sample. PrintWindow is kept as an option, but its geometry does not line up with
    /// either GetWindowRect or the DWM visible frame on this system - captured text came back with its
    /// top sliced off by the invisible resize border, and that is not a bug worth shipping as default.
    ///
    /// The costs are real and documented instead of hidden: the window must be on screen and
    /// unobscured, and a text caret becomes part of the pixels (~one extra character, which the
    /// caller's matching budget and the consensus rule are there to absorb).
    /// </summary>
    internal static Frame? TryCaptureScreen(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;

        IntPtr screenDc = IntPtr.Zero, memoryDc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return null;

            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) return null;

            var header = new BITMAPINFOHEADER
            {
                Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
                SizeImage = (uint)(width * height * 4),
            };

            dib = CreateDIBSection(memoryDc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) return null;

            previous = SelectObject(memoryDc, dib);
            // CAPTUREBLT includes layered windows, which is what makes this work for windows that
            // composite rather than paint into a visible surface.
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, SRCCOPY | CAPTUREBLT)) return null;

            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new Frame(width, height, pixels);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero) SelectObject(memoryDc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
            if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Paints the window and returns its pixels, or null when it refuses to paint.
    /// </summary>
    internal static Frame? TryCapture(IntPtr hwnd, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;

        IntPtr memoryDc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(IntPtr.Zero);
            if (memoryDc == IntPtr.Zero) return null;

            // Negative height asks for a top-down bitmap, so row 0 is the top row and no
            // vertical flip is needed afterwards.
            var header = new BITMAPINFOHEADER
            {
                Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = 0, // BI_RGB
                SizeImage = (uint)(width * height * 4),
            };

            dib = CreateDIBSection(memoryDc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) return null;

            previous = SelectObject(memoryDc, dib);
            if (!PrintWindow(hwnd, memoryDc, PW_RENDERFULLCONTENT)) return null;

            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new Frame(width, height, pixels);
        }
        catch (Exception)
        {
            // A window that dies mid-capture is a read failure, not a crash.
            return null;
        }
        finally
        {
            if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero) SelectObject(memoryDc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
        }
    }
}
