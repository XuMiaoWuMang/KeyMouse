namespace KeyMouse.Recognition.Ocr;

/// <summary>
/// DB (differentiable binarization) post-processing: a probability map in, text boxes out.
///
/// The reference implementation runs OpenCV's `findContours` and a polygon unclip. We have no
/// OpenCV, so this does the same job with a flood fill over the binarized map: every connected
/// region becomes one box, the region's mean probability becomes its score, and the box is grown by
/// the unclip rule (area × ratio / perimeter) to recover the pixels the network's shrink lost.
///
/// It is not a pixel-for-pixel port. What matters for us is "which lines of text are where", and
/// axis-aligned boxes around connected text do that. Rotated boxes would need the polygon crop and
/// are not worth the code until a screenshot proves otherwise.
/// </summary>
internal static class DbPostProcess
{
    internal sealed record Box(int X, int Y, int Width, int Height, double Score);

    /// <summary>The knobs that matter, with PaddleOCR's defaults for this model family.</summary>
    internal sealed record Options(
        double Threshold = 0.3,
        double BoxThreshold = 0.6,
        double UnclipRatio = 1.5,
        int MinSize = 3,
        int MaxCandidates = 1000)
    {
        internal static Options Default { get; } = new();
    }

    /// <summary>
    /// Turns a `[1, 1, H, W]` probability map into boxes scaled back to the source picture.
    /// `scale` is source/target, because det runs on a resized copy.
    /// </summary>
    internal static List<Box> Extract(
        float[] probability, int mapWidth, int mapHeight, double scaleX, double scaleY, Options options)
    {
        var visited = new bool[mapWidth * mapHeight];
        var boxes = new List<Box>();
        var stack = new Stack<(int X, int Y)>();

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int start = y * mapWidth + x;
                if (visited[start] || probability[start] < options.Threshold) continue;

                int left = x, right = x, top = y, bottom = y;
                double sum = 0;
                int count = 0;

                visited[start] = true;
                stack.Push((x, y));

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    int index = cy * mapWidth + cx;
                    sum += probability[index];
                    count++;
                    if (cx < left) left = cx;
                    if (cx > right) right = cx;
                    if (cy < top) top = cy;
                    if (cy > bottom) bottom = cy;

                    // 4-neighbourhood: 8 would merge two lines through a single diagonal pixel.
                    Push(cx - 1, cy); Push(cx + 1, cy); Push(cx, cy - 1); Push(cx, cy + 1);
                }

                void Push(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= mapWidth || ny >= mapHeight) return;
                    int next = ny * mapWidth + nx;
                    if (visited[next] || probability[next] < options.Threshold) return;
                    visited[next] = true;
                    stack.Push((nx, ny));
                }

                int boxWidth = right - left + 1;
                int boxHeight = bottom - top + 1;
                if (boxWidth < options.MinSize || boxHeight < options.MinSize) continue;

                double score = sum / count;
                if (score < options.BoxThreshold) continue;

                // Unclip: distance = area × ratio / perimeter, applied to all four sides.
                double area = boxWidth * (double)boxHeight;
                double perimeter = 2 * (boxWidth + (double)boxHeight);
                double distance = perimeter > 0 ? area * options.UnclipRatio / perimeter : 0;

                int expandedX = (int)Math.Round((left - distance) * scaleX);
                int expandedY = (int)Math.Round((top - distance) * scaleY);
                int expandedW = (int)Math.Round((boxWidth + 2 * distance) * scaleX);
                int expandedH = (int)Math.Round((boxHeight + 2 * distance) * scaleY);

                boxes.Add(new Box(expandedX, expandedY, Math.Max(1, expandedW), Math.Max(1, expandedH), score));
                if (boxes.Count >= options.MaxCandidates) return Sort(boxes);
            }
        }

        return Sort(boxes);
    }

    /// <summary>
    /// Reading order: top to bottom, then left to right, with "same row" tolerance so a line broken
    /// into pieces does not interleave with the line above it.
    /// </summary>
    internal static List<Box> Sort(List<Box> boxes)
    {
        boxes.Sort((a, b) =>
        {
            int tolerance = Math.Max(6, Math.Min(a.Height, b.Height) / 2);
            if (Math.Abs(a.Y - b.Y) > tolerance) return a.Y.CompareTo(b.Y);
            return a.X.CompareTo(b.X);
        });
        return boxes;
    }
}
