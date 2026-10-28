namespace KeyMouse.Runtime;

/// <summary>
/// 目标窗口：把命令行选择器解析成一个可用的窗口，并在动手前做完资格检查。
/// </summary>
internal static partial class Commands
{

    /// <summary>
    /// Resolves the selector and applies the eligibility gate, without touching focus.
    /// Reading needs exactly this half of the work: a background window can be read, but it
    /// still has to be able to paint. Throws CommandFailure (nothing read, nothing sent).
    /// </summary>
    internal static WindowInfo ResolveUsable(GlobalOptions g)
    {
        var selector = g.ToSelector();
        var resolution = WindowResolver.Resolve(selector, g.AllowRestore);
        if (resolution.Matched.Count == 0)
            throw new CommandFailure(3, WindowLocator.NoMatchMessage(resolution.All, selector));

        var usable = resolution.Usable;
        if (usable.Count == 0)
        {
            string head = resolution.Considered.Count == 1
                ? "目标窗口不可用"
                : $"有 {resolution.Considered.Count} 个窗口匹配，但没有一个可用";
            throw new CommandFailure(resolution.Considered.Count == 1 ? 4 : 3,
                $"{head}:\n{string.Join("\n", resolution.Rejections)}");
        }

        if (usable.Count == 1) return usable[0];
        if (g.Pick is int pick && pick >= 1 && pick <= usable.Count) return usable[pick - 1];

        throw new CommandFailure(3,
            $"有 {usable.Count} 个可用窗口匹配——请用 --pick <n> 指定一个：\n{WindowLocator.CandidateTable(usable)}");
    }

    /// <summary>
    /// Resolves the selector, applies the eligibility gate, brings the window to the
    /// foreground and re-verifies it. Throws CommandFailure (nothing sent) otherwise.
    /// Returns null only when the caller gave no selector at all.
    /// </summary>
    private static WindowInfo? PrepareTarget(GlobalOptions g)
    {
        if (!g.HasSelector) return null;

        WindowInfo target = ResolveUsable(g);

        FocusTarget(target, g);

        var post = WindowInfo.Capture(target.Handle, 200);
        if (!post.Visible || post.Minimized)
            throw new CommandFailure(4, $"「{target.Title}」在聚焦与注入之间消失了——未发送任何输入");
        if (post.ResponseMs is null)
            throw new CommandFailure(4, $"「{target.Title}」刚聚焦就不响应了——未发送任何输入");

        return post;
    }

    /// <summary>
    /// Narrows candidates without guessing which window the user meant: visible windows
    /// first (an app also owns hidden helper windows), then unowned windows first (dialogs,
    /// popups, composition bridges and IME UI are owned by a primary window). Each tier is
    /// only applied when it leaves something behind, so a hidden or owned target stays
    /// reachable when nothing else matched.
    /// </summary>
    internal static List<WindowInfo> PreferCandidates(IReadOnlyList<WindowInfo> matched)
    {
        var considered = matched.Where(w => w.Visible).ToList();
        if (considered.Count == 0) considered = matched.ToList();

        var primary = considered.Where(w => w.Owner == IntPtr.Zero).ToList();
        return primary.Count > 0 ? primary : considered;
    }

    private static void FocusTarget(WindowInfo target, GlobalOptions g)
    {
        // 默认什么都不做：窗口只用来换算坐标，焦点归用户。
        if (string.Equals(g.FocusPolicy, "never", StringComparison.OrdinalIgnoreCase)) return;

        if (string.Equals(g.FocusPolicy, "none", StringComparison.OrdinalIgnoreCase))
        {
            if (!WindowFocus.IsForeground(target.Handle))
                throw new CommandFailure(5, $"focus-policy none：「{target.Title}」不是前台窗口——未发送任何输入");
            return;
        }

        if (!string.Equals(g.FocusPolicy, "gentle", StringComparison.OrdinalIgnoreCase))
            throw new CommandFailure(2, $"--focus-policy 只能是 never / gentle / none（收到 '{g.FocusPolicy}'）");

        var result = WindowFocus.Focus(target.Handle, g.FocusAttempts);
        if (!result.Ok)
            throw new CommandFailure(5, $"无法聚焦「{target.Title}」：{result.Detail}——未发送任何输入");
    }

    /// <summary>Screen point for -wx/-wy, or null when no relative coordinate was requested.</summary>
    private static (int X, int Y)? RelativePoint(GlobalOptions g, WindowInfo? target, string command)
    {
        if (!g.Wx.HasValue && !g.Wy.HasValue) return null;
        if (!g.Wx.HasValue || !g.Wy.HasValue) throw new CommandFailure(2, "-wx 和 -wy 必须成对出现");
        if (target is null)
            throw new CommandFailure(2, $"{command}：-wx/-wy 需要窗口选择器（--title/--class/--process/--pid/--hwnd）");

        var point = ClientPoint(target, g.Wx.Value, g.Wy.Value, command);
        if (g.StrictPoint) RequirePointBelongsToTarget(target, point.X, point.Y, command);
        return point;
    }

    /// <summary>Converts a client-area point to screen pixels and checks it is still inside.</summary>
    private static (int X, int Y) ClientPoint(WindowInfo target, int clientX, int clientY, string what)
    {
        var (x, y) = WindowLocator.ClientToScreen(target.Handle, clientX, clientY);
        if (!WindowLocator.IsInsideClientArea(target.Handle, x, y))
            throw new CommandFailure(4, $"{what}：客户区坐标 {clientX},{clientY} 落在目标窗口客户区之外");
        return (x, y);
    }

    private static void RequirePointBelongsToTarget(WindowInfo target, int x, int y, string what)
    {
        if (WindowLocator.WindowAt(x, y) != NativeWindow.Root(target.Handle))
            throw new CommandFailure(4, $"strict-point：{x},{y} 下方的窗口不是目标（{what}）——未发送任何输入");
    }

}
