using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 时间线：录制把每一步的"从开始算第几毫秒"写进 atMs，回放据此排程。
/// 没有它，快速挥动会被匀速慢慢放出来（因为回放只能靠 sleep 表达 ≥ minGap 的停顿）。
/// </summary>
internal static class TimelineTests
{
    private static Recorder.Session Session() => new(
        Path.Combine(Path.GetTempPath(), "keymouse-timeline-test.json"),
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
