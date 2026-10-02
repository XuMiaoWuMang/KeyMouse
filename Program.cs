using System.Globalization;

namespace KeyMouse;

internal static class Program
{
    private const string Version = "1.2.1";

    internal static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* no console attached */ }

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
                default:
                    return Fail(2, $"unknown group '{rest[0]}' (expected: mouse | key | window | run | help)");
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
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    // ------------------------------------------------------- window targeting

    /// <summary>
    /// Resolves the selector, applies the eligibility gate, brings the window to the
    /// foreground and re-verifies it. Throws CommandFailure (nothing sent) otherwise.
    /// Returns null only when the caller gave no selector at all.
    /// </summary>
    private static WindowInfo? PrepareTarget(GlobalOptions g)
    {
        if (!g.HasSelector) return null;

        var selector = g.ToSelector();
        var matched = WindowLocator.Find(selector);
        if (matched.Count == 0) throw new CommandFailure(3, WindowLocator.NoMatchMessage(selector));

        var considered = PreferCandidates(matched);

        var usable = new List<WindowInfo>();
        var rejected = new List<string>();
        foreach (var candidate in considered)
        {
            var verdict = WindowEligibility.Check(candidate, g.AllowRestore, out var current);
            if (verdict.Ok) usable.Add(current);
            else rejected.Add($"  {current.Describe()}\n      -> {verdict.Summary}");
        }

        if (usable.Count == 0)
        {
            string head = considered.Count == 1
                ? "target window is not usable"
                : $"{considered.Count} windows match but none is usable";
            throw new CommandFailure(considered.Count == 1 ? 4 : 3, $"{head}:\n{string.Join("\n", rejected)}");
        }

        WindowInfo target;
        if (usable.Count == 1)
        {
            target = usable[0];
        }
        else if (g.Pick is int pick && pick >= 1 && pick <= usable.Count)
        {
            target = usable[pick - 1];
        }
        else
        {
            throw new CommandFailure(3,
                $"{usable.Count} usable windows match - add --pick <n>:\n{WindowLocator.CandidateTable(usable)}");
        }

        FocusTarget(target, g);

        var post = WindowInfo.Capture(target.Handle, 200);
        if (!post.Visible || post.Minimized)
            throw new CommandFailure(4, $"\"{target.Title}\" disappeared between focus and injection - nothing was sent");
        if (post.ResponseMs is null)
            throw new CommandFailure(4, $"\"{target.Title}\" stopped responding right after focus - nothing was sent");

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
                throw new CommandFailure(5, $"focus-policy none: \"{target.Title}\" is not the foreground window - nothing was sent");
            return;
        }

        if (!string.Equals(g.FocusPolicy, "gentle", StringComparison.OrdinalIgnoreCase))
            throw new CommandFailure(2, $"--focus-policy must be gentle or none (got '{g.FocusPolicy}')");

        var result = WindowFocus.Focus(target.Handle, g.FocusAttempts);
        if (!result.Ok)
            throw new CommandFailure(5, $"could not focus \"{target.Title}\": {result.Detail} - nothing was sent");
    }

    /// <summary>Screen point for -wx/-wy, or null when no relative coordinate was requested.</summary>
    private static (int X, int Y)? RelativePoint(GlobalOptions g, WindowInfo? target, string command)
    {
        if (!g.Wx.HasValue && !g.Wy.HasValue) return null;
        if (!g.Wx.HasValue || !g.Wy.HasValue) throw new CommandFailure(2, "-wx and -wy must be given together");
        if (target is null)
            throw new CommandFailure(2, $"{command}: -wx/-wy need a window selector (--title/--class/--process/--pid/--hwnd)");

        var (x, y) = WindowLocator.ClientToScreen(target.Handle, g.Wx.Value, g.Wy.Value);
        if (!WindowLocator.IsInsideClientArea(target.Handle, x, y))
            throw new CommandFailure(4, $"client point {g.Wx},{g.Wy} falls outside the target's client area");

        if (g.StrictPoint && WindowLocator.WindowAt(x, y) != NativeWindow.Root(target.Handle))
            throw new CommandFailure(4, $"strict-point: the window at {x},{y} is not the target - nothing was sent");

        return (x, y);
    }

    // ---------------------------------------------------------------- mouse

    private static int Mouse(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "mouse: expected a command (move | moveby | click | dblclick | down | up | wheel | hwheel | drag | pos)");

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
                    Console.WriteLine($"moved to client {g.Wx},{g.Wy} -> screen {rel.X},{rel.Y}");
                    return 0;
                }

                if (pos.Count != 2) return Fail(2, "usage: KeyMouse mouse move <x> <y>   or   mouse move -wx <cx> -wy <cy> --title <t>");
                int mx = IntArg(pos[0], "x"), my = IntArg(pos[1], "y");
                NativeInput.MoveTo(mx, my);
                var p1 = NativeInput.GetCursor();
                Console.WriteLine($"moved to {p1.X},{p1.Y}");
                return 0;
            }

            case "moveby":
            {
                if (pos.Count != 2) return Fail(2, "usage: KeyMouse mouse moveby <dx> <dy>");
                int dx = IntArg(pos[0], "dx"), dy = IntArg(pos[1], "dy");
                NativeInput.MoveBy(dx, dy);
                var p = NativeInput.GetCursor();
                Console.WriteLine($"moved by {dx},{dy} -> {p.X},{p.Y}");
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
                    if (ax.HasValue != ay.HasValue) return Fail(2, "-x and -y must be given together");
                    if (ax.HasValue) point = (ax.Value, ay!.Value);
                }
                else
                {
                    RejectAbsolute(opt, "-x/-y");
                }

                if (point is { } pt) NativeInput.MoveTo(pt.X, pt.Y);

                NativeInput.Click(button, count, interval);
                var now = NativeInput.GetCursor();
                Console.WriteLine($"clicked {button}{(count > 1 ? $" x{count}" : "")} at {now.X},{now.Y}");
                return 0;
            }

            case "down":
            case "up":
            {
                EnsureTarget();
                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                if (cmd == "down") NativeInput.ButtonDown(button); else NativeInput.ButtonUp(button);
                Console.WriteLine($"{button} {cmd}");
                return 0;
            }

            case "wheel":
            case "hwheel":
            {
                var target = EnsureTarget();
                var relative = RelativePoint(g, target, $"mouse {cmd}");

                if (pos.Count == 0 && !opt.ContainsKey("delta"))
                    return Fail(2, $"usage: KeyMouse mouse {cmd} <delta> [-x X -y Y]   (120 = one notch, + = up/right)");
                int delta = pos.Count > 0 ? IntArg(pos[0], "delta") : IntArg(opt["delta"], "delta");

                (int X, int Y)? point = relative;
                if (point is null)
                {
                    int? ax = opt.ContainsKey("x") ? IntArg(opt["x"], "x") : null;
                    int? ay = opt.ContainsKey("y") ? IntArg(opt["y"], "y") : null;
                    if (ax.HasValue != ay.HasValue) return Fail(2, "-x and -y must be given together");
                    if (ax.HasValue) point = (ax.Value, ay!.Value);
                }
                else
                {
                    RejectAbsolute(opt, "-x/-y");
                }

                if (point is { } wheelAt) NativeInput.MoveTo(wheelAt.X, wheelAt.Y);

                NativeInput.Wheel(delta, cmd == "hwheel");
                Console.WriteLine($"wheel {(cmd == "hwheel" ? "h" : "v")} delta {delta}");
                return 0;
            }

            case "drag":
            {
                EnsureTarget();
                if (g.Wx.HasValue || g.Wy.HasValue)
                    return Fail(2, "mouse drag does not support -wx/-wy yet (window-relative drag endpoints are not implemented)");
                if (pos.Count != 4)
                    return Fail(2, "usage: KeyMouse mouse drag <x1> <y1> <x2> <y2> [--button left] [--steps 20] [--duration 400]");

                string button = opt.GetValueOrDefault("button") ?? "left";
                int steps = IntOr(opt, "steps", 20);
                int duration = IntOr(opt, "duration", 400);
                NativeInput.Drag(
                    IntArg(pos[0], "x1"), IntArg(pos[1], "y1"),
                    IntArg(pos[2], "x2"), IntArg(pos[3], "y2"),
                    button, steps, duration);
                Console.WriteLine($"dragged {pos[0]},{pos[1]} -> {pos[2]},{pos[3]} with {button}");
                return 0;
            }

            default:
                return Fail(2, $"mouse: unknown command '{cmd}'");
        }
    }

    private static void RejectAbsolute(Dictionary<string, string> opt, string what)
    {
        if (opt.ContainsKey("x") || opt.ContainsKey("y"))
            throw new CommandFailure(2, $"use either window-relative (-wx/-wy) or absolute ({what}) coordinates, not both");
    }

    // ------------------------------------------------------------- keyboard

    private static int Keyboard(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "key: expected a command (press | down | up | combo | type)");

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
                if (pos.Count != 1) return Fail(2, "usage: KeyMouse key press <key> [-n COUNT] [-i MS]");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                int count = IntOr(opt, "n", 1);
                int interval = IntOr(opt, "i", 60);
                for (int i = 0; i < count; i++)
                {
                    NativeInput.Tap(vk, ext);
                    if (i < count - 1) Thread.Sleep(Math.Max(1, interval));
                }
                Console.WriteLine($"pressed {pos[0]}{(count > 1 ? $" x{count}" : "")}");
                return 0;
            }

            case "down":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "usage: KeyMouse key down <key>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, false);
                Console.WriteLine($"{pos[0]} down");
                return 0;
            }

            case "up":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "usage: KeyMouse key up <key>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, true);
                Console.WriteLine($"{pos[0]} up");
                return 0;
            }

            case "combo":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "usage: KeyMouse key combo <k1+k2+...>   e.g. ctrl+shift+s");
                var keys = new List<(ushort Vk, bool Ext)>();
                foreach (string part in pos[0].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    keys.Add(KeyMap.Resolve(part));

                keys = keys.OrderBy(k => IsModifier(k.Vk) ? 0 : 1).ToList();
                NativeInput.Chord(keys, IntOr(opt, "hold", 30));
                Console.WriteLine("combo " + string.Join("+", keys.Select(k => $"0x{k.Vk:X2}")));
                return 0;
            }

            case "type":
            {
                EnsureTarget();
                if (pos.Count == 0) return Fail(2, "usage: KeyMouse key type <text> [--interval MS]");
                string text = string.Join(' ', pos);
                NativeInput.TypeText(text, IntOr(opt, "interval", IntOr(opt, "i", 15)));
                Console.WriteLine($"typed {text.Length} chars");
                return 0;
            }

            default:
                return Fail(2, $"key: unknown command '{cmd}'");
        }
    }

    // ---------------------------------------------------------------- window

    private static int WindowGroup(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "window: expected a command (list | inspect | focus)");

        string cmd = args[0].ToLowerInvariant();
        var (_, opt) = Parse(args[1..], "filter", "process", "limit");

        switch (cmd)
        {
            case "list":
            {
                string? filter = opt.GetValueOrDefault("filter") ?? g.Title;
                string? process = opt.GetValueOrDefault("process") ?? g.ProcessName;
                bool all = opt.ContainsKey("all");

                var windows = WindowLocator.EnumerateTopLevel(100)
                    .Where(w => (all || (w.Visible && w.Title.Length > 0))
                                && (filter is null || w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                && (process is null || w.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                Console.WriteLine($"{"hwnd",-10}  {"process",-20} {"title",-42} {"class",-26} state");
                foreach (var w in windows) Console.WriteLine(w.TableRow());
                Console.WriteLine(all
                    ? $"\n{windows.Count} window(s), including hidden and untitled ones"
                    : $"\n{windows.Count} visible window(s) with a title - pass --all to include hidden/untitled helper windows");
                return 0;
            }

            case "inspect":
            {
                if (!g.HasSelector) return Fail(2, "window inspect needs a selector (--title/--class/--process/--pid/--hwnd)");
                var selector = g.ToSelector();
                var matched = WindowLocator.Find(selector);
                if (matched.Count == 0) return Fail(3, WindowLocator.NoMatchMessage(selector));

                Console.WriteLine($"{matched.Count} window(s) match {selector.Describe()}\n");
                int usable = 0;
                foreach (var candidate in matched)
                {
                    var verdict = WindowEligibility.Check(candidate, g.AllowRestore, out var current);
                    if (verdict.Ok) usable++;
                    Console.WriteLine($"  {current.Describe()}");
                    Console.WriteLine($"      rect {current.Rect.Left},{current.Rect.Top} {current.Rect.Width}x{current.Rect.Height}   foreground={WindowFocus.IsForeground(current.Handle)}");
                    Console.WriteLine($"      verdict: {(verdict.Ok ? "USABLE" : "NOT USABLE")}");
                    foreach (var problem in verdict.Problems) Console.WriteLine($"      problem: {problem}");
                    foreach (var note in verdict.Notes) Console.WriteLine($"      note: {note}");
                    Console.WriteLine();
                }

                // Same preference rule as the gate, so inspect explains what a real
                // command would actually pick.
                var considered = PreferCandidates(matched);

                Console.WriteLine(usable == 0
                    ? "no usable window (see problems above)"
                    : $"{usable} usable window(s); add --pick <n> when more than one is usable");
                return usable == 0 ? (considered.Count == 1 ? 4 : 3) : 0;
            }

            case "focus":
            {
                var target = PrepareTarget(g) ?? throw new CommandFailure(2, "window focus needs a selector");
                Console.WriteLine($"focused {target.Describe()}");
                return 0;
            }

            default:
                return Fail(2, $"window: unknown command '{cmd}'");
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
                if (i + 1 >= args.Length) throw new ArgumentException($"--{name} needs a value ({what})");
                return args[++i];
            }

            switch (name.ToLowerInvariant())
            {
                case "title": g.Title = Take("title substring"); break;
                case "title-exact": g.TitleExact = Take("exact window title"); break;
                case "class": g.ClassName = Take("window class name"); break;
                case "process": g.ProcessName = Take("process name, e.g. notepad"); break;
                case "pid": g.Pid = (uint)IntArg(Take("process id"), "--pid"); break;
                case "hwnd": g.Hwnd = WindowLocator.ParseHandle(Take("window handle")); break;
                case "pick": g.Pick = IntArg(Take("candidate index"), "--pick"); break;
                case "focus-policy": g.FocusPolicy = Take("gentle|none"); break;
                case "focus-attempts": g.FocusAttempts = Math.Max(1, IntArg(Take("attempt count"), "--focus-attempts")); break;
                case "allow-restore": g.AllowRestore = true; break;
                case "strict-point": g.StrictPoint = true; break;
                case "wx": g.Wx = IntArg(Take("client-area x"), "-wx"); break;
                case "wy": g.Wy = IntArg(Take("client-area y"), "-wy"); break;
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

    private static int IntArg(string raw, string what) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : throw new ArgumentException($"'{raw}' is not a valid integer for {what}");

    private static int IntOr(Dictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out string? raw) &&
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine("error: " + message);
        return code;
    }

    private static void PrintUsage() => Console.WriteLine($"""
        KeyMouse {Version} - inject real mouse & keyboard events on Windows (one shot, no polling, no daemon)

        USAGE
          KeyMouse <group> <command> [arguments] [options]

        MOUSE
          mouse pos                                   print current cursor position
          mouse move <x> <y>                          move cursor to absolute screen pixel
          mouse move -wx <cx> -wy <cy> <selector>     move to a client-area point of a window
          mouse moveby <dx> <dy>                      move cursor relatively
          mouse click [button] [-x X -y Y] [-n N] [-i MS]
          mouse dblclick [button] [-x X -y Y]         double click (same as click -n 2)
          mouse down [button] / mouse up [button]     press / release and keep it held
          mouse wheel <delta> [-x X -y Y]             120 = one notch, + = up (scroll down = -120)
          mouse hwheel <delta>                        horizontal wheel
          mouse drag <x1> <y1> <x2> <y2> [--button B] [--steps N] [--duration MS]

          button: left (default) | right | middle | x1 | x2

        KEYBOARD
          key press <key> [-n COUNT] [-i MS]          tap a key COUNT times
          key down <key> / key up <key>               hold / release a key
          key combo <k1+k2+...> [--hold MS]           e.g. ctrl+shift+s, win+r, alt+f4
          key type <text> [--interval MS]             type unicode text (layout/IME independent)
                                                      default 15ms/char; --interval 0 for max speed

        WINDOW
          window list [--filter <t>] [--process <p>] [--all]
                                                      list top-level windows with their state
          window inspect <selector>                   why a window is usable or not
          window focus <selector>                     focus it and verify (nothing else)

        SCRIPT
          run <file|-> [--delay MS] [--keep-going] [--dry-run] [--echo]
                                                      run commands from a file (or stdin) in order,
                                                      one per line, '#' comments, 'sleep <ms>' lines.
                                                      Each line is a normal command, so selectors and
                                                      the focus gate apply per line. Stops at the first
                                                      failure unless --keep-going. --dry-run runs the
                                                      gate checks but sends nothing. Scripts must be
                                                      UTF-8.

        WINDOW SELECTOR (all given constraints are ANDed; usable by mouse/key/window commands)
          --title <substring>       case-insensitive title substring
          --title-exact <text>      exact title
          --class <name>            window class name
          --process <exe name>      e.g. notepad, QQ
          --pid <n>                 process id
          --hwnd <0x1234|1234>      window handle
          --pick <n>                pick the n-th usable candidate (1-based, top of the z-order first)

        WINDOW POLICY
          --focus-policy gentle|none  gentle (default): SetForegroundWindow, re-read the real
                                      foreground, up to 3 attempts, then fail with exit 5.
                                      none: require it to already be foreground.
          --focus-attempts <n>        attempts for gentle mode (default 3)
          --allow-restore             let KeyMouse un-minimize the target (off by default)
          -wx <cx> -wy <cy>           client-area coordinates (must be paired, needs a selector)
          --strict-point              also require the window under the point to be the target

        KEYS
          a-z  0-9  f1-f24  esc enter tab space backspace delete insert home end
          pageup pagedown up down left right ctrl shift alt win rwin apps capslock
          num0-num9 numadd numsub nummul numdiv numdecimal numenter numlock
          printscreen pause  semicolon equals comma minus period slash grave
          lbracket rbracket backslash quote  vk:0x5B (raw virtual-key escape hatch)

        EXAMPLES
          KeyMouse mouse move 100 200
          KeyMouse mouse click right -x 640 -y 480
          KeyMouse mouse click left -wx 120 -wy 340 --title "记事本"
          KeyMouse key type "hello 世界" --process notepad
          KeyMouse window list --process QQ
          KeyMouse window inspect --title "记事本"

        EXIT CODES
          0 = ok              1 = runtime failure (e.g. SendInput blocked)
          2 = usage error     3 = selector matched nothing / ambiguous (use --pick)
          4 = target not usable (hidden / minimized / cloaked / not responding)
          5 = focus verification failed - nothing was sent
          (a script returns the exit code of its first failing line)

        NOTES
          * Events land on whatever window is focused / under the cursor. With a selector,
            KeyMouse focuses the target first and refuses to send anything if it cannot
            verify the focus - fail closed, never guess.
          * Hidden windows are always refused: force-showing a hidden window (tray apps,
            Electron) can yield a window that only paints one stale frame.
          * SendInput is blocked by UIPI when the target runs elevated - run KeyMouse
            elevated as well.
        """);
}
