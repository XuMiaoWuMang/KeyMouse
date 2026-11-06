using KeyMouse.Capture;

namespace KeyMouse.Recognition.Ocr;

/// <summary>
/// Pixels in, float tensors out. No OpenCV: the four operations PaddleOCR actually needs are
/// resize, crop, normalize and "lay it out CHW", and each is a few lines over a BGRA byte array.
///
/// Everything here is deliberately allocation-visible: a read is 10-30 ms of work and the buffers
/// are small, so clarity beats pooling. If profiling ever says otherwise, this is the file to
/// revisit - not the call sites.
/// </summary>
internal static class OcrImage
{
    /// <summary>Bilinear resize of a BGRA frame. Used by det (whole picture) and rec (one line).</summary>
    internal static byte[] Resize(byte[] bgra, int width, int height, int targetWidth, int targetHeight)
    {
        var output = new byte[targetWidth * targetHeight * 4];
        double scaleX = (double)width / targetWidth;
        double scaleY = (double)height / targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            double sourceY = (y + 0.5) * scaleY - 0.5;
            int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, height - 1);
            int y1 = Math.Min(y0 + 1, height - 1);
            double fy = Math.Clamp(sourceY - y0, 0, 1);

            for (int x = 0; x < targetWidth; x++)
            {
                double sourceX = (x + 0.5) * scaleX - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, width - 1);
                int x1 = Math.Min(x0 + 1, width - 1);
                double fx = Math.Clamp(sourceX - x0, 0, 1);

                int i00 = (y0 * width + x0) * 4;
                int i10 = (y0 * width + x1) * 4;
                int i01 = (y1 * width + x0) * 4;
                int i11 = (y1 * width + x1) * 4;
                int target = (y * targetWidth + x) * 4;

                for (int c = 0; c < 4; c++)
                {
                    double top = bgra[i00 + c] * (1 - fx) + bgra[i10 + c] * fx;
                    double bottom = bgra[i01 + c] * (1 - fx) + bgra[i11 + c] * fx;
                    output[target + c] = (byte)Math.Clamp(Math.Round(top * (1 - fy) + bottom * fy), 0, 255);
                }
            }
        }

        return output;
    }

    /// <summary>Copies a rectangle out, clamping to the frame. Boxes come from a network and may hang over an edge.</summary>
    internal static byte[] Crop(
        byte[] bgra, int width, int height, (int X, int Y, int W, int H) box, out int croppedWidth, out int croppedHeight)
    {
        int x = Math.Clamp(box.X, 0, Math.Max(0, width - 1));
        int y = Math.Clamp(box.Y, 0, Math.Max(0, height - 1));
        int w = Math.Clamp(box.W, 1, width - x);
        int h = Math.Clamp(box.H, 1, height - y);

        croppedWidth = w;
        croppedHeight = h;
        var output = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
        {
            Buffer.BlockCopy(bgra, ((y + row) * width + x) * 4, output, row * w * 4, w * 4);
        }
        return output;
    }

    /// <summary>
    /// RGBA-in-0..1 CHW tensor, the layout both PaddleOCR models want.
    ///
    /// `mean`/`std` are per-channel, in BGR order, exactly as the Python preprocessing does it:
    /// `(pixel/255 - mean) / std`. The tensors are `[1, 3, H, W]`.
    /// </summary>
    internal static float[] ToTensor(
        byte[] bgra, int width, int height, float[] mean, float[] std, int paddedWidth = 0)
    {
        int tensorWidth = paddedWidth > 0 ? paddedWidth : width;
        var tensor = new float[3 * height * tensorWidth];
        int plane = height * tensorWidth;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int source = (y * width + x) * 4;
                int target = y * tensorWidth + x;
                // BGRA in memory; the models were trained on RGB.
                tensor[target] = (bgra[source + 2] / 255f - mean[0]) / std[0];
                tensor[plane + target] = (bgra[source + 1] / 255f - mean[1]) / std[1];
                tensor[2 * plane + target] = (bgra[source] / 255f - mean[2]) / std[2];
            }
        }

        return tensor;
    }

    /// <summary>Largest scale that fits the long side into `limit`, per PaddleOCR's "max" rule.</summary>
    internal static double ScaleForMax(int width, int height, int limit)
    {
        int longest = Math.Max(width, height);
        return longest <= limit ? 1.0 : (double)limit / longest;
    }

    /// <summary>Rounds up to a multiple of 32; det needs it because the backbone downsamples 32×.</summary>
    internal static int AlignUp(int value, int multiple) =>
        Math.Max(multiple, (int)Math.Round((double)value / multiple) * multiple);
}
