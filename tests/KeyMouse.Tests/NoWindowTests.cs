using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 默认不要求窗口：没有 target 的步骤就是"把事件发到屏幕位置 / 当前焦点"，
/// 不校验窗口、不抢前台。窗口变成可选（客户区坐标仍然需要它——那是算术，不是策略）。
/// </summary>
internal static class NoWindowTests
{
    public static void Run()
    {
        Harness.Group("nowindow: 没有窗口也能发事件");

        var click = new FlowStep
        {
            Type = "click",
            Button = "left",
            At = new FlowPoint { Space = "screen", X = 640, Y = 480 },
        };
        Harness.Sequence("屏幕坐标的点击不带选择器",
            ["mouse", "click", "left", "-x", "640", "-y", "480"],
            FlowDocument.ToArguments(click));

        var drag = new FlowStep
        {
            Type = "drag",
            From = new FlowPoint { Space = "screen", X = 10, Y = 20 },
            To = new FlowPoint { Space = "screen", X = 30, Y = 40 },
        };
        Harness.Sequence("屏幕坐标的拖拽也不需要窗口",
            ["mouse", "drag", "10", "20", "30", "40"],
            FlowDocument.ToArguments(drag));

        var clientDrag = new FlowStep
        {
            Type = "drag",
            From = new FlowPoint { Space = "client", X = 10, Y = 20 },
            To = new FlowPoint { Space = "client", X = 30, Y = 40 },
        };
        Harness.Throws<CommandFailure>("客户区拖拽没有窗口会被拒（没法换算）",
            () => FlowDocument.ToArguments(clientDrag));

        var typing = new FlowStep { Type = "type", Text = "hi" };
        Harness.Sequence("不指定窗口的打字就发给当前焦点",
            ["key", "type", "hi"],
            FlowDocument.ToArguments(typing));

        var document = new FlowDocument
        {
            Steps =
            [
                click,
                drag,
                typing,
            ],
        };
        Harness.Check("整篇没有一个窗口，也能通过加载与编译",
            FlowPlan.Build(document, Path.Combine(Path.GetTempPath(), "keymouse-nowindow-test.json")).StepCount == 3);

        // 读屏必须落在某个窗口里：区域允许 client / window，不允许 screen。
        Harness.Group("nowindow: 读屏区域的空间");

        FlowStep Reading(string space) => new()
        {
            Type = "wait-text",
            Text = "完成",
            Target = new FlowTarget { Process = "notepad" },
            Region = new FlowRegion { Space = space, X = 0, Y = 0, Width = 100, Height = 20 },
        };

        // 校验发生在加载时（FlowDocument.Load），所以这里走一遍真实的 存盘 → 加载。
        string Load(FlowStep step)
        {
            string path = Path.Combine(Path.GetTempPath(), "keymouse-region-" + step.Region!.Space + ".json");
            new FlowDocument { Steps = [step] }.Save(path);
            return FlowDocument.Load(path).Steps[0].Region!.Space;
        }

        Harness.Equal("region 可以是整个窗口（标题栏那类也能读）", "window", Load(Reading("window")));
        Harness.Equal("region 默认还是客户区", "client", Load(Reading("client")));
        Harness.Throws<CommandFailure>("region 写 screen 会被拒（没有窗口就没有可比对的坐标系）",
            () => Load(Reading("screen")));
    }
}
