using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 新录制将停顿保存在对应操作上，旧文件仍按绝对时间线运行。
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
        CompareRecordingModes();
        CheckReplayDelays();
        CheckTextGaps();
        Harness.Group("timeline: 完全录制写出相对等待");

        Recorder.Session session = Session();
        uint started = session.StartedAt;
        session.OnEvent(Move(100, 100, started + 100));
        session.OnEvent(Move(200, 100, started + 150));
        session.OnEvent(Move(300, 100, started + 400));
        session.Finish();

        List<FlowStep> moves = session.Steps.Where(s => s.Type == "move").ToList();
        Harness.Equal("三个点都留下（每个都走了 100 像素）", 3, moves.Count);
        Harness.Equal("从起录到第一步", 100, moves[0].DelayMs);
        Harness.Equal("第二步等待两次移动的间隔", 50, moves[1].DelayMs);
        Harness.Equal("第三步等待两次移动的间隔", 250, moves[2].DelayMs);

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
        Harness.Equal("总时长包括三个移动前的等待", 400, recorded.DurationMs);

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

        Harness.Equal("ToArguments 不会把 atMs 当成命令行参数", 4,
            FlowCommandDisplay.Tokens(loaded.Steps[0]).Count());
    }

    private static void CheckReplayDelays()
    {
        // Real scheduling, fake dispatch: each row includes the wait before its action.
        var run = FlowRun.Run(FlowRun.Flow("""[{"type":"key","combo":"a"},{"type":"key","combo":"b","delayMs":40}]"""));
        Harness.Check("缺省与自定义等待都发生在各自操作内", run.ExitCode == 0 && run.Report?.Commands is
            [{DurationMs: >= 1000}, {DurationMs: >= 40}]);
        var dry = FlowRun.Run(FlowRun.Flow("""[{"type":"key","combo":"a","delayMs":5000}]"""), "--dry-run");
        Harness.Check("干跑跳过步骤等待", dry.ExitCode == 0 && dry.Report?.Commands is [{DurationMs: < 5000}]);
        var legacy = FlowRun.Run(FlowRun.Flow("""[{"type":"sleep","atMs":1,"ms":900},{"type":"sleep","atMs":1500,"ms":7,"delayMs":0}]"""));
        Harness.Check("旧时间戳与显式等待互斥", legacy.Report?.Commands is [{Output: "等待 0 ms"}, {Output: "等待 7 ms"}]);
    }

    private static void CheckTextGaps()
    {
        string path = Path.Combine(Path.GetTempPath(), $"km-type-{Guid.NewGuid():N}.json");
        try
        {
            var session = new Recorder.Session(path, 300, 8, false, 0);
            void Type(char c, int offset) => session.OnEvent(new HookEvent(HookKind.KeyDown, 0xE7, 0, 0, c,
                unchecked(session.StartedAt + (uint)offset), IntPtr.Zero));
            Type('a', 0); Type('b', 100); Type('c', 1500); Type('d', 1500);
            session.Finish();
            Harness.Check("动作内保留字符间隔，长停顿写入下一步，同刻输入间隔为零", FlowDocument.Load(path).Steps is
                [{Text: "ab", DelayMs: 0, IntervalMs: 100}, {Text: "cd", DelayMs: 1400, IntervalMs: 0}]);
        }
        finally { File.Delete(path); }
    }

    private static void CompareRecordingModes()
    {
        Harness.Group("record: 同一段操作按任务或按时间线记录");
        foreach (var mode in new[] { Recorder.Mode.Key, Recorder.Mode.Full })
        {
            string path = Path.Combine(Path.GetTempPath(), $"km-record-{Guid.NewGuid():N}.json");
            try
            {
                var s = new Recorder.Session(path, 300, 8, false, 0, mode);
                void Feed(HookKind kind, uint ms, int x = 0, int data = 0, int key = 0) =>
                    s.OnEvent(new HookEvent(kind, key, x, 30, data, s.StartedAt + ms, IntPtr.Zero));
                Feed(HookKind.MouseMove, 5, 15);
                Feed(HookKind.KeyDown, 50, data: '甲', key: 0xE7);
                Feed(HookKind.MouseMove, 100, 100);
                Feed(HookKind.KeyDown, 5050, data: '乙', key: 0xE7);
                Feed(HookKind.ButtonDown, 5100, 20, 2);
                Feed(HookKind.ButtonUp, 5150, 20, 2);
                Feed(HookKind.ButtonDown, 5200, 25, 1);
                Feed(HookKind.MouseMove, 5300, 40);
                Feed(HookKind.ButtonUp, 5400, 80, 1);
                Feed(HookKind.ButtonDown, 5500, 80, 1);
                Feed(HookKind.ButtonUp, 6100, 80, 1);
                Feed(HookKind.HWheel, 6200, 80, -120);
                Feed(HookKind.KeyDown, 6300, key: 0x11);
                Feed(HookKind.KeyDown, 6350, key: 0x53);
                Feed(HookKind.KeyUp, 6660, key: 0x53);
                Feed(HookKind.KeyUp, 6700, key: 0x11);
                Feed(HookKind.KeyDown, 6800, key: 0x70);
                Feed(HookKind.KeyUp, 7300, key: 0x70);
                Feed(HookKind.KeyDown, 7400, key: 0x71);
                Feed(HookKind.KeyUp, 8100, key: 0x71);
                s.Finish();
                var doc = FlowDocument.Load(path);
                if (mode == Recorder.Mode.Full)
                {
                    Harness.Check("完全录制保留移动与总时长，各步直接带等待", doc.Steps.Any(x => x.Type == "move") && doc.Steps.All(x => x is {DelayMs: >= 0, AtMs: null} && x.Type != "sleep") && doc.DurationMs == 8100);
                    Harness.Equal("拖动结束到长按开始", 100, doc.Steps.Single(x => x.Type == "mousehold").DelayMs);
                    Harness.Equal("长按结束到滚轮", 100, doc.Steps.Single(x => x.Type == "wheel").DelayMs);
                    Harness.Equal("按下到松开", 700, doc.Steps[^1].DelayMs);
                    continue;
                }
                Harness.Equal("关键事件顺序", "type,click,drag,mousehold,wheel,key,keyhold,keyhold", string.Join(",", doc.Steps.Select(x => x.Type)));
                Harness.Check("关键动作逐步使用默认等待", doc.DurationMs is null && doc.Steps.All(x => x is {DelayMs: 1000, AtMs: null}));
                Harness.Check("空移不切断中文输入", doc.Steps[0] is {Text: "甲乙", IntervalMs: null});
                Harness.Check("右击 / 拖动 / 鼠标长按 / 横向滚动", doc.Steps[1].Button == "right" && doc.Steps[2] is {From.X: 25, To.X: 80} && doc.Steps[3].Ms == 600 && doc.Steps[4] is {Axis: "horizontal", Delta: -120});
                Harness.Check("快捷键只触发一次，独立长按保留各自时长", doc.Steps[5].Combo!.StartsWith("ctrl+") && doc.Steps[6].Ms == 500 && doc.Steps[7].Ms == 700);
            }
            finally { File.Delete(path); }
        }
    }
}
