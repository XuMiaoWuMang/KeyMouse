using System.Globalization;

namespace KeyMouse.Flow;

/// <summary>CLI-style spelling for reports, not an execution format.</summary>
internal static class FlowCommandDisplay
{
    internal static string[] Tokens(FlowStep step)
    {
        string[] selector = step.Target?.Selector() ?? [];
        var args = new List<string>();
        switch (step.Type)
        {
            case "focus":
                args.AddRange(["window", "focus"]);
                args.AddRange(selector);
                break;

            case "click":
                args.AddRange(["mouse", "click", step.Button ?? "left"]);
                args.AddRange(PointArguments(step, step.At, "-x", "-y", "-wx", "-wy"));
                args.AddRange(selector);
                break;

            case "move":
                if (step.At is null) throw new CommandFailure(2, "move 步骤需要 at");
                args.Add("mouse");
                args.Add("move");
                if (step.At.Space == FlowSpace.Client)
                {
                    RequireTarget(step);
                    args.AddRange(["-wx", Int(step.At.X), "-wy", Int(step.At.Y)]);
                }
                else
                {
                    args.AddRange([Int(step.At.X), Int(step.At.Y)]); // mouse move takes x y positionally
                }
                args.AddRange(selector);
                break;

            case "wheel":
                if (step.Axis is { Length: > 0 } unknown &&
                    !string.Equals(unknown, FlowAxis.Vertical, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(unknown, FlowAxis.Horizontal, StringComparison.OrdinalIgnoreCase))
                    throw new CommandFailure(2, $"wheel 的 axis '{unknown}' 不认识（可用：{FlowAxis.Vertical} | {FlowAxis.Horizontal}）");

                // 横向滚轮是另一条命令（mouse hwheel），不是给 wheel 加个负号。
                bool horizontal = string.Equals(step.Axis, FlowAxis.Horizontal, StringComparison.OrdinalIgnoreCase);
                args.AddRange(["mouse", horizontal ? "hwheel" : "wheel", (step.Delta ?? 120).ToString(CultureInfo.InvariantCulture)]);
                args.AddRange(PointArguments(step, step.At, "-x", "-y", "-wx", "-wy"));
                args.AddRange(selector);
                break;

            case "drag":
                if (step.From is null || step.To is null) throw new CommandFailure(2, "drag 步骤需要 from 与 to");
                if (step.From.Space != step.To.Space)
                    throw new CommandFailure(2, "drag 的起点与终点必须在同一个坐标空间");

                if (step.From.Space == FlowSpace.Client)
                {
                    RequireTarget(step);
                    args.AddRange(["mouse", "drag",
                        "-wx", step.From.X.ToString(CultureInfo.InvariantCulture),
                        "-wy", step.From.Y.ToString(CultureInfo.InvariantCulture),
                        "--wx2", step.To.X.ToString(CultureInfo.InvariantCulture),
                        "--wy2", step.To.Y.ToString(CultureInfo.InvariantCulture)]);
                }
                else
                {
                    // 屏幕坐标的拖拽不需要窗口：从这儿拖到那儿，跟当前谁在前台无关。
                    args.AddRange(["mouse", "drag",
                        step.From.X.ToString(CultureInfo.InvariantCulture),
                        step.From.Y.ToString(CultureInfo.InvariantCulture),
                        step.To.X.ToString(CultureInfo.InvariantCulture),
                        step.To.Y.ToString(CultureInfo.InvariantCulture)]);
                }
                if (step.Button is { Length: > 0 } button && button != "left") args.AddRange(["--button", button]);
                if (step.DurationMs is int duration)
                {
                    args.AddRange(["--duration", duration.ToString(CultureInfo.InvariantCulture)]);
                }
                args.AddRange(selector);
                break;

            case "type":
                args.AddRange(["key", "type", step.Text ?? ""]);
                if (step.IntervalMs is int interval)
                {
                    args.AddRange(["--interval", interval.ToString(CultureInfo.InvariantCulture)]);
                }
                args.AddRange(selector);
                break;

            case "key":
                // 单键也走 combo：文件里只有一个按键字段，CLI 的 combo 解析器接受单键。
                args.AddRange(["key", "combo", step.Combo ?? step.Text ?? ""]);
                args.AddRange(selector);
                break;

            // 鼠标长按：按下 / 抬起（位置取按下的那一点）。
            case "mousedown":
                args.AddRange(["mouse", "down", "-button", step.Button ?? "left"]);
                args.AddRange(selector);
                break;

            case "mouseup":
                args.AddRange(["mouse", "up", "-button", step.Button ?? "left"]);
                args.AddRange(selector);
                break;

            // 长按的两半：按下 / 抬起。回放时中间隔着时间线，就是"按住了这么久"。
            case "keydown":
                args.AddRange(["key", "down", step.Combo ?? step.Text ?? ""]);
                args.AddRange(selector);
                break;

            case "keyup":
                args.AddRange(["key", "up", step.Combo ?? step.Text ?? ""]);
                args.AddRange(selector);
                break;


            default:
                // 执行器自己跑的类型（sleep、长按、循环…）没有对应的 CLI 命令：返回空数组，
                // 调用方据此跳过发命令，而不是把它当成错误（长按刚踩过这个坑）。
                if (FlowDocument.RunnerTypes.Contains(step.Type)) return [];
                throw new CommandFailure(2, $"'{step.Type}' 不认识");
        }

        return args.ToArray();
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void RequireTarget(FlowStep step)
    {
        if (step.Target is null || step.Target.Selector().Length == 0)
        {
            throw new CommandFailure(2, $"'{step.Type}' 用客户区坐标时必须带 target（窗口选择器），否则坐标无从换算");
        }
    }

    private static IEnumerable<string> PointArguments(
        FlowStep step, FlowPoint? point, string? absoluteX, string? absoluteY, string relativeX, string relativeY)
    {
        if (point is null) return [];
        if (point.Space == FlowSpace.Client)
        {
            RequireTarget(step);
            return [relativeX, Int(point.X), relativeY, Int(point.Y)];
        }
        if (point.Space != FlowSpace.Screen)
        {
            throw new CommandFailure(2, $"'{step.Type}' 的坐标空间 '{point.Space}' 不认识（client | screen）");
        }
        if (absoluteX is null || absoluteY is null)
        {
            throw new CommandFailure(2, $"'{step.Type}' 只支持客户区坐标（space=client）：屏幕坐标会因为窗口移动而失效");
        }
        return [absoluteX, Int(point.X), absoluteY, Int(point.Y)];
    }
}
