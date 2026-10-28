using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse.Capture;

/// <summary>
/// 像素：抓取区域、对比度归一化、放大与白边、写出 32 位 BMP 给引擎读。
/// </summary>
internal static partial class Probe
{

    internal static NativeCapture.Frame CaptureRegion(
        WindowInfo window, bool windowSpace, (int X, int Y, int W, int H) region, string capture)
    {
        NativeWindow.GetWindowRect(window.Handle, out NativeWindow.RECT windowRect);
        int fullWidth = windowRect.Right - windowRect.Left;
        int fullHeight = windowRect.Bottom - windowRect.Top;
        var client = new NativeWindow.POINT { X = 0, Y = 0 };
        if (!NativeWindow.ClientToScreen(window.Handle, ref client))
            throw new CommandFailure(4, $"「{window.Title}」的客户区原点读不出来——未读取任何内容");

        if (capture == "print")
        {
            // PrintWindow paints the window itself, so a client-relative region has to be shifted by
            // the client origin inside the window rectangle. (Window space is rejected earlier for
            // this path: the painted geometry is inset and does not line up on this machine.)
            int offsetX = client.X - windowRect.Left;
            int offsetY = client.Y - windowRect.Top;
            var windowFrame = NativeCapture.TryCapture(window.Handle, fullWidth, fullHeight)
                ?? throw new CommandFailure(ExitUnreadable,
                    $"「{window.Title}」拒绝为读取而绘制（最小化、被挂起或只剩一帧陈旧画面）——未读取到任何内容");
            return windowFrame.Crop(offsetX + region.X, offsetY + region.Y, region.W, region.H);
        }

        int originX = windowSpace ? windowRect.Left : client.X;
        int originY = windowSpace ? windowRect.Top : client.Y;
        return NativeCapture.TryCaptureScreen(originX + region.X, originY + region.Y, region.W, region.H)
            ?? throw new CommandFailure(ExitUnreadable,
                $"屏幕区域 {originX + region.X},{originY + region.Y} {region.W}x{region.H} 抓取失败——未读取到任何内容");
    }

