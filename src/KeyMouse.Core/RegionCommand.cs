using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse;

/// <summary>A rectangle in screen pixels (virtual-screen coordinates, which can be negative).</summary>
internal readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    internal int Right => X + Width;
    internal int Bottom => Y + Height;

    /// <summary>True when <paramref name="other"/> lies entirely inside this rectangle.</summary>
    internal bool Contains(ScreenRect other) =>
        other.Width > 0 && other.Height > 0 &&
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>The same rectangle expressed relative to a space whose origin is (originX, originY).</summary>
    internal ScreenRect RelativeTo(int originX, int originY) => new(X - originX, Y - originY, Width, Height);
}

/// <summary>
/// Where a picked screen rectangle sits inside the window underneath it.
/// <see cref="Space"/> is null when the rectangle is not wholly inside one window - then there is
/// no coordinate system in which probe could accept it, and the note says so.
/// </summary>
internal sealed record RegionPlacement(WindowInfo? Window, string? Space, ScreenRect? Region, string? Note);

/// <summary>
/// `region pick` - a human draws the rectangle instead of typing coordinates.
///
/// Why this exists: three times in a row a region was picked from memory and landed on wallpaper
/// or blank space, and probe correctly reported "read empty". The tool refuses to guess, so the
/// human has to show it - and showing is faster and more reliable than typing four numbers.
///
/// What it is not: it does not read anything (that is still probe's job), and it does not decide
/// which window matters by itself beyond "the window under the centre of the selection".
/// </summary>
internal static class RegionCommand
{
    internal static int Run(string[] args, Commands.GlobalOptions g)
    {
        var (positional, options) = Commands.Parse(args, "rect");

        if (positional.Count == 0)
            throw new ArgumentException("region：需要一个子命令（目前只有 pick）");
        if (!string.Equals(positional[0], "pick", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"region：未知子命令 '{positional[0]}'（可用：pick）");
        if (positional.Count > 1)
            throw new ArgumentException($"region pick 不接受位置参数 '{positional[1]}'");
        if (g.HasSelector)
            throw new ArgumentException("region pick 不接受窗口选择器——选区落在哪个窗口上由选区自己决定");
        if (options.ContainsKey("region") || options.ContainsKey("space"))
            throw new ArgumentException("region pick 不接受 --region / --space：那是 probe 的参数，这里只需要框一下");

        ScreenRect picked;
        if (options.TryGetValue("rect", out string? rawRect))
        {
            // The non-interactive path: smoke tests and scripts use it, humans do not.
            picked = ParseRect(rawRect);
        }
        else
        {
            var selection = RegionPicker.Pick()
                ?? throw new CommandFailure(3, "已取消选区——没有产生任何坐标");
            picked = new ScreenRect(selection.X, selection.Y, selection.Width, selection.Height);
        }

        var placement = Describe(picked);
        if (options.ContainsKey("json")) WriteJson(picked, placement);
        else WriteText(picked, placement);
        return 0;
    }

    /// <summary>Resolves the window under the centre of the selection and classifies the rectangle.</summary>
    internal static RegionPlacement Describe(ScreenRect selection)
    {
        int centerX = selection.X + selection.Width / 2;
        int centerY = selection.Y + selection.Height / 2;

        IntPtr handle = WindowLocator.WindowAt(centerX, centerY);
        if (handle == IntPtr.Zero)
            return new RegionPlacement(null, null, null, "选区中心下面没有窗口——probe 需要一个窗口");
        var window = WindowInfo.Capture(handle);

        NativeWindow.GetWindowRect(handle, out NativeWindow.RECT windowRectRaw);
        var windowRect = new ScreenRect(
            windowRectRaw.Left, windowRectRaw.Top,
            windowRectRaw.Right - windowRectRaw.Left, windowRectRaw.Bottom - windowRectRaw.Top);

        var (space, region) = Classify(selection, windowRect, ClientBounds(handle, windowRect));
        string? note = space is null
            ? $"选区没有整个落在「{window.Title}」里——probe 的坐标必须属于同一个窗口，把框收小一点"
            : null;
        return new RegionPlacement(window, space, region, note);
    }

    /// <summary>
    /// The client area in screen pixels, or the window rectangle when the window has no usable
    /// client area (WebView shells were measured at 0x0).
    /// </summary>
    internal static ScreenRect ClientBounds(IntPtr handle, ScreenRect windowRect)
    {
        if (!NativeWindow.GetClientRect(handle, out NativeWindow.RECT client)) return windowRect;
        int width = client.Right - client.Left;
        int height = client.Bottom - client.Top;
        if (width <= 0 || height <= 0) return windowRect;

        var (x, y) = WindowLocator.ClientToScreen(handle, 0, 0);
        return new ScreenRect(x, y, width, height);
    }

    /// <summary>
    /// The whole rule, in one place and free of native calls so it can be tested:
    /// prefer the client area (a window-relative region stops being valid the moment the window
    /// moves, and the title bar is the only part that needs the window space), otherwise fall
    /// back to the window rectangle, otherwise there is no usable space at all.
    /// </summary>
    internal static (string? Space, ScreenRect? Region) Classify(ScreenRect selection, ScreenRect windowRect, ScreenRect clientRect)
    {
        if (clientRect.Contains(selection)) return ("client", selection.RelativeTo(clientRect.X, clientRect.Y));
        if (windowRect.Contains(selection)) return ("window", selection.RelativeTo(windowRect.X, windowRect.Y));
        return (null, null);
    }

    internal static ScreenRect ParseRect(string raw)
    {
        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            throw new ArgumentException($"--rect 需要 x,y,w,h 四个整数，收到 '{raw}'");

        var values = new int[4];
        for (int i = 0; i < 4; i++)
            values[i] = Commands.IntArg(parts[i], "--rect");
        if (values[2] <= 0 || values[3] <= 0)
            throw new ArgumentException($"--rect 的宽和高必须为正，收到 '{raw}'");

        return new ScreenRect(values[0], values[1], values[2], values[3]);
    }

    /// <summary>The command a caller would paste next, or null when there is no usable space.</summary>
    internal static string? SuggestedProbe(RegionPlacement placement)
    {
        if (placement.Window is null || placement.Region is not { } region) return null;
        string space = placement.Space == "window" ? "--space window " : "";
        return $"KeyMouse probe --hwnd 0x{placement.Window.Handle.ToInt64():X} {space}" +
               $"--region {region.X},{region.Y},{region.Width},{region.Height}";
    }

    private static void WriteText(ScreenRect screen, RegionPlacement placement)
    {
        Console.WriteLine($"选区  屏幕 {screen.X},{screen.Y} {screen.Width}x{screen.Height}");
        if (placement.Window is { } window) Console.WriteLine($"目标  {window.Describe()}");
        if (placement.Space is { } space && placement.Region is { } region)
            Console.WriteLine($"空间  {(space == "client" ? "客户区" : "窗口")}（{space}）  region {region.X},{region.Y},{region.Width},{region.Height}");
        if (placement.Note is { } note) Console.WriteLine($"注意  {note}");
        if (SuggestedProbe(placement) is { } command) Console.WriteLine($"命令  {command}");
    }

    private static void WriteJson(ScreenRect screen, RegionPlacement placement)
    {
        var report = new
        {
            ok = true,
            screen = new { rect = new[] { screen.X, screen.Y, screen.Width, screen.Height } },
            window = placement.Window is { } w
                ? new
                {
                    handle = $"0x{w.Handle.ToInt64():X}",
                    title = w.Title,
                    pid = w.ProcessId,
                    @class = w.ClassName,
                }
                : null,
            space = placement.Space,
            region = placement.Region is { } r ? new[] { r.X, r.Y, r.Width, r.Height } : null,
            probe = SuggestedProbe(placement),
            note = placement.Note,
        };

        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
