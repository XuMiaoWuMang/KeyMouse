using System.Globalization;

namespace KeyMouse;

internal static class Program
{
    internal const string Version = "2.0.0";

    /// <summary>
    /// STA because the region picker is a WinForms window: an interactive desktop selection needs
    /// a single-threaded apartment, and everything else here is unaffected by the choice.
    /// </summary>
    [STAThread]
    internal static int Main(string[] args)
    {
        // Speak the console's code page, so that whoever reads us decodes what we wrote.
        ConsoleText.ConfigureOutputEncoding();

        try
        {
            string[] rest = ExtractGlobalOptions(args, out GlobalOptions global);
            if (rest.Length == 0) { PrintUsage(); return 2; }

            switch (rest[0].ToLowerInvariant())
            {
                case "-h" or "--help" or "help" or "/?":
                    PrintUsage();
                    return 0;
                case "-v" or "--version" or "version":
                    Console.WriteLine($"KeyMouse {Version}");
                    return 0;
                case "mouse":
                    return Mouse(rest[1..], global);
                case "key" or "keyboard":
                    return Keyboard(rest[1..], global);
                case "window":
                    return WindowGroup(rest[1..], global);
                case "run":
                    return ScriptRunner.Run(rest[1..], global, Main);
                case "probe":
                    return Probe.Run(rest[1..], global);
                case "region":
                    return RegionCommand.Run(rest[1..], global);
                default:
                    return Fail(2, $"未知命令组 '{rest[0]}'（可用：mouse | key | window | run | probe | region | help）");
            }
        }
        catch (CommandFailure ex)
        {
            return Fail(ex.Code, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Fail(2, ex.Message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误：{ex.Message}");
            return 1;
        }
    }

    // ------------------------------------------------------- window targeting

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
        if (string.Equals(g.FocusPolicy, "none", StringComparison.OrdinalIgnoreCase))
        {
            if (!WindowFocus.IsForeground(target.Handle))
                throw new CommandFailure(5, $"focus-policy none：「{target.Title}」不是前台窗口——未发送任何输入");
            return;
        }

        if (!string.Equals(g.FocusPolicy, "gentle", StringComparison.OrdinalIgnoreCase))
            throw new CommandFailure(2, $"--focus-policy 只能是 gentle 或 none（收到 '{g.FocusPolicy}'）");

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

    // ---------------------------------------------------------------- mouse

    private static int Mouse(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "mouse：需要一个子命令（move | moveby | click | dblclick | down | up | wheel | hwheel | drag | pos）");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "x", "y", "n", "i", "button", "steps", "duration", "delta");

        WindowInfo? cached = null;
        WindowInfo? EnsureTarget() => g.HasSelector ? cached ??= PrepareTarget(g) : null;

        switch (cmd)
        {
            case "pos":
            {
                var (x, y) = NativeInput.GetCursor();
                Console.WriteLine($"{x},{y}");
                return 0;
            }

            case "move":
            {
                var target = EnsureTarget();
                var relative = RelativePoint(g, target, "mouse move");

                if (relative is { } rel)
                {
                    RejectAbsolute(opt, "-x/-y");
                    NativeInput.MoveTo(rel.X, rel.Y);
                    Console.WriteLine($"已移动到客户区 {g.Wx},{g.Wy} → 屏幕 {rel.X},{rel.Y}");
                    return 0;
                }

                if (pos.Count != 2) return Fail(2, "用法：KeyMouse mouse move <x> <y>   或   mouse move -wx <cx> -wy <cy> --title <标题>");
                int mx = IntArg(pos[0], "x"), my = IntArg(pos[1], "y");
                NativeInput.MoveTo(mx, my);
                var p1 = NativeInput.GetCursor();
                Console.WriteLine($"已移动到 {p1.X},{p1.Y}");
                return 0;
            }

            case "moveby":
            {
                if (pos.Count != 2) return Fail(2, "用法：KeyMouse mouse moveby <dx> <dy>");
                int dx = IntArg(pos[0], "dx"), dy = IntArg(pos[1], "dy");
                NativeInput.MoveBy(dx, dy);
                var p = NativeInput.GetCursor();
                Console.WriteLine($"已相对移动 {dx},{dy} → {p.X},{p.Y}");
                return 0;
            }

            case "click":
            case "dblclick":
            {
                var target = EnsureTarget();
                var relative = RelativePoint(g, target, $"mouse {cmd}");

                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                int count = cmd == "dblclick" ? 2 : IntOr(opt, "n", 1);
                int interval = IntOr(opt, "i", 80);

                (int X, int Y)? point = relative;
                if (point is null)
                {
                    int? ax = opt.ContainsKey("x") ? IntArg(opt["x"], "x") : null;
                    int? ay = opt.ContainsKey("y") ? IntArg(opt["y"], "y") : null;
                    if (ax.HasValue != ay.HasValue) return Fail(2, "-x 和 -y 必须成对出现");
                    if (ax.HasValue) point = (ax.Value, ay!.Value);
                }
                else
                {
                    RejectAbsolute(opt, "-x/-y");
                }

                if (point is { } pt) NativeInput.MoveTo(pt.X, pt.Y);

                NativeInput.Click(button, count, interval);
                var now = NativeInput.GetCursor();
                Console.WriteLine($"已点击 {button}{(count > 1 ? $" ×{count}" : "")}，位置 {now.X},{now.Y}");
                return 0;
            }

            case "down":
            case "up":
            {
                EnsureTarget();
                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                if (cmd == "down") NativeInput.ButtonDown(button); else NativeInput.ButtonUp(button);
                Console.WriteLine($"鼠标 {button} 已{(cmd == "down" ? "按下" : "松开")}");
                return 0;
            }

            case "wheel":
            case "hwheel":
            {
                var target = EnsureTarget();
                var relative = RelativePoint(g, target, $"mouse {cmd}");

                if (pos.Count == 0 && !opt.ContainsKey("delta"))
                    return Fail(2, $"用法：KeyMouse mouse {cmd} <增量> [-x X -y Y]   （120 = 一格，+ 为向上/向右）");
                int delta = pos.Count > 0 ? IntArg(pos[0], "delta") : IntArg(opt["delta"], "delta");

                (int X, int Y)? point = relative;
                if (point is null)
                {
                    int? ax = opt.ContainsKey("x") ? IntArg(opt["x"], "x") : null;
                    int? ay = opt.ContainsKey("y") ? IntArg(opt["y"], "y") : null;
                    if (ax.HasValue != ay.HasValue) return Fail(2, "-x 和 -y 必须成对出现");
                    if (ax.HasValue) point = (ax.Value, ay!.Value);
                }
                else
                {
                    RejectAbsolute(opt, "-x/-y");
                }

                if (point is { } wheelAt) NativeInput.MoveTo(wheelAt.X, wheelAt.Y);

                NativeInput.Wheel(delta, cmd == "hwheel");
                Console.WriteLine($"滚轮{(cmd == "hwheel" ? "（横向）" : "（纵向）")} 增量 {delta}");
                return 0;
            }

            case "drag":
            {
                string button = opt.GetValueOrDefault("button") ?? "left";
                int steps = IntOr(opt, "steps", 20);
                int duration = IntOr(opt, "duration", 400);

                bool anyRelative = g.Wx.HasValue || g.Wy.HasValue || g.Wx2.HasValue || g.Wy2.HasValue;
                if (anyRelative)
                {
                    // Check the shape of the arguments first, so a usage error does not depend
                    // on the state of the desktop.
                    if (!g.Wx.HasValue || !g.Wy.HasValue || !g.Wx2.HasValue || !g.Wy2.HasValue)
                        return Fail(2, "相对拖拽需要 -wx -wy --wx2 --wy2 四个都给");
                    if (pos.Count != 0)
                        return Fail(2, "四个坐标和四个客户区标志只能给一套，不能混用");

                    var target = EnsureTarget()
                        ?? throw new CommandFailure(2, "mouse drag：-wx/-wy 需要窗口选择器" +
                                                       "（--title/--class/--process/--pid/--hwnd）");

                    var from = ClientPoint(target, g.Wx.Value, g.Wy.Value, "drag start");
                    var to = ClientPoint(target, g.Wx2.Value, g.Wy2.Value, "drag end");
                    if (g.StrictPoint)
                    {
                        RequirePointBelongsToTarget(target, from.X, from.Y, "drag start");
                        RequirePointBelongsToTarget(target, to.X, to.Y, "drag end");
                    }

                    NativeInput.Drag(from.X, from.Y, to.X, to.Y, button, steps, duration);
                    Console.WriteLine($"已从客户区 {g.Wx},{g.Wy} 拖到 {g.Wx2},{g.Wy2}" +
                                      $"（屏幕 {from.X},{from.Y} → {to.X},{to.Y}），用 {button} 键");
                    return 0;
                }

                EnsureTarget();
                if (pos.Count != 4)
                    return Fail(2, "用法：KeyMouse mouse drag <x1> <y1> <x2> <y2> [--button left] [--steps 20] [--duration 400]");

                NativeInput.Drag(
                    IntArg(pos[0], "x1"), IntArg(pos[1], "y1"),
                    IntArg(pos[2], "x2"), IntArg(pos[3], "y2"),
                    button, steps, duration);
                Console.WriteLine($"已从 {pos[0]},{pos[1]} 拖到 {pos[2]},{pos[3]}，用 {button} 键");
                return 0;
            }

            default:
                return Fail(2, $"mouse：未知子命令 '{cmd}'");
        }
    }

