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
    }
}
