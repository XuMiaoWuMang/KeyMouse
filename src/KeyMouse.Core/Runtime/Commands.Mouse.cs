namespace KeyMouse.Runtime;

/// <summary>
/// 鼠标：move / click / drag / wheel，以及客户区坐标到屏幕坐标的换算。
/// </summary>
internal static partial class Commands
{

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

}
