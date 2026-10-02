using System.Globalization;

namespace KeyMouse;

internal static class Program
{
    private const string Version = "1.0.0";

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* no console attached */ }

        try
        {
            if (args.Length == 0) { PrintUsage(); return 2; }

            switch (args[0].ToLowerInvariant())
            {
                case "-h" or "--help" or "help" or "/?":
                    PrintUsage();
                    return 0;
                case "-v" or "--version" or "version":
                    Console.WriteLine($"KeyMouse {Version}");
                    return 0;
                case "mouse":
                    return Mouse(args[1..]);
                case "key" or "keyboard":
                    return Keyboard(args[1..]);
                default:
                    return Fail($"unknown group '{args[0]}' (expected: mouse | key | help)");
            }
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    // ---------------------------------------------------------------- mouse

    private static int Mouse(string[] args)
    {
        if (args.Length == 0)
            return Fail("mouse: expected a command (move | moveby | click | dblclick | down | up | wheel | hwheel | drag | pos)");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "x", "y", "n", "i", "button", "steps", "duration", "delta");

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
                if (pos.Count != 2) return Fail("usage: KeyMouse mouse move <x> <y>");
                int x = IntArg(pos[0], "x"), y = IntArg(pos[1], "y");
                NativeInput.MoveTo(x, y);
                var p = NativeInput.GetCursor();
                Console.WriteLine($"moved to {p.X},{p.Y}");
                return 0;
            }

            case "moveby":
            {
                if (pos.Count != 2) return Fail("usage: KeyMouse mouse moveby <dx> <dy>");
                int dx = IntArg(pos[0], "dx"), dy = IntArg(pos[1], "dy");
                NativeInput.MoveBy(dx, dy);
                var p = NativeInput.GetCursor();
                Console.WriteLine($"moved by {dx},{dy} -> {p.X},{p.Y}");
                return 0;
            }

            case "click":
            case "dblclick":
            {
                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                int count = cmd == "dblclick" ? 2 : IntOr(opt, "n", 1);
                int interval = IntOr(opt, "i", 80);
                int? tx = opt.ContainsKey("x") ? IntArg(opt["x"], "x") : null;
                int? ty = opt.ContainsKey("y") ? IntArg(opt["y"], "y") : null;
                if (tx.HasValue || ty.HasValue)
                {
                    if (tx.HasValue && ty.HasValue) NativeInput.MoveTo(tx.Value, ty.Value);
                    else return Fail("-x and -y must be given together");
                }

                NativeInput.Click(button, count, interval);
                var p = NativeInput.GetCursor();
                Console.WriteLine($"clicked {button}{(count > 1 ? $" x{count}" : "")} at {p.X},{p.Y}");
                return 0;
            }

            case "down":
            {
                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                NativeInput.ButtonDown(button);
                Console.WriteLine($"{button} down");
                return 0;
            }

            case "up":
            {
                string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
                NativeInput.ButtonUp(button);
                Console.WriteLine($"{button} up");
                return 0;
            }

            case "wheel":
            case "hwheel":
            {
                if (pos.Count == 0 && !opt.ContainsKey("delta"))
                    return Fail($"usage: KeyMouse mouse {cmd} <delta> [-x X -y Y]   (120 = one notch, + = up/right)");
                int delta = pos.Count > 0 ? IntArg(pos[0], "delta") : IntArg(opt["delta"], "delta");
                int? tx = opt.ContainsKey("x") ? IntArg(opt["x"], "x") : null;
                int? ty = opt.ContainsKey("y") ? IntArg(opt["y"], "y") : null;
                if (tx.HasValue || ty.HasValue)
                {
                    if (tx.HasValue && ty.HasValue) NativeInput.MoveTo(tx.Value, ty.Value);
                    else return Fail("-x and -y must be given together");
                }

                NativeInput.Wheel(delta, cmd == "hwheel");
                Console.WriteLine($"wheel {(cmd == "hwheel" ? "h" : "v")} delta {delta}");
                return 0;
            }

            case "drag":
            {
                if (pos.Count != 4)
                    return Fail("usage: KeyMouse mouse drag <x1> <y1> <x2> <y2> [--button left] [--steps 20] [--duration 400]");
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
                return Fail($"mouse: unknown command '{cmd}'");
        }
    }

    // ------------------------------------------------------------- keyboard

    private static int Keyboard(string[] args)
    {
        if (args.Length == 0)
            return Fail("key: expected a command (press | down | up | combo | type)");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "n", "i", "interval", "hold");

        switch (cmd)
        {
            case "press":
            case "tap":
            {
                if (pos.Count != 1) return Fail("usage: KeyMouse key press <key> [-n COUNT] [-i MS]");
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
                if (pos.Count != 1) return Fail("usage: KeyMouse key down <key>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, false);
                Console.WriteLine($"{pos[0]} down");
                return 0;
            }

            case "up":
            {
                if (pos.Count != 1) return Fail("usage: KeyMouse key up <key>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, true);
                Console.WriteLine($"{pos[0]} up");
                return 0;
            }

            case "combo":
            {
                if (pos.Count != 1) return Fail("usage: KeyMouse key combo <k1+k2+...>   e.g. ctrl+shift+s");
                var keys = new List<(ushort Vk, bool Ext)>();
                foreach (string part in pos[0].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    keys.Add(KeyMap.Resolve(part));

                // modifiers go down first, everything is released in reverse order
                keys = keys.OrderBy(k => IsModifier(k.Vk) ? 0 : 1).ToList();
                NativeInput.Chord(keys, IntOr(opt, "hold", 30));
                Console.WriteLine("combo " + string.Join("+", keys.Select(k => $"0x{k.Vk:X2}")));
                return 0;
            }

            case "type":
            {
                if (pos.Count == 0) return Fail("usage: KeyMouse key type <text> [--interval MS]");
                string text = string.Join(' ', pos);
                NativeInput.TypeText(text, IntOr(opt, "interval", IntOr(opt, "i", 15)));
                Console.WriteLine($"typed {text.Length} chars");
                return 0;
            }

            default:
                return Fail($"key: unknown command '{cmd}'");
        }
    }

    // -------------------------------------------------------------- helpers

    private static bool IsModifier(ushort vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;

    /// <summary>Splits argv into positional values and -flag/--flag[=value] options.</summary>
    private static (List<string> Positional, Dictionary<string, string> Options) Parse(string[] args, params string[] valueFlags)
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

    private static int Fail(string message)
    {
        Console.Error.WriteLine("error: " + message);
        return 2;
    }

    private static void PrintUsage() => Console.WriteLine($"""
        KeyMouse {Version} - inject real mouse & keyboard events on Windows (one shot, no polling, no daemon)

        USAGE
          KeyMouse <group> <command> [arguments] [options]

        MOUSE
          mouse pos                                   print current cursor position
          mouse move <x> <y>                          move cursor to absolute screen pixel
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
                                                      default 15ms/char; use --interval 0 for max speed

        KEYS
          a-z  0-9  f1-f24  esc enter tab space backspace delete insert home end
          pageup pagedown up down left right ctrl shift alt win rwin apps capslock
          num0-num9 numadd numsub nummul numdiv numdecimal numenter numlock
          printscreen pause  semicolon equals comma minus period slash grave
          lbracket rbracket backslash quote  vk:0x5B (raw virtual-key escape hatch)

        EXAMPLES
          KeyMouse mouse move 100 200
          KeyMouse mouse click right -x 640 -y 480
          KeyMouse mouse click left -n 2 -i 60
          KeyMouse mouse drag 400 300 900 300 --duration 600
          KeyMouse mouse wheel -120 -x 1200 -y 600
          KeyMouse key combo ctrl+shift+s
          KeyMouse key type "hello 世界"
          KeyMouse key press enter

        EXIT CODES
          0 = ok   1 = runtime failure (e.g. SendInput blocked)   2 = usage error

        NOTES
          * The event lands on whatever window has focus / is under the cursor: focus the
            target first (this is exactly what a human does).
          * SendInput is blocked by UIPI when the target window is elevated - run KeyMouse
            elevated as well in that case.
        """);
}
