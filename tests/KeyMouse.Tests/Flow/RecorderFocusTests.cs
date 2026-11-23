using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 录制只保存实际输入，不自动插入起始聚焦；显式添加的 focus 仍能正常保存和加载。
/// 从 Session 经 Finish 到 FlowDocument.Load 验证实际写出的文件。
/// </summary>
internal static class RecorderFocusTests
{
    internal static void Run()
    {
        ActualInputIsTheFirstStep();
        EmptyRecordingStaysEmpty();
        DiscardedEventsStayDiscarded();
        StoppingKeysStayOutOfTheFlow();
        ExplicitFocusIsPreserved();
    }

    private static void ActualInputIsTheFirstStep()
    {
        foreach (string kind in new[] { "click", "move", "drag", "key", "type", "esc" })
        {
            WithSession((session, path) =>
            {
                uint time = session.StartedAt;
                switch (kind)
                {
                    case "click":
                        session.OnEvent(Event(HookKind.ButtonDown, time + 10, x: 100, y: 200, data: 1));
                        session.OnEvent(Event(HookKind.ButtonUp, time + 30, x: 100, y: 200, data: 1));
                        break;
                    case "move":
                        session.OnEvent(Event(HookKind.MouseMove, time + 10, x: 100, y: 200));
                        break;
                    case "drag":
                        session.OnEvent(Event(HookKind.ButtonDown, time + 10, x: 100, y: 200, data: 1));
                        session.OnEvent(Event(HookKind.MouseMove, time + 40, x: 300, y: 400));
                        session.OnEvent(Event(HookKind.ButtonUp, time + 70, x: 300, y: 400, data: 1));
                        break;
                    case "key":
                        session.OnEvent(Event(HookKind.KeyDown, time + 10, key: 0x0D));
                        session.OnEvent(Event(HookKind.KeyUp, time + 30, key: 0x0D));
                        break;
                    case "type":
                        session.OnEvent(Event(HookKind.KeyDown, time + 10, key: 0xE7, data: 'x'));
                        break;
                    case "esc":
                        session.OnEvent(Event(HookKind.KeyDown, time + 10, key: 0x1B));
                        break;
                }

                session.Finish();
                FlowDocument document = FlowDocument.Load(path);
                Harness.Equal($"{kind} 起录只保存实际的一步输入", 1, document.Steps.Count);
                Harness.Check($"{kind} 起录不添加 focus 或绑定窗口", document.Steps.All(step => step.Type != "focus" && step.Target is null));
                FlowStep? input = document.Steps.FirstOrDefault(step => step.Type != "focus");
                Harness.Equal($"{kind} 的输入类型不变", kind == "esc" ? "key" : kind, input?.Type);
                Harness.Equal($"{kind} 从起录等到动作开始", 10, input?.DelayMs);
                Harness.Equal($"{kind} 的录制时长仅由实际输入决定", kind == "drag" ? 70 : 10, document.DurationMs);
                if (kind is "click" or "move")
                    Harness.Check($"{kind} 保留绝对屏幕落点", input?.At is { Space: "screen", X: 100, Y: 200 });
                if (kind == "drag")
                    Harness.Check("拖动两端与时长完整保留", input is
                    {
                        From: { Space: "screen", X: 100, Y: 200 },
                        To: { Space: "screen", X: 300, Y: 400 }, DurationMs: 60,
                    });
                if (kind == "esc") Harness.Equal("单次 ESC 延迟确认后仍是实际按键", "esc", input?.Combo);
                if (kind == "type") Harness.Equal("文本输入内容不变", "x", input?.Text);
            });
        }
    }

    private static void EmptyRecordingStaysEmpty()
    {
        WithSession((session, path) =>
        {
            session.Finish();
            FlowDocument document = FlowDocument.Load(path);
            Harness.Equal("空录制不产生额外步骤", 0, document.Steps.Count);
            Harness.Equal("空录制时长为零", 0, document.DurationMs);
        });
    }

    private static void DiscardedEventsStayDiscarded()
    {
        foreach (var item in new[]
        {
            (Name: "未配对鼠标抬起", Kind: HookKind.ButtonUp, Key: 0, Data: 1),
            (Name: "控制字符 packet", Kind: HookKind.KeyDown, Key: 0xE7, Data: (int)'\n'),
            (Name: "单独修饰键", Kind: HookKind.KeyDown, Key: 0xA2, Data: 0),
            (Name: "未配对键盘抬起", Kind: HookKind.KeyUp, Key: 0x0D, Data: 0),
        })
        {
            WithSession((session, path) =>
            {
                session.OnEvent(Event(item.Kind, session.StartedAt + 10, key: item.Key, data: item.Data));
                session.Finish();
                Harness.Equal($"{item.Name} 不生成输入或自动 focus", 0, FlowDocument.Load(path).Steps.Count);
            });
        }
    }

    private static void StoppingKeysStayOutOfTheFlow()
    {
        foreach (bool doubleEscape in new[] { false, true })
        {
            WithSession((session, path) =>
            {
                uint time = session.StartedAt;
                if (doubleEscape)
                {
                    session.OnEvent(Event(HookKind.KeyDown, time + 10, key: 0x1B));
                    session.OnEvent(Event(HookKind.KeyDown, time + 20, key: 0x1B));
                }
                else
                {
                    session.OnEvent(Event(HookKind.KeyDown, time + 10, key: 0xA2));
                    session.OnEvent(Event(HookKind.KeyDown, time + 20, key: 0xA4));
                    session.OnEvent(Event(HookKind.KeyDown, time + 30, key: 0x51));
                }
                session.Finish();
                Harness.Check($"{(doubleEscape ? "双 ESC" : "Ctrl+Alt+Q")} 仍能停止且不生成输入或 focus",
                    session.ShouldStop() && FlowDocument.Load(path).Steps.Count == 0);
            });
        }
    }

    private static void ExplicitFocusIsPreserved()
    {
        WithSession((session, path) =>
        {
            var focus = new FlowStep { Type = "focus", Target = new FlowTarget { Process = "notepad", Class = "Notepad" }, AtMs = 0 };
            session.Steps.Add(focus);
            session.Finish();
            FlowDocument document = FlowDocument.Load(path);
            Harness.Equal("显式添加的 focus 不被删除或重复", 1, document.Steps.Count);
            Harness.Sequence("显式 focus 的窗口契约不变",
                ["window", "focus", "--process", "notepad", "--class", "Notepad"],
                FlowCommandDisplay.Tokens(document.Steps[0]));
        });
    }

    private static HookEvent Event(HookKind kind, uint time, int key = 0, int x = 0, int y = 0, int data = 0) =>
        new(kind, key, x, y, data, time, IntPtr.Zero);

    private static void WithSession(Action<Recorder.Session, string> test)
    {
        string run = Path.Combine(Path.GetTempPath(), "KeyMouse.Tests", "record-focus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        try
        {
            string path = Path.Combine(run, "flow.json");
            var session = new Recorder.Session(path, 300, 8, false, 0);
            test(session, path);
        }
        finally { Directory.Delete(run, recursive: true); }
    }
}
