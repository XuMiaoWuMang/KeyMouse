using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 滚轮的两个自由度：方向（上下 / 左右）与合并规则。
/// 横向滚轮不是"给 wheel 加个负号"——它是另一条命令（mouse hwheel），所以要有一个轴字段。
/// </summary>
internal static class WheelTests
{
    public static void Run()
    {
        Harness.Group("wheel: 竖向默认，横向单算");

        var vertical = new FlowStep
        {
            Type = "wheel",
            Delta = 240,
            At = new FlowPoint { Space = "client", X = 10, Y = 20 },
            Target = new FlowTarget { Process = "notepad" },
        };
        Harness.Sequence("不写 axis 就是竖向",
            ["mouse", "wheel", "240", "-wx", "10", "-wy", "20", "--process", "notepad"],
            FlowDocument.ToArguments(vertical));

        vertical.Axis = "vertical";
        Harness.Sequence("写 axis=vertical 也一样",
            ["mouse", "wheel", "240", "-wx", "10", "-wy", "20", "--process", "notepad"],
            FlowDocument.ToArguments(vertical));

        vertical.Axis = "horizontal";
        Harness.Sequence("axis=horizontal 走 mouse hwheel",
            ["mouse", "hwheel", "240", "-wx", "10", "-wy", "20", "--process", "notepad"],
            FlowDocument.ToArguments(vertical));

        Harness.Throws<CommandFailure>("axis 写错会被拒", () =>
        {
            var bad = new FlowStep { Type = "wheel", Delta = 120, Axis = "diagonal" };
            FlowDocument.ToArguments(bad);
        });
    }
}
