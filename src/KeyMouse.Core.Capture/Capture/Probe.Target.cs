using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse.Capture;

/// <summary>
/// 目标窗口与区域：把选择器落成可用窗口、校验区域的坐标系、把命中结果变成可点击的点。
/// </summary>
internal static partial class Probe
{

    private static FindReference DescribeFind(
        TextMatch? match, string expected, string mode, int maxErrors, WindowInfo window, bool windowSpace,
        (int X, int Y, int W, int H) region)
    {
        if (match is null) return new FindReference(expected, false, mode, maxErrors, null, null, null, null, null);

        int centreX = region.X + match.Rect[0] + match.Rect[2] / 2;
        int centreY = region.Y + match.Rect[1] + match.Rect[3] / 2;
        if (windowSpace)
        {
            var client = new NativeWindow.POINT { X = 0, Y = 0 };
            var rect = WindowBounds(window, windowSpace: true);
            if (!NativeWindow.ClientToScreen(window.Handle, ref client))
                throw new CommandFailure(4, $"「{window.Title}」的客户区原点读不出来——无法换算命中位置");
            centreX -= client.X - rect.Left;
            centreY -= client.Y - rect.Top;
        }

        return new FindReference(expected, true, mode, maxErrors, match.Rect,
            new FindPoint("client", centreX, centreY), match.LineText, match.Distance, match.Confidence);
    }

    private static NativeWindow.RECT WindowBounds(WindowInfo window, bool windowSpace)
    {
        NativeWindow.RECT rect;
        if (window.Handle == IntPtr.Zero || !(windowSpace
            ? NativeWindow.GetWindowRect(window.Handle, out rect)
            : NativeWindow.GetClientRect(window.Handle, out rect)))
            throw new CommandFailure(4, $"「{window.Title}」的{(windowSpace ? "窗口" : "客户区")}尺寸读不出来——未读取任何内容");
        return rect;
    }

    /// <summary>
    /// Checks a region against the space it claims to live in. Shared by the CLI and by the
    /// `wait-text` predicate so the two can never disagree about what "out of bounds" means.
    /// </summary>
    internal static void ValidateRegion(WindowInfo window, bool windowSpace, (int X, int Y, int W, int H) region)
    {
        var bounds = WindowBounds(window, windowSpace);
        int spaceWidth = bounds.Right - bounds.Left;
        int spaceHeight = bounds.Bottom - bounds.Top;
        string spaceName = windowSpace ? "窗口" : "客户区";
        if (region.X < 0 || region.Y < 0 || region.W <= 0 || region.H <= 0 ||
            (long)region.X + region.W > spaceWidth || (long)region.Y + region.H > spaceHeight)
        {
            throw new ArgumentException(
                $"区域 {region.X},{region.Y},{region.W},{region.H} 超出{spaceName}（{spaceWidth}x{spaceHeight}）——" +
                $"probe 的区域是{spaceName}相对坐标，不猜、也不截断");
        }
    }

    /// <summary>
    /// 屏幕区域的边界就是整个虚拟桌面（多显示器时是各屏并集）。屏幕区域不隶属于任何窗口，
    /// 所以这里不碰 GetClientRect——那正是"不填 target 也能读"的意义。
    /// </summary>
    internal static void ValidateScreenRegion((int X, int Y, int W, int H) region)
    {
        int left = NativeHooks.GetSystemMetrics(NativeHooks.SmXVirtualScreen);
        int top = NativeHooks.GetSystemMetrics(NativeHooks.SmYVirtualScreen);
        int width = NativeHooks.GetSystemMetrics(NativeHooks.SmCxVirtualScreen);
        int height = NativeHooks.GetSystemMetrics(NativeHooks.SmCyVirtualScreen);
        if (width <= 0 || height <= 0)
            throw new ArgumentException("读不到虚拟屏幕的尺寸，无法校验屏幕区域");
        if (region.W <= 0 || region.H <= 0)
            throw new ArgumentException($"屏幕区域 {region.X},{region.Y},{region.W},{region.H} 的宽高必须大于 0");
        if (region.X < left || region.Y < top || (long)region.X + region.W > (long)left + width || (long)region.Y + region.H > (long)top + height)
        {
            throw new ArgumentException(
                $"屏幕区域 {region.X},{region.Y},{region.W},{region.H} 超出桌面（{left},{top} {width}x{height}）——" +
                "probe 的屏幕区域是绝对坐标，不猜、也不截断");
        }
    }

    /// <summary>
    /// Captures one region. The screen grab starts on the desktop, so its origin depends on the
    /// space being addressed: a window-space region is measured from the window's outer rectangle, a
    /// client-space one from the client origin ClientToScreen reports. Getting this wrong is silent -
    /// the first version always started at the client origin, so a title-bar selection read the top
    /// strip of the client area instead (proof: both spaces produced byte-identical images).
    /// </summary>
    internal static bool IsSeen(
        IReadOnlyList<string> reads, double? confidence, int minConfidence, int requested, out string reason)
    {
        reason = "";
        if (reads.Count == 0)
        {
            reason = "没有读取到任何结果";
            return false;
        }

        string first = reads[0];
        if (reads.Any(r => r != first))
        {
            string seen = string.Join("\n", reads.Select((r, i) => $"  第 {i + 1} 次：{Show(r)}"));
            reason = $"{requested} 次读取结果不一致，判定为没看清（未做出任何判断）：\n{seen}";
            return false;
        }

        if (first.Length > 0 && confidence is double c && c < minConfidence)
        {
            reason = $"读到了文字，但置信度 {c.ToString("0.0", CultureInfo.InvariantCulture)} 低于下限 {minConfidence}" +
                     $"（实测这个区间基本是乱码）：{Show(reads[0])}";
            return false;
        }

        return true;
    }

    /// <summary>Client-relative "x,y,w,h"; anything else is a usage error, not a guess.</summary>
    internal static (int X, int Y, int W, int H) ParseRegion(string raw)
    {
        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            throw new ArgumentException($"--region 需要四个逗号分隔的整数（x,y,w,h），收到 '{raw}'");

        var values = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                throw new ArgumentException($"--region 的第 {i + 1} 个值 '{parts[i]}' 不是整数");
        }
        return (values[0], values[1], values[2], values[3]);
    }

}

