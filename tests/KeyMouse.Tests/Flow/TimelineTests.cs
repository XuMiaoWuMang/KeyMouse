using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 时间线：录制把每一步的"从开始算第几毫秒"写进 atMs，回放据此排程。
/// 没有它，快速挥动会被匀速慢慢放出来（因为回放只能靠 sleep 表达 ≥ minGap 的停顿）。
/// </summary>
internal static class TimelineTests
{
    private static Recorder.Session Session(string name = "keymouse-timeline-test") => new(
        Path.Combine(Path.GetTempPath(), name + ".json"),
        300, 8, false, 0);

    private static HookEvent Move(int x, int y, uint time) =>
        new(HookKind.MouseMove, 0, x, y, 0, time, IntPtr.Zero);

    public static void Run()
    {
        Harness.Group("timeline: 录制写出 atMs");

        Recorder.Session session = Session();
        uint started = session.StartedAt;
        session.OnEvent(Move(100, 100, started + 100));
        session.OnEvent(Move(200, 100, started + 150));
        session.OnEvent(Move(300, 100, started + 400));
        session.Finish();

        List<FlowStep> moves = session.Steps.Where(s => s.Type == "move").ToList();
        Harness.Equal("三个点都留下（每个都走了 100 像素）", 3, moves.Count);
        Harness.Equal("第一步的 atMs", 100, moves[0].AtMs);
        Harness.Equal("第二步的 atMs", 150, moves[1].AtMs);
        Harness.Equal("第三步的 atMs", 400, moves[2].AtMs);

        Harness.Group("record: 忽略矩形里的鼠标事件");

        Recorder.Session ignoring = Session("keymouse-ignore-rect-test");
        ignoring.IgnoreRects.Add((100, 100, 50, 50));
        uint t0 = ignoring.StartedAt;
        ignoring.OnEvent(Move(120, 120, t0 + 10));      // 矩形内：丢掉
        ignoring.OnEvent(Move(300, 300, t0 + 20));      // 矩形外：留下
        ignoring.OnEvent(new HookEvent(HookKind.ButtonDown, 0, 120, 120, 1, t0 + 30, IntPtr.Zero));  // 内：丢
        ignoring.OnEvent(new HookEvent(HookKind.ButtonUp, 0, 120, 120, 1, t0 + 40, IntPtr.Zero));    // 内：丢
        ignoring.OnEvent(new HookEvent(HookKind.ButtonDown, 0, 400, 400, 1, t0 + 50, IntPtr.Zero));  // 外：留
        ignoring.OnEvent(new HookEvent(HookKind.ButtonUp, 0, 400, 400, 1, t0 + 60, IntPtr.Zero));    // 外：留
        ignoring.Finish();

        Harness.Equal("矩形内的移动与点击都不进流程", 2,
            ignoring.Steps.Count(s => s.Type is "move" or "click"));
        Harness.Check("留下的是矩形外的那一对",
            ignoring.Steps.Any(s => s.Type == "click" && s.At is { X: 400, Y: 400 }));

        Harness.Group("timeline: 文档记录总时长");

        FlowDocument recorded = FlowDocument.Load(
            Path.Combine(Path.GetTempPath(), "keymouse-timeline-test.json"));
        Harness.Equal("总时长 = 最后一步的 atMs", 400, recorded.DurationMs);

        Harness.Group("timeline: atMs 能存能读");

        string path = Path.Combine(Path.GetTempPath(), "keymouse-timeline-roundtrip.json");
        var document = new FlowDocument
        {
            Steps =
            [
                new FlowStep { Type = "move", At = new FlowPoint { Space = "screen", X = 10, Y = 20 }, AtMs = 1234 },
            ],
        };
        document.Save(path);
        FlowDocument loaded = FlowDocument.Load(path);
        Harness.Equal("atMs 往返不变", 1234, loaded.Steps[0].AtMs);

        Harness.Group("timeline: 手工流程不写 atMs 就不按时间线走");

        Harness.Check("没有 atMs 的步骤视为不受时间线约束", loaded.Steps[0].AtMs is not null);
        Harness.Equal("ToArguments 不会把 atMs 当成命令行参数", 4,
            FlowDocument.ToArguments(loaded.Steps[0]).Count());
    }
}