    private static void RejectAbsolute(Dictionary<string, string> opt, string what)
    {
        if (opt.ContainsKey("x") || opt.ContainsKey("y"))
            throw new CommandFailure(2, $"客户区坐标（-wx/-wy）和绝对坐标（{what}）只能给一套，不能混用");
    }

    // ------------------------------------------------------------- keyboard

    private static int Keyboard(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "key：需要一个子命令（press | down | up | combo | type）");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "n", "i", "interval", "hold");

        WindowInfo? cached = null;
        void EnsureTarget() { if (g.HasSelector) cached ??= PrepareTarget(g); }

        switch (cmd)
        {
            case "press":
            case "tap":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key press <按键> [-n 次数] [-i 毫秒]");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                int count = IntOr(opt, "n", 1);
                int interval = IntOr(opt, "i", 60);
                for (int i = 0; i < count; i++)
                {
                    NativeInput.Tap(vk, ext);
                    if (i < count - 1) Thread.Sleep(Math.Max(1, interval));
                }
                Console.WriteLine($"已敲击 {pos[0]}{(count > 1 ? $" ×{count}" : "")}");
                return 0;
            }

            case "down":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key down <按键>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, false);
                Console.WriteLine($"已按下 {pos[0]}");
                return 0;
            }

            case "up":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key up <按键>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, true);
                Console.WriteLine($"已松开 {pos[0]}");
                return 0;
            }

            case "combo":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key combo <键1+键2+...>   例如 ctrl+shift+s");
                var keys = new List<(ushort Vk, bool Ext)>();
                foreach (string part in pos[0].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    keys.Add(KeyMap.Resolve(part));

                keys = keys.OrderBy(k => IsModifier(k.Vk) ? 0 : 1).ToList();
                NativeInput.Chord(keys, IntOr(opt, "hold", 30));
                Console.WriteLine("已发送组合键 " + string.Join("+", keys.Select(k => $"0x{k.Vk:X2}")));
                return 0;
            }

            case "type":
            {
                EnsureTarget();
                if (pos.Count == 0) return Fail(2, "用法：KeyMouse key type <文本> [--interval 毫秒]");
                string text = string.Join(' ', pos);
                NativeInput.TypeText(text, IntOr(opt, "interval", IntOr(opt, "i", 15)));
                Console.WriteLine($"已输入 {text.Length} 个字符");
                return 0;
            }

            default:
                return Fail(2, $"key：未知子命令 '{cmd}'");
        }
    }

    // ---------------------------------------------------------------- window

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
                Console.WriteLine($"已聚焦 {target.Describe()}");
                return 0;
            }

            default:
                return Fail(2, $"window：未知子命令 '{cmd}'");
        }
    }

    // -------------------------------------------------------------- helpers

    internal sealed class GlobalOptions
    {
        public string? Title;
        public string? TitleExact;
        public string? ClassName;
        public string? ProcessName;
        public uint? Pid;
        public IntPtr? Hwnd;
        public int? Pick;
        public string FocusPolicy = "gentle";
        public int FocusAttempts = WindowFocus.DefaultAttempts;
        public bool AllowRestore;
        public bool StrictPoint;
        public int? Wx;
        public int? Wy;

        /// <summary>Client-area end point: only `mouse drag` uses it.</summary>
        public int? Wx2;
        public int? Wy2;

        public bool HasSelector => Title is not null || TitleExact is not null || ClassName is not null ||
                                   ProcessName is not null || Pid is not null || Hwnd is not null;

        public WindowSelector ToSelector() => new()
        {
            Title = Title,
            TitleExact = TitleExact,
            ClassName = ClassName,
            ProcessName = ProcessName,
            ProcessId = Pid,
            Handle = Hwnd
        };
    }

    /// <summary>Pulls window/focus options out of argv; everything else stays for the command parser.</summary>
    internal static string[] ExtractGlobalOptions(string[] args, out GlobalOptions g)
    {
        g = new GlobalOptions();
        var rest = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--")
            {
                for (int j = i + 1; j < args.Length; j++) rest.Add(args[j]);
                break;
            }
            if (arg.Length < 2 || arg[0] != '-')
            {
                rest.Add(arg);
                continue;
            }

            string name = arg.TrimStart('-');
            string? inline = null;
            int eq = name.IndexOf('=');
            if (eq >= 0)
            {
                inline = name[(eq + 1)..];
                name = name[..eq];
            }

            string Take(string what)
            {
                if (inline is not null) return inline;
                if (i + 1 >= args.Length) throw new ArgumentException($"--{name} 需要一个值（{what}）");
                return args[++i];
            }

            switch (name.ToLowerInvariant())
            {
                case "title": g.Title = Take("标题子串"); break;
                case "title-exact": g.TitleExact = Take("完整标题"); break;
                case "class": g.ClassName = Take("窗口类名"); break;
                case "process": g.ProcessName = Take("进程名，如 notepad"); break;
                case "pid": g.Pid = (uint)IntArg(Take("进程 ID"), "--pid"); break;
                case "hwnd": g.Hwnd = WindowLocator.ParseHandle(Take("窗口句柄")); break;
                case "pick": g.Pick = IntArg(Take("候选序号"), "--pick"); break;
                case "focus-policy": g.FocusPolicy = Take("gentle|none"); break;
                case "focus-attempts": g.FocusAttempts = Math.Max(1, IntArg(Take("尝试次数"), "--focus-attempts")); break;
                case "allow-restore": g.AllowRestore = true; break;
                case "strict-point": g.StrictPoint = true; break;
                case "wx": g.Wx = IntArg(Take("客户区 x"), "-wx"); break;
                case "wx2": g.Wx2 = IntArg(Take("拖拽终点的客户区 x"), "--wx2"); break;
                case "wy2": g.Wy2 = IntArg(Take("拖拽终点的客户区 y"), "--wy2"); break;
                case "wy": g.Wy = IntArg(Take("客户区 y"), "-wy"); break;
                default: rest.Add(arg); break;
            }
        }

        return rest.ToArray();
    }

    private static bool IsModifier(ushort vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;

    /// <summary>Splits argv into positional values and -flag/--flag[=value] options.</summary>
    internal static (List<string> Positional, Dictionary<string, string> Options) Parse(string[] args, params string[] valueFlags)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            bool looksLikeOption = a.Length > 1 && a[0] == '-' &&
                                   !int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            if (!looksLikeOption)
            {
                positional.Add(a);
                continue;
            }

            string name = a.TrimStart('-');
            string? value = null;
            int eq = name.IndexOf('=');
            if (eq >= 0)
            {
                value = name[(eq + 1)..];
                name = name[..eq];
            }
            if (value is null && valueFlags.Contains(name, StringComparer.OrdinalIgnoreCase) && i + 1 < args.Length)
                value = args[++i];
            options[name] = value ?? "true";
        }

        return (positional, options);
    }

    internal static int IntArg(string raw, string what) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : throw new ArgumentException($"'{raw}' 不是合法的整数（{what}）");

    internal static int IntOr(Dictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out string? raw) &&
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine("错误：" + message);
        return code;
    }

    private static void PrintUsage() => Usage.Print();
}
