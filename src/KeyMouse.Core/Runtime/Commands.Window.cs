namespace KeyMouse.Runtime;

/// <summary>
/// 窗口：list / inspect / focus。
/// </summary>
internal static partial class Commands
{

    private static int WindowGroup(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "window：需要一个子命令（list | inspect | focus）");

        string cmd = args[0].ToLowerInvariant();
        var (_, opt) = Parse(args[1..], "filter", "process", "limit");

        switch (cmd)
        {
            case "list":
            {
                string? filter = opt.GetValueOrDefault("filter") ?? g.Title;
                string? process = opt.GetValueOrDefault("process") ?? g.ProcessName;
                bool all = opt.ContainsKey("all");

                // Enumerate cheaply, filter, and only then probe the rows that are actually
                // printed - probing every top-level window cost up to 100 ms per hung one.
                var windows = WindowLocator.EnumerateTopLevel()
                    .Where(w => (all || (w.Visible && w.Title.Length > 0))
                                && (filter is null || w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                && (process is null || w.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                foreach (var w in windows) w.WithResponse(NativeWindow.ResponseMs(w.Handle, 100));

                Console.WriteLine(ConsoleText.Pad("句柄", 12) + ConsoleText.Pad("进程", 22) +
                                  ConsoleText.Pad("标题", 42) + " " + ConsoleText.Pad("类名", 26) + " 状态");
                foreach (var w in windows) Console.WriteLine(w.TableRow());
                Console.WriteLine(all
                    ? $"\n共 {windows.Count} 个窗口（含隐藏与无标题的）"
                    : $"\n共 {windows.Count} 个可见且有标题的窗口——加 --all 可列出隐藏/无标题的辅助窗口");
                return 0;
            }

            case "inspect":
            {
                if (!g.HasSelector) return Fail(2, "window inspect 需要一个窗口选择器（--title/--class/--process/--pid/--hwnd）");
                var selector = g.ToSelector();
                var matched = WindowLocator.Find(selector);
                if (matched.Count == 0)
                    return Fail(3, WindowLocator.NoMatchMessage(WindowLocator.EnumerateTopLevel(), selector));

                Console.WriteLine($"有 {matched.Count} 个窗口匹配 {selector.Describe()}\n");
                int usable = 0;
                foreach (var candidate in matched)
                {
                    var verdict = WindowEligibility.Check(candidate, g.AllowRestore, out var current);
                    if (verdict.Ok) usable++;
                    Console.WriteLine($"  {current.Describe()}");
                    Console.WriteLine($"      矩形 {current.Rect.Left},{current.Rect.Top} {current.Rect.Width}x{current.Rect.Height}   前台={WindowFocus.IsForeground(current.Handle)}");
                    Console.WriteLine($"      判定：{(verdict.Ok ? "可用" : "不可用")}");
                    foreach (var problem in verdict.Problems) Console.WriteLine($"      问题：{problem}");
                    foreach (var note in verdict.Notes) Console.WriteLine($"      备注：{note}");
                    Console.WriteLine();
                }

                // Same preference rule as the gate, so inspect explains what a real
                // command would actually pick.
                var considered = PreferCandidates(matched);

                Console.WriteLine(usable == 0
                    ? "没有可用窗口（原因见上面的「问题」）"
                    : $"有 {usable} 个可用窗口；多于一个时请用 --pick <n> 指定");
                return usable == 0 ? (considered.Count == 1 ? 4 : 3) : 0;
            }

            case "focus":
            {
                var target = PrepareTarget(g) ?? throw new CommandFailure(2, "window focus 需要一个窗口选择器");
                // 这条命令的全部意义就是把窗口提到前台：与其它命令不同，它不看 --focus-policy。
                // （默认策略是 never——普通命令只拿窗口换算坐标，不抢焦点；但 focus 不抢焦点等于什么都没做。）
                FocusResult focus = WindowFocus.Focus(target.Handle, g.FocusAttempts);
                if (!focus.Ok)
                    throw new CommandFailure(5, $"无法聚焦「{target.Title}」：{focus.Detail}");
                Console.WriteLine($"已聚焦 {target.Describe()}（{focus.Detail}）");
                return 0;
            }

            default:
                return Fail(2, $"window：未知子命令 '{cmd}'");
        }
    }

}
