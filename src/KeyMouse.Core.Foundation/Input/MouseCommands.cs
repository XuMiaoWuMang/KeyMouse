using static KeyMouse.Runtime.CommandLine;

namespace KeyMouse.Input;

internal static class MouseCommands
{
    internal static int Run(string[] args, GlobalOptions g)
    {
        if (args.Length == 0) return Fail(2, "mouse：需要一个子命令");
        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "x", "y", "n", "i", "button", "steps", "duration", "delta");
        string button = opt.GetValueOrDefault("button") ?? (pos.Count > 0 ? pos[0] : "left");
        (int X, int Y)? Absolute()
        {
            bool x = opt.ContainsKey("x"), y = opt.ContainsKey("y");
            if (x != y) throw new CommandFailure(2, "-x 和 -y 必须成对出现");
            return x ? (IntArg(opt["x"], "x"), IntArg(opt["y"], "y")) : null;
        }
        switch (cmd)
        {
            case "pos":
                var p1 = NativeInput.GetCursor();
                CommandOutput.Out.WriteLine($"{p1.X},{p1.Y}");
                return 0;
            case "moveby":
                {
                    if (pos.Count != 2) return Fail(2, "用法：KeyMouse mouse moveby <dx> <dy>");
                    int dx = IntArg(pos[0], "dx"), dy = IntArg(pos[1], "dy");
                    NativeInput.MoveBy(dx, dy);
                    if (ExecutionMode.DryRun) { CommandOutput.Out.WriteLine($"试运行：相对移动 {dx},{dy}"); return 0; }
                    var p = NativeInput.GetCursor();
                    CommandOutput.Out.WriteLine($"已相对移动 {dx},{dy} → {p.X},{p.Y}");
                    return 0;
                }

            case "move":
                if (g.Wx.HasValue || g.Wy.HasValue) return Move(g, Absolute());
                if (pos.Count != 2) return Fail(2, "用法：KeyMouse mouse move <x> <y>");
                return Move(g, (IntArg(pos[0], "x"), IntArg(pos[1], "y")));
            case "click" or "dblclick":
                return Click(g, Absolute(), button, cmd == "dblclick" ? 2 : IntOr(opt, "n", 1), IntOr(opt, "i", 80));
            case "down" or "up": return Button(g, button, cmd == "up");
            case "wheel" or "hwheel":
                if (pos.Count == 0 && !opt.ContainsKey("delta")) return Fail(2, $"mouse {cmd} 需要增量");
                return Wheel(g, Absolute(), IntArg(pos.Count > 0 ? pos[0] : opt["delta"], "delta"), cmd == "hwheel");
            case "drag":
                bool relative = g.Wx.HasValue || g.Wy.HasValue || g.Wx2.HasValue || g.Wy2.HasValue;
                if (relative && pos.Count != 0) return Fail(2, "四个坐标和四个客户区标志只能给一套，不能混用");
                if (!relative && pos.Count != 4) return Fail(2, "用法：KeyMouse mouse drag <x1> <y1> <x2> <y2>");
                return Drag(g, relative ? null : (IntArg(pos[0], "x1"), IntArg(pos[1], "y1")),
                    relative ? null : (IntArg(pos[2], "x2"), IntArg(pos[3], "y2")),
                    opt.GetValueOrDefault("button") ?? "left", IntOr(opt, "steps", 20), IntOr(opt, "duration", 400));
            default: return Fail(2, $"mouse：未知子命令 '{cmd}'");
        }
    }

    private static (int X, int Y)? Resolve(GlobalOptions g, (int X, int Y)? absolute, string command)
    {
        if (absolute != null && (g.Wx.HasValue || g.Wy.HasValue))
            throw new CommandFailure(2, "客户区坐标（-wx/-wy）和绝对坐标（-x/-y）只能给一套，不能混用");
        return CommandTarget.RelativePoint(g, CommandTarget.PrepareTarget(g), command) ?? absolute;
    }

    internal static int Move(GlobalOptions g, (int X, int Y)? point)
    {
        var p = Resolve(g, point, "mouse move") ?? throw new CommandFailure(2, "move 步骤需要 at");
        NativeInput.MoveTo(p.X, p.Y);
        CommandOutput.Out.WriteLine($"已移动到 {p.X},{p.Y}");
        return 0;
    }

    internal static int Click(GlobalOptions g, (int X, int Y)? point, string button, int count = 1, int interval = 80)
    {
        NativeInput.ValidateButton(button);
        if (Resolve(g, point, "mouse click") is { } pt) NativeInput.MoveTo(pt.X, pt.Y);
        NativeInput.Click(button, count, interval);
        if (ExecutionMode.DryRun) { CommandOutput.Out.WriteLine($"试运行：点击 {button} ×{count}"); return 0; }
        var now = NativeInput.GetCursor();
        CommandOutput.Out.WriteLine($"已点击 {button}{(count > 1 ? $" ×{count}" : "")}，位置 {now.X},{now.Y}");
        return 0;
    }

    internal static int Button(GlobalOptions g, string button, bool up)
    {
        CommandTarget.PrepareTarget(g);
        if (up) NativeInput.ButtonUp(button); else NativeInput.ButtonDown(button);
        CommandOutput.Out.WriteLine($"鼠标 {button} 已{(up ? "松开" : "按下")}");
        return 0;
    }

    internal static int Wheel(GlobalOptions g, (int X, int Y)? point, int delta, bool horizontal)
    {
        if (Resolve(g, point, "mouse wheel") is { } pt) NativeInput.MoveTo(pt.X, pt.Y);
        NativeInput.Wheel(delta, horizontal);
        CommandOutput.Out.WriteLine($"滚轮{(horizontal ? "（横向）" : "（纵向）")} 增量 {delta}");
        return 0;
    }

    internal static int Drag(GlobalOptions g, (int X, int Y)? from, (int X, int Y)? to, string button, int steps, int duration)
    {
        NativeInput.ValidateButton(button);
        bool relative = from == null || to == null;
        if (relative && (g.Wx == null || g.Wy == null || g.Wx2 == null || g.Wy2 == null))
            return Fail(2, "相对拖拽需要 -wx -wy --wx2 --wy2 四个都给");
        var target = CommandTarget.PrepareTarget(g);
        if (relative)
        {
            if (target == null) return Fail(2, "mouse drag：-wx/-wy 需要窗口选择器");
            from = CommandTarget.ClientPoint(target, g.Wx!.Value, g.Wy!.Value, "drag start");
            to = CommandTarget.ClientPoint(target, g.Wx2!.Value, g.Wy2!.Value, "drag end");
            if (g.StrictPoint)
            {
                CommandTarget.RequirePointBelongsToTarget(target, from.Value.X, from.Value.Y, "drag start");
                CommandTarget.RequirePointBelongsToTarget(target, to.Value.X, to.Value.Y, "drag end");
            }
        }
        var start = from!.Value; var end = to!.Value;
        NativeInput.Drag(start.X, start.Y, end.X, end.Y, button, steps, duration);
        CommandOutput.Out.WriteLine($"已从 {start.X},{start.Y} 拖到 {end.X},{end.Y}，用 {button} 键");
        return 0;
    }
}