    /// <summary>
    /// Reads one region once and hands back what the engine saw - text, confidence and the boxes -
    /// for callers that want a value rather than a report: the `wait-text` predicate, and the steps
    /// that act on what they saw (`click-text`). It deliberately runs the same capture, preprocessing
    /// and engine call as the CLI, so "what the step saw" and "what `probe` prints" cannot drift.
    /// </summary>
    internal static NativeCapture.Frame Normalize(NativeCapture.Frame frame)
    {
        var histogram = new int[256];
        for (int i = 0; i < frame.Bgra.Length; i += 4)
        {
            int luma = (frame.Bgra[i] * 29 + frame.Bgra[i + 1] * 150 + frame.Bgra[i + 2] * 77) >> 8;
            histogram[luma]++;
        }

        int background = 0;
        for (int level = 1; level < 256; level++)
        {
            if (histogram[level] > histogram[background]) background = level;
        }

        // A blank region has nothing to normalise, and stretching it would only invent structure.
        // Tested rather than assumed: the previous guard compared the window size, which the
        // constant-size window can never trip, so a uniform grey region was being mapped to white.
        int nearBackground = 0;
        for (int level = Math.Max(0, background - 6); level <= Math.Min(255, background + 6); level++)
        {
            nearBackground += histogram[level];
        }
        int total = frame.Width * frame.Height;
        if (total - nearBackground < Math.Max(1, total / 1000)) return frame;

        bool darkOnLight = background >= 128;
        int low = darkOnLight ? background - 64 : background;
        int high = darkOnLight ? background : background + 64;
        if (high - low < 8) return frame;

        double scale = 255.0 / (high - low);
        var pixels = new byte[frame.Bgra.Length];
        for (int i = 0; i < frame.Bgra.Length; i += 4)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                double value = (frame.Bgra[i + channel] - low) * scale;
                value = Math.Clamp(value, 0, 255);
                pixels[i + channel] = (byte)(darkOnLight ? value + 0.5 : 255 - value + 0.5);
            }
            pixels[i + 3] = 255;
        }
        return new NativeCapture.Frame(frame.Width, frame.Height, pixels);
    }

    /// <summary>
    /// Upscale plus an optional white border, both opt-in: the default path hands the engine the
    /// pixels exactly as captured, so anything this does to them is something the caller asked for.
    ///
    /// Kept because it was measured to help on hard samples (three of four failing real samples
    /// were rescued by 3x upscaling) even though the current corpus cannot tell the two apart
    /// (10 samples: raw 9/10, upscaled 9~10/10 - see tests/evidence).
    ///
    /// Two samplers: `nearest` replicates each source pixel into a scale x scale block and invents
    /// nothing; `bilinear` interpolates, which measured one sample better on the current corpus.
    /// </summary>
    internal static NativeCapture.Frame Preprocess(
        NativeCapture.Frame source, int scale, int pad, bool nearest = false)
    {
        int scaledWidth = source.Width * scale;
        int scaledHeight = source.Height * scale;
        int width = scaledWidth + 2 * pad;
        int height = scaledHeight + 2 * pad;
        var pixels = new byte[width * height * 4];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xFF; pixels[i + 1] = 0xFF; pixels[i + 2] = 0xFF; pixels[i + 3] = 0xFF;
        }

        for (int y = 0; y < scaledHeight; y++)
        {
            int nearestY = Math.Min(source.Height - 1, y / scale);
            double sourceY = (y + 0.5) / scale - 0.5;
            int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, source.Height - 1);
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            double fy = Math.Clamp(sourceY - y0, 0, 1);

            for (int x = 0; x < scaledWidth; x++)
            {
                int destination = ((y + pad) * width + (x + pad)) * 4;

                if (nearest)
                {
                    int nearestX = Math.Min(source.Width - 1, x / scale);
                    int source4 = (nearestY * source.Width + nearestX) * 4;
                    pixels[destination] = source.Bgra[source4];
                    pixels[destination + 1] = source.Bgra[source4 + 1];
                    pixels[destination + 2] = source.Bgra[source4 + 2];
                    continue;
                }

                double sourceX = (x + 0.5) / scale - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                double fx = Math.Clamp(sourceX - x0, 0, 1);

                for (int channel = 0; channel < 3; channel++)
                {
                    int i00 = (y0 * source.Width + x0) * 4 + channel;
                    int i10 = (y0 * source.Width + x1) * 4 + channel;
                    int i01 = (y1 * source.Width + x0) * 4 + channel;
                    int i11 = (y1 * source.Width + x1) * 4 + channel;

                    double top = source.Bgra[i00] * (1 - fx) + source.Bgra[i10] * fx;
                    double bottom = source.Bgra[i01] * (1 - fx) + source.Bgra[i11] * fx;
                    pixels[destination + channel] = (byte)Math.Clamp(top * (1 - fy) + bottom * fy + 0.5, 0, 255);
                }
            }
        }

        return new NativeCapture.Frame(width, height, pixels);
    }

    /// <summary>
    /// Writes a 32-bit top-down BMP. BMP rather than PNG on purpose: the encoder is 40 lines of
    /// header instead of a dependency, and the engine reads BMP natively.
    /// </summary>
    internal static void WriteBmp(string path, NativeCapture.Frame frame)
    {
        int stride = frame.Width * 4;
        int imageSize = stride * frame.Height;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(14 + 40 + imageSize);
        writer.Write(0);
        writer.Write(14 + 40);

        writer.Write(40);
        writer.Write(frame.Width);
        writer.Write(-frame.Height); // negative height = top-down rows
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);             // BI_RGB
        writer.Write(imageSize);
        writer.Write(2835);          // 72 DPI, in pixels per metre
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(frame.Bgra);
    }

    /// <summary>
    /// Runs the external engine over one image and parses its tsv.
    ///
    /// The engine is an explicit, replaceable dependency - not a bundled model. What it is and
    /// which model files it used go into the output, because "why did this work yesterday" has
    /// no answer otherwise.
    /// </summary>
}

