using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// The desktop-free half of recording: a flow step compiles into exactly the command line a human
/// would type, the loader rejects documents it cannot honour, and the thinning rule keeps the
/// trajectory readable. The hooks and the actual replay are covered by smoke.ps1.
/// </summary>
internal static class FlowTests
{
    public static void Run()
    {
        Harness.Group("flow: steps compile to command lines");

        var clientClick = new FlowStep
        {
            Type = "click",
            Button = "left",
            At = new FlowPoint { Space = "client", X = 10, Y = 20 },
            Target = new FlowTarget { Process = "notepad", Class = "Notepad" },
        };
        Harness.Sequence("a client click becomes -wx/-wy plus the selector",
            ["mouse", "click", "left", "-wx", "10", "-wy", "20", "--process", "notepad", "--class", "Notepad"],
            FlowDocument.ToArguments(clientClick));

        var screenClick = new FlowStep
        {
            Type = "click",
            Button = "right",
            At = new FlowPoint { Space = "screen", X = 100, Y = 200 },
        };
        Harness.Sequence("a screen click stays absolute (and needs no selector)",
            ["mouse", "click", "right", "-x", "100", "-y", "200"],
            FlowDocument.ToArguments(screenClick));

        var drag = new FlowStep
        {
            Type = "drag",
            Button = "left",
            From = new FlowPoint { Space = "client", X = 5, Y = 6 },
            To = new FlowPoint { Space = "client", X = 50, Y = 60 },
            DurationMs = 400,
            Target = new FlowTarget { Process = "notepad" },
        };
        Harness.Sequence("a drag carries both ends and its duration",
            ["mouse", "drag", "-wx", "5", "-wy", "6", "--wx2", "50", "--wy2", "60", "--duration", "400", "--process", "notepad"],
            FlowDocument.ToArguments(drag));

        var move = new FlowStep
        {
            Type = "move",
            At = new FlowPoint { Space = "client", X = 7, Y = 8 },
            Target = new FlowTarget { Process = "notepad" },
        };
        Harness.Sequence("a client move uses -wx/-wy",
            ["mouse", "move", "-wx", "7", "-wy", "8", "--process", "notepad"],
            FlowDocument.ToArguments(move));

        var screenMove = new FlowStep { Type = "move", At = new FlowPoint { Space = "screen", X = 7, Y = 8 } };
        Harness.Sequence("a screen move is positional (that is the mouse move syntax)",
            ["mouse", "move", "7", "8"],
            FlowDocument.ToArguments(screenMove));

        var wheel = new FlowStep
        {
            Type = "wheel",
            Delta = -240,
            At = new FlowPoint { Space = "client", X = 1, Y = 2 },
            Target = new FlowTarget { Process = "notepad" },
        };
        Harness.Sequence("a wheel step keeps the accumulated delta",
            ["mouse", "wheel", "-240", "-wx", "1", "-wy", "2", "--process", "notepad"],
            FlowDocument.ToArguments(wheel));

        var typing = new FlowStep
        {
            Type = "type",
            Text = "hello 世界",
            IntervalMs = 25,
            Target = new FlowTarget { Process = "notepad" },
        };
        Harness.Sequence("typing keeps the pace the human used",
            ["key", "type", "hello 世界", "--interval", "25", "--process", "notepad"],
            FlowDocument.ToArguments(typing));

        var combo = new FlowStep { Type = "key", Combo = "ctrl+s", Target = new FlowTarget { Process = "notepad" } };
        Harness.Sequence("a combination stays a combination",
            ["key", "combo", "ctrl+s", "--process", "notepad"],
            FlowDocument.ToArguments(combo));

        var single = new FlowStep { Type = "key", Text = "enter", Target = new FlowTarget { Process = "notepad" } };
        Harness.Sequence("a single key is a press",
            ["key", "press", "enter", "--process", "notepad"],
            FlowDocument.ToArguments(single));

        var focus = new FlowStep { Type = "focus", Target = new FlowTarget { Process = "notepad", Class = "Notepad" } };
        Harness.Sequence("focus compiles to the window command",
            ["window", "focus", "--process", "notepad", "--class", "Notepad"],
            FlowDocument.ToArguments(focus));

        Harness.Throws<CommandFailure>("client coordinates without a target are refused",
            () => FlowDocument.ToArguments(new FlowStep
            {
                Type = "move",
                At = new FlowPoint { Space = "client", X = 1, Y = 1 },
            }));
        Harness.Throws<CommandFailure>("wait steps are the runner's business, not the compiler's",
            () => FlowDocument.ToArguments(new FlowStep { Type = "sleep", Ms = 100 }));

        Harness.Group("flow: an empty when is the absence of a precondition");

        string emptyWhen = Path.Combine(Path.GetTempPath(), $"keymouse-empty-when-{Environment.ProcessId}.json");
        try
        {
            // 这就是在编辑器里点一下 when 的其它输入框之后文件的样子：前提的对象在，文字是空的。
            // 它不该让文件作废——"留空 = 总是执行"本来就是那一栏自己的说明。
            File.WriteAllText(emptyWhen, """
                {"format":"keymouse-flow","version":1,"steps":[
                  {"type":"sleep","ms":1,"when":{"region":{"space":"client","x":0,"y":0,"width":10,"height":10},"else":"fail"}}]}
                """);
            FlowDocument relaxed = FlowDocument.Load(emptyWhen);
            Harness.Equal("...still loads", 1, relaxed.Steps.Count);
            Harness.Check("...with the half-filled precondition dropped", relaxed.Steps[0].When is null);
            Harness.Equal("...so the step always runs", "sleep", relaxed.Steps[0].Type);
        }
        finally
        {
            if (File.Exists(emptyWhen)) File.Delete(emptyWhen);
        }
        Harness.Group("flow: the contract is the single source of truth");

        foreach (string type in FlowDocument.KnownTypes)
        {
            Harness.Check($"'{type}' has a contract entry", FlowSchema.TryGetStep(type, out _), type);
        }
        Harness.Check("...and the contract does not invent types",
            FlowSchema.Steps.Keys.All(FlowDocument.KnownTypes.Contains), string.Join(", ", FlowSchema.Steps.Keys));

        // 契约里出现的每一种字段，前端都必须有办法渲染；否则就是"格式里有、界面上没有"。
        string[] renderable = ["const", "text", "multiline", "int", "bool", "enum", "point", "region", "target", "when", "steps", "vars", "stringList"];
        foreach ((string type, FlowStepContract contract) in FlowSchema.Steps)
        {
            Harness.Check($"'{type}' declares at least one field", contract.Own.Count > 0, type);
            foreach (FlowField field in contract.Own)
            {
                Harness.Check($"'{type}.{field.Name}' has a label", field.LabelZh.Length > 0, field.Name);
                Harness.Check($"'{type}.{field.Name}' uses a kind the editor renders",
                    renderable.Contains(field.Kind), field.Kind);
            }
        }

        Harness.Check("click-text needs target, region and text",
            Required("click-text", "target") && Required("click-text", "region") && Required("click-text", "text"));
        Harness.Check("...while its tuning knobs are optional",
            !Required("click-text", "match") && !Required("click-text", "button") && !Required("click-text", "timeoutMs"));
        Harness.Check("read-text needs into", Required("read-text", "into"));
        Harness.Check("drag needs both ends and a window",
            Required("drag", "from") && Required("drag", "to") && Required("drag", "target"));
        Harness.Check("a key is either a combo or a single key", FlowSchema.Steps["key"].OneOf.Count == 2);
        Harness.Check("a repeat needs a count and a body",
            Required("repeat", "times") && Required("repeat", "steps"));
        Harness.Check("a call needs a flow", Required("call", "flow"));
        Harness.Check("...and exports are optional", !Required("call", "export"));
        Harness.Check("the document itself needs format, version and steps",
            FlowSchema.DocumentFields.Count(f => f.Required) == 3);
        Harness.Check("...and everything a step may carry beyond its own fields is optional",
            FlowSchema.CommonFields.All(f => !f.Required));
        Harness.Check("nested shapes are declared where the format nests",
            FlowSchema.ShapeFields("target").Count > 0 && FlowSchema.ShapeFields("region").Count == 5 &&
            FlowSchema.ShapeFields("point").Count == 3 && FlowSchema.ShapeFields("when").Count > 0);

        // 契约说必须的东西，加载器就得拒绝：`drag` 少了端点以前是跑到编译时才炸的。
        string dragPath = Path.Combine(Path.GetTempPath(), $"keymouse-drag-required-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(dragPath,
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"drag","target":{"process":"p"}}]}""");
            Harness.Throws<CommandFailure>("a drag without from/to is refused when the file loads",
                () => FlowDocument.Load(dragPath));
        }
        finally
        {
            if (File.Exists(dragPath)) File.Delete(dragPath);
        }

        static bool Required(string type, string field) =>
            FlowSchema.Steps[type].Own.Any(f => f.Name == field && f.Required);
        Harness.Group("flow: restoring a minimized window is opt-in");

        string restorePath = Path.Combine(Path.GetTempPath(), $"keymouse-restore-test-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(restorePath,
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"focus","target":{"process":"notepad"}}]}""");
            Harness.Check("a flow does not restore windows unless it says so",
                !FlowDocument.Load(restorePath).AllowRestore);

            File.WriteAllText(restorePath,
                """{"format":"keymouse-flow","version":1,"allowRestore":true,"steps":[{"type":"focus","target":{"process":"notepad"}}]}""");
            Harness.Check("...and does when it carries the flag", FlowDocument.Load(restorePath).AllowRestore);
        }
        finally
        {
            if (File.Exists(restorePath)) File.Delete(restorePath);
        }

        Harness.Group("flow: loading and the trajectory rule");

        string path = Path.Combine(Path.GetTempPath(), $"keymouse-flow-test-{Environment.ProcessId}.json");
        try
        {
            var document = new FlowDocument
            {
                RecordedAt = "2026-10-03T00:00:00+08:00",
                Screen = new FlowScreen { Width = 2560, Height = 1440 },
                Steps =
                [
                    new FlowStep { Type = "sleep", Ms = 250 },
                    new FlowStep { Type = "type", Text = "hi", Target = new FlowTarget { Process = "notepad" } },
                ],
            };
            document.Save(path);
            var loaded = FlowDocument.Load(path);
            Harness.Equal("a saved flow loads back", 2, loaded.Steps.Count);
            Harness.Equal("...with its step types intact", "sleep", loaded.Steps[0].Type);
            Harness.Equal("...and its recorded pace", 250, loaded.Steps[0].Ms);

            File.WriteAllText(path, """{"format":"keymouse-flow","version":1,"steps":[{"type":"teleport"}]}""");
            Harness.Throws<CommandFailure>("an unknown step type is rejected on load",
                () => FlowDocument.Load(path));

            File.WriteAllText(path, """{"format":"something-else","version":1,"steps":[]}""");
            Harness.Throws<CommandFailure>("a foreign format is rejected",
                () => FlowDocument.Load(path));

            File.WriteAllText(path, """{"format":"keymouse-flow","version":2,"steps":[]}""");
            Harness.Throws<CommandFailure>("a newer version is rejected instead of half-honoured",
                () => FlowDocument.Load(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        var points = new List<System.Drawing.Point>
        {
            new(0, 0), new(3, 3), new(5, 5), new(100, 100), new(101, 101),
        };
        Harness.Sequence("thinning keeps the first point and every point past the threshold",
            ["0", "3"],
            Recorder.ThinTrajectoryIndices(points, 8).Select(i => i.ToString()));
        Harness.Check("a gesture inside the threshold is a click",
            !Recorder.IsDrag(new System.Drawing.Point(0, 0), new System.Drawing.Point(3, 3), 8));
        Harness.Check("a gesture past the threshold is a drag",
            Recorder.IsDrag(new System.Drawing.Point(0, 0), new System.Drawing.Point(20, 20), 8));

        Harness.Group("flow: the wait-text predicate");
        Harness.Check("whitespace is ignored: the engine splits CJK into one word per character",
            TextPredicate.Matches("你 好 ， 世 界", "你好，世界", TextPredicate.Contains, 0, out _));
        Harness.Check("contains finds a UI string inside a longer read",
            TextPredicate.Matches("是否保存更改？[是] [否] [取消]", "取消", TextPredicate.Contains, 0, out _));
        Harness.Check("...and refuses one that is not there",
            !TextPredicate.Matches("保存 取消 确定", "另存为", TextPredicate.Contains, 0, out _));
        Harness.Check("exact demands every character",
            TextPredicate.Matches("用户名或密码不正确", "用户名或密码不正确", TextPredicate.Exact, 0, out _));
        Harness.Check("...including the ones it got wrong",
            !TextPredicate.Matches("用户名或密码不正确", "用户名或密码错误", TextPredicate.Exact, 0, out _));
        Harness.Check("fuzzy spends the caller's budget: one wrong character fits in one",
            TextPredicate.Matches("保存近钮", "保存按钮", TextPredicate.Fuzzy, 1, out _));
        Harness.Check("...and does not fit in zero",
            !TextPredicate.Matches("保存近钮", "保存按钮", TextPredicate.Fuzzy, 0, out _));
        Harness.Check("two edits need a budget of two",
            TextPredicate.Matches("保存控钮", "保存按钮", TextPredicate.Fuzzy, 2, out _));
        Harness.Check("an unknown mode is a failure with an explanation, not a silent no",
            !TextPredicate.Matches("a", "a", "roughly", 1, out string unknownMode) &&
            unknownMode.Contains("未知的匹配方式"));
        Harness.Equal("edit distance counts characters", 3, TextPredicate.Distance("", "abc"));
        Harness.Equal("...and is zero for an exact match", 0, TextPredicate.Distance("按钮", "按钮"));
        Harness.Equal("squashing drops every kind of whitespace", "abc",
            TextPredicate.Squash("a b\tc\n d".Replace("d", "")));
        Harness.Equal("...and works past the stack buffer (300 characters)", 300,
            TextPredicate.Squash(string.Join(' ', new string('x', 300).ToCharArray())).Length);
        Harness.Check("every mode the loader accepts is one the predicate knows",
            TextPredicate.Modes.All(TextPredicate.IsKnownMode) && !TextPredicate.IsKnownMode("regex"));

        Harness.Group("flow: wait-text is validated when the file loads");
        var waitPath = Path.Combine(Path.GetTempPath(), $"keymouse-wait-test-{Environment.ProcessId}.json");
        try
        {
            void Write(string steps) =>
                File.WriteAllText(waitPath, $$"""{"format":"keymouse-flow","version":1,"steps":[{{steps}}]}""");

            Write("""{"type":"wait-text","text":"保存成功","target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("a wait-text without a region is refused",
                () => FlowDocument.Load(waitPath));

            Write("""{"type":"wait-text","region":{"space":"client","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("a wait-text without text is refused",
                () => FlowDocument.Load(waitPath));

            Write("""{"type":"wait-text","text":"保存成功","match":"roughly","region":{"space":"client","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("an unknown match mode is refused before anything runs",
                () => FlowDocument.Load(waitPath));

            Write("""{"type":"wait-text","text":"保存成功","region":{"space":"screen","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("a screen-space region is refused: it goes stale with the window",
                () => FlowDocument.Load(waitPath));

            Write("""{"type":"wait-text","text":"保存成功","match":"fuzzy","maxErrors":1,"timeoutMs":3000,"confirm":2,"region":{"space":"client","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"}}""");
            var valid = FlowDocument.Load(waitPath);
            Harness.Equal("a complete condition loads", "wait-text", valid.Steps[0].Type);
            Harness.Equal("...with its budget", 1, valid.Steps[0].MaxErrors);
            Harness.Equal("...and its rectangle", 200, valid.Steps[0].Region!.Width);
        }
        finally
        {
            if (File.Exists(waitPath)) File.Delete(waitPath);
        }

        Harness.Group("flow: click-text and the when precondition");
        var actPath = Path.Combine(Path.GetTempPath(), $"keymouse-act-test-{Environment.ProcessId}.json");
        try
        {
            void WriteAct(string step) =>
                File.WriteAllText(actPath, $$"""{"format":"keymouse-flow","version":1,"steps":[{{step}}]}""");

            WriteAct("""{"type":"click-text","text":"保存","target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("a click-text without a region is refused",
                () => FlowDocument.Load(actPath));

            WriteAct("""{"type":"click-text","text":"保存","at":{"space":"client","x":0,"y":0},"target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("...and a click-text without text is refused too",
                () => FlowDocument.Load(actPath));

            WriteAct("""{"type":"click-text","text":"保存","match":"vibes","region":{"space":"client","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"}}""");
            Harness.Throws<CommandFailure>("an unknown match mode is refused on load",
                () => FlowDocument.Load(actPath));

            WriteAct("""{"type":"click","at":{"space":"client","x":10,"y":10},"target":{"process":"notepad"},"when":{"text":"保存成功"}}""");
            Harness.Throws<CommandFailure>("a when without a region is refused",
                () => FlowDocument.Load(actPath));

            WriteAct("""{"type":"click","at":{"space":"client","x":10,"y":10},"target":{"process":"notepad"},"when":{"text":"保存成功","region":{"space":"client","x":0,"y":0,"width":200,"height":24},"else":"explode"}}""");
            Harness.Throws<CommandFailure>("an unknown else is refused (skip | fail only)",
                () => FlowDocument.Load(actPath));

            WriteAct("""{"type":"click-text","button":"right","text":"保存","match":"fuzzy","maxErrors":1,"timeoutMs":3000,"region":{"space":"client","x":0,"y":0,"width":200,"height":24},"target":{"process":"notepad"},"when":{"text":"就绪","region":{"space":"client","x":0,"y":0,"width":200,"height":24},"else":"skip"}}""");
            var acted = FlowDocument.Load(actPath);
            Harness.Equal("a click-text with a precondition loads", "click-text", acted.Steps[0].Type);
            Harness.Equal("...keeping its button", "right", acted.Steps[0].Button);
            Harness.Equal("...and its precondition's else", "skip", acted.Steps[0].When!.Else);
        }
        finally
        {
            if (File.Exists(actPath)) File.Delete(actPath);
        }
        Harness.Group("flow: keys and retryable codes");
        Harness.Equal("virtual keys map back to names for recording", "enter", KeyMap.NameOf(0x0D));
        Harness.Equal("...and escape is 'esc'", "esc", KeyMap.NameOf(0x1B));
        Harness.Equal("...unknown keys have no name", null, KeyMap.NameOf(0xE0));
        Harness.Check("'could not read' (6) is retryable, as the docs promise",
            ScriptRunner.IsRetryable(6));
        Harness.Check("a runtime failure (1) is never retried",
            !ScriptRunner.IsRetryable(1));
    }
}
