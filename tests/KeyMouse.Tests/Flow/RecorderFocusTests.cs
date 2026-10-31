using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 录制收尾时补的那一步 `focus`。
///
/// 它不是"顺手加的一行"，而是回放能不能落在正确窗口上的前提：录制只写坐标、
/// 不绑窗口，目标窗口被压着的时候第一下点击就打在别人身上（实测反馈）。
/// </summary>
internal static class RecorderFocusTests
{
    internal static void Run()
    {
        NoStartTargetKeepsStepsAsIs();
        StartTargetPrependsFocus();
        ExistingFocusIsNotDuplicated();
        EmptyRecordingStillGetsFocus();
        InputListIsNotMutated();
    }

    private static void NoStartTargetKeepsStepsAsIs()
    {
        var steps = new List<FlowStep> { new() { Type = "click", AtMs = 10 } };
        var saved = Recorder.WithLeadingFocus(steps, null);
        Harness.Check("没有起始窗口就不补 focus", ReferenceEquals(saved, steps));
    }

    private static void StartTargetPrependsFocus()
    {
        var steps = new List<FlowStep>
        {
            new() { Type = "click", AtMs = 6250 },
            new() { Type = "type", Text = "hello", AtMs = 7000 },
        };
        var saved = Recorder.WithLeadingFocus(steps, new FlowTarget { Process = "notepad" });

        Harness.Check("补出来的第一步是 focus", saved.Count == 3 && saved[0].Type == "focus");
        Harness.Check("focus 指向录制时的那个进程", saved[0].Target?.Process == "notepad");
        Harness.Check("focus 在时间线的第 0 毫秒", saved[0].AtMs == 0);
        Harness.Check("原来的步骤原样跟在后面", saved[1].AtMs == 6250 && saved[2].Text == "hello");
    }

    private static void ExistingFocusIsNotDuplicated()
    {
        var steps = new List<FlowStep>
        {
            new() { Type = "focus", Target = new FlowTarget { Process = "notepad" }, AtMs = 0 },
            new() { Type = "click", AtMs = 100 },
        };
        var saved = Recorder.WithLeadingFocus(steps, new FlowTarget { Process = "notepad" });
        Harness.Check("已经是 focus 开头就不再补一条", ReferenceEquals(saved, steps));
    }

    private static void EmptyRecordingStillGetsFocus()
    {
        var saved = Recorder.WithLeadingFocus(new List<FlowStep>(), new FlowTarget { Process = "calc" });
        Harness.Check("一步都没录到时也记得先切窗口", saved.Count == 1 && saved[0].Type == "focus");
    }

    private static void InputListIsNotMutated()
    {
        var steps = new List<FlowStep> { new() { Type = "click", AtMs = 10 } };
        Recorder.WithLeadingFocus(steps, new FlowTarget { Process = "notepad" });
        Harness.Check("调用方的步骤列表没被偷偷改掉", steps.Count == 1 && steps[0].Type == "click");
    }
}
