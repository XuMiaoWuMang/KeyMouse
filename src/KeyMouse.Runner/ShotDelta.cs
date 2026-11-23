using Frame = KeyMouse.Capture.NativeCapture.Frame;

namespace KeyMouse.Runner;

/// <summary>A v2 subscriber can opt into pixels from shots it already requested. Consume the capture
/// directly, never reopen a path supplied by the client. Omit oversized frames rather than retaining them.</summary>
internal sealed class ShotDelta
{
    internal sealed record Patch(int Frame, int? BaseFrame, int Width, int Height, int X, int Y, int W, int H, byte[] Bgra);
    private Frame? _last;
    private int _base;
    internal Patch? Next(int index, Frame frame)
    {
        if (frame.Bgra.Length > 16 * 1024 * 1024) { _last = null; return null; }
        var last = _last;
        bool full = last == null || frame.Width != last.Width || frame.Height != last.Height;
        int left = 0, top = 0, right = frame.Width, bottom = frame.Height;
        if (!full)
        {
            left = right; top = bottom; right = bottom = 0;
            int stride = frame.Width * 4;
            for (int y = 0; y < frame.Height; y++)
            {
                var a = frame.Bgra.AsSpan(y * stride, stride);
                var b = last!.Bgra.AsSpan(y * stride, stride);
                if (a.SequenceEqual(b)) continue;
                top = Math.Min(top, y); bottom = y + 1;
                int x = 0, end = frame.Width;
                while (a.Slice(x * 4, 4).SequenceEqual(b.Slice(x * 4, 4))) x++;
                while (a.Slice((end - 1) * 4, 4).SequenceEqual(b.Slice((end - 1) * 4, 4))) end--;
                left = Math.Min(left, x); right = Math.Max(right, end);
            }
            if (right == 0) return null;
        }
        var patch = new Patch(index, full ? null : _base, frame.Width, frame.Height, left, top, right - left, bottom - top,
            full ? frame.Bgra : frame.Crop(left, top, right - left, bottom - top).Bgra);
        _last = frame; _base = index;
        return patch;
    }
}
