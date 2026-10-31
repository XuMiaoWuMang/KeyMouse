using System.Diagnostics;
using System.Globalization;

namespace KeyMouse.Flow;
/// <summary>
/// Executes a JSON flow (`run flow.json`). It is deliberately thin: every step is compiled into the
/// same command line a human would type, and dispatched through the same entry point, so selectors,
/// the eligibility gate, focus verification, `--dry-run` and the exit codes mean exactly the same
/// thing here as everywhere else. What the runner adds is sequencing, the two waiting steps that
/// have no command-line equivalent, and the report.
/// </summary>
internal static class FlowRunner
{
    private static bool _running;

    /// <summary>
    /// True when `run` was handed a JSON flow. Detection is content-based (the first non-space
    /// character), and only the documented form `run &lt;flow.json&gt; [options]` is considered, so
    /// an option value can never be mistaken for the path. stdin stays the text syntax on purpose:
    /// a stream cannot be peeked twice.
    /// </summary>
    internal static bool LooksLikeJson(string[] args)
    {
        if (args.Length == 0) return false;
        string path = args[0];
        if (path.StartsWith('-') || path == "-") return false;

        try
        {
            if (!File.Exists(path)) return false;
            using var reader = new StreamReader(path);
            int read;
            while ((read = reader.Read()) >= 0)
            {
                char c = (char)read;
                if (char.IsWhiteSpace(c)) continue;
                return c == '{';
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        return false;
    }

    internal static int Run(string[] args, Commands.GlobalOptions global, Func<string[], int> execute)
    {
        if (_running) return Commands.Fail(2, "run：流程里不能再跑流程（拒绝嵌套）");
        if (global.HasSelector || global.Wx.HasValue || global.Wy.HasValue || global.Pick is not null)
            return Commands.Fail(2, "run：窗口选择器要写在步骤里，不能挂在 run 自己身上");

        var options = new ScriptOptions();
        if (ScriptRunner.ParseOptions(args, options) is { } optionError) return Commands.Fail(2, optionError);
        if (options.Path is null)
            return Commands.Fail(2, "用法：KeyMouse run <流程.json> [--delay 毫秒] [--keep-going] [--dry-run] [--retry 次数] [--report 文件.json]");

        FlowDocument document;
        try
        {
            // --set wins over the document variables, so one file can be reused with other data.
            document = FlowDocument.Load(options.Path, options.Variables);
            // The document's own intent joins the command line's: either may allow a restore.
            if (document.AllowRestore) options.AllowRestore = true;
        }
        catch (CommandFailure ex)
        {
            return Commands.Fail(ex.Code, ex.Message);
        }

        // Loops are flattened before anything runs: a `repeat`/`foreach` becomes the steps it would
        // run, with the loop variables already substituted. One execution loop keeps one place where
        // pause, cancel and step reporting live - no second interpreter for groups.
        FlowPlan plan = FlowPlan.Build(document, Path.GetFullPath(options.Path!));
        if (plan.StepCount == 0) return Commands.Fail(2, $"流程 '{options.Path}' 里没有任何步骤");

        string source = Path.GetFullPath(options.Path);
        var report = new ScriptReport
        {
            Script = source,
            StartedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            DryRun = options.DryRun,
            Total = plan.StepCount,
        };

        Console.WriteLine($"流程 {source}");
        Console.WriteLine($"  {plan.StepCount} 步" + (options.DryRun ? "（--dry-run：只做资格检查，不发送输入）" : ""));
        if (document.RecordedAt is not null) Console.WriteLine($"  录制于 {document.RecordedAt}");
        if (document.DurationMs is int recordedMs)
            Console.WriteLine($"  时长 {recordedMs / 1000.0:0.#} 秒（按录制时的时间线回放）");

        int exitCode = 0;
        _running = true;
        try
        {
            // Numbered by step, not by plan item: an export item is bookkeeping, and counting it would
            // report "23/21" in the log and in a UI's progress bar.
            int stepNo = 0;

            // 时间线回放：录制出来的每一步都带 atMs（从录制开始算的毫秒）。
            // 回放按这个时间线排程，快速挥动就是一阵密集事件，中途停顿就是真的停——
            // 以前只有"每个 move 固定睡 8ms"，所以速度均匀且偏慢（实测反馈）。
            var timeline = Stopwatch.StartNew();
            bool timelineDriven = plan.Items.Any(item => item.Step?.AtMs is not null);

            for (int i = 0; i < plan.Items.Count; i++)
            {
                // A step-less item is a `call`'s export: copy the names the subflow agreed to hand back
                // into the caller's frame, which is what makes a subflow reusable for real work.
                if (plan.Items[i].Step is null)
                {
                    FlowPlan.Item export = plan.Items[i];
                    foreach (string name in export.Export ?? [])
                    {
                        string? value = export.ExportFrom?.Lookup(name);
                        if (value is not null) export.Frame.Set(name, value);
                    }
                    continue;
                }
                // The Runner's seam: with no control installed (the CLI path) these are no-ops; with a
                // client attached they block while paused, throw when cancelled, and report the step
                // boundary so a UI can highlight the row that is executing right now.
                stepNo++;
                Execution.BetweenSteps(stepNo);

                // Resolved per step, not per run: a `read-text` can put something in scope that later
                // steps interpolate, which is what makes variables worth having.
                FlowPlan.Item item = plan.Items[i];
                FlowStep step = FlowDocument.Resolve(item.Step!, item.Frame);

                if (timelineDriven && !options.DryRun)
                {
                    // 等到时间线上的这一刻。等到就已经落后（例如上一步很慢）时不再补等，
                    // 直接往下走：宁可晚一点，也不要越拖越晚。
                    if (step.AtMs is int atMs) WaitForTimeline(timeline, atMs, stepNo);

                    // sleep 的等待已经由 atMs 表达，执行它会变成双倍等待。
                    if (step.Type == "sleep" && step.AtMs is not null) step = new FlowStep { Type = "sleep", Ms = 0 };
                }
                VariableFrame frame = item.Frame;
                var record = new CommandRecord { Index = stepNo, Line = stepNo, Origin = item.Origin };
                var clock = Stopwatch.StartNew();
                int attempt = 0;
                int code;
                string output;
                bool skipped = false;
                Execution.StepStarted(stepNo, step.Type);

                // A step may carry a precondition: run it only if that text is on screen in time.
                // Skipping sends nothing, and failing is exit 3 for the same reason - the caller can
                // retry without wondering whether half of something happened.
                if (step.When is { } condition)
                {
                    PollResult when = EvaluateWhen(step, condition, options.DryRun, options.AllowRestore, stepNo);
                    if (!when.Found)
                    {
                        if ((condition.Else ?? "skip") == "fail")
                        {
                            code = when.ExitCode;
                            output = $"前提不成立（else=fail）：{when.Detail}";
                            record.Attempts = 1;
                            record.ExitCode = code;
                            record.DurationMs = clock.ElapsedMilliseconds;
                            record.Output = output;
                            record.Command = Describe(step);
                            report.Commands.Add(record);
                            Execution.StepFinished(stepNo, code, clock.ElapsedMilliseconds);
                            report.Failed++;
                            exitCode = code;
                            report.ExitCode = code;
                            report.StoppedAtLine = stepNo;
                            break;
                        }

                        skipped = true;
                        code = 0;
                        output = $"前提不成立，已跳过：{when.Detail}";
                    }
                    else
                    {
                        skipped = false;
                        code = 0;
                        output = $"前提成立：{when.Detail}";
                    }
                }
                else
                {
                    code = 0;
                    output = "";
                }

                while (!skipped && true)
                {
                    attempt++;
                    (code, output) = RunStep(step, options, execute, stepNo, frame);
                    if (code == 0 || attempt > options.Retry || !ScriptRunner.IsRetryable(code)) break;
                    Console.WriteLine($"  [{stepNo,3}] 第 {attempt} 次重试（退出码 {code} 可重试，等 {options.RetryDelayMs} ms）");
                    Thread.Sleep(options.RetryDelayMs);
                }

                clock.Stop();
                Execution.StepFinished(stepNo, code, clock.ElapsedMilliseconds);
                record.Attempts = attempt;
                record.ExitCode = code;
                record.DurationMs = clock.ElapsedMilliseconds;
                record.Output = output;
                record.Command = Describe(step);
                record.Skipped = skipped;
                report.Commands.Add(record);

                string tag = skipped ? "skip" : code == 0 ? "ok" : $"exit {code}";
                Console.WriteLine($"  [{stepNo,3}/{plan.StepCount}] {tag,-8} {record.Command}" +
                                  (output.Length > 0 && (options.Echo || code != 0 || skipped) ? $"  <- {Shorten(output)}" : ""));

                if (code == 0)
                {
                    report.Succeeded++;
                }
                else
                {
                    report.Failed++;
                    if (!options.KeepGoing)
                    {
                        exitCode = code;
                        report.ExitCode = code;
                        report.StoppedAtLine = stepNo;
                        break;
                    }
                    exitCode = exitCode == 0 ? code : exitCode;
                }

                if (options.DelayMs > 0 && i < plan.Items.Count - 1) Thread.Sleep(options.DelayMs);
            }
        }
        finally
        {
            _running = false;
        }

        report.ExitCode = exitCode;
        Console.WriteLine();
        Console.WriteLine($"流程结束：{report.Succeeded} 步成功，{report.Failed} 步失败，退出码 {exitCode}");
        if (options.ReportPath is not null)
        {
            ScriptRunner.WriteReport(options.ReportPath, report);
            Console.WriteLine($"报告 {Path.GetFullPath(options.ReportPath)}");
        }
        return exitCode;
    }

    private static (int ExitCode, string Output) RunStep(FlowStep step, ScriptOptions options, Func<string[], int> execute, int stepIndex, VariableFrame frame)
    {
        switch (step.Type)
        {
            case "sleep":
                int ms = Math.Clamp(step.Ms ?? 0, 0, 3_600_000);
                if (!options.DryRun) Thread.Sleep(ms);
                return (0, $"等待 {ms} ms");

            // 长按是一步，不是三步：按下 → 按住 → 松开在同一步里做完。
            // 写成一步的好处是它可以被当一个操作来编辑（按键/落点/时长/窗口），
            // 而文件里也不会出现"按下"和"松开"被别的步骤隔开却仍算一次长按的情况。
            case "hold":
            case "mousehold":
            case "keyhold":
            {
                int holdMs = Math.Clamp(step.Ms ?? 0, 0, 3_600_000);
                string what = step.Button is { Length: > 0 } button
                    ? $"鼠标{button switch { "right" => "右键", "middle" => "中键", _ => "左键" }}"
                    : $"键盘 {step.Combo ?? step.Text ?? "?"}";
                if (options.DryRun) return (0, $"长按 {what} {holdMs} ms（试运行未真的按）");

                bool mouse = step.Type == "mousehold" || (step.Type == "hold" && step.Button is { Length: > 0 });
                if (mouse && step.At is { Space: FlowSpace.Screen }) NativeInput.MoveTo(step.At.X, step.At.Y);
                // 键盘长按不绕道 CLI 参数解析：直接按下/松开，少一层出错的机会。
                (ushort Vk, bool Ext) key = default;
                if (mouse) NativeInput.ButtonDown(step.Button!);
                else
                {
                    key = KeyMap.Resolve(step.Combo ?? step.Text ?? "");
                    NativeInput.Key(key.Vk, key.Ext, false);

                    // 真人的长按是**系统连发**：按住期间不断产生重复 keydown。
                    // 只做"按下→干等→松开"时，输入法/文本框只收到一个键（实测：候选框里只有一个 a），
                    // 因为重复是键盘驱动产生的，注入一次按下不会自动重复。
                    // 这里照着 Windows 的节奏补出来：首延迟约 500 ms，之后每 33 ms 重复一次。
                    int repeatStart = Math.Min(500, holdMs);
                    Thread.Sleep(repeatStart);
                    for (int elapsed = repeatStart; elapsed < holdMs; elapsed += 33)
                    {
                        NativeInput.Key(key.Vk, key.Ext, false);
                        Thread.Sleep(33);
                    }
                    NativeInput.Key(key.Vk, key.Ext, true);
                    return (0, $"长按 {what} {holdMs} ms（含连发）");
                }

                Thread.Sleep(holdMs);

                if (mouse) NativeInput.ButtonUp(step.Button!);
                else NativeInput.Key(key.Vk, key.Ext, true);

                return (0, $"长按 {what} {holdMs} ms");
            }

            case "wait-window":
                return WaitWindow(step, options);

            case "wait-text":
                return WaitText(step, options, stepIndex);

            case "click-text":
                return ClickText(step, options, execute, stepIndex);

            case "read-text":
                return ReadText(step, options, stepIndex, frame);

            default:
                string[] argv = FlowDocument.ToArguments(step);
                // The document's consent has to travel with the compiled command line too: the step is
                // dispatched through the same parser as a hand-typed command, and that parser keeps its own
                // idea of whether restoring a minimized window is allowed. Measured: without this, a flow
                // with "allowRestore": true still exited 4.
                if (options.AllowRestore) argv = [.. argv, "--allow-restore"];
                if (options.DryRun) argv = [.. argv, "--dry-run"];
                var tokens = new List<string>(argv);
                return ScriptRunner.RunOnce(execute, tokens);
        }
    }

    /// <summary>
    /// Reads a region once and stores what was read in a variable, so a flow can carry data from one
    /// step to the next ("read the total, then type it somewhere else"). With `text` given it stores
    /// the matched piece instead of the whole line - the same locator `--find` uses.
    /// </summary>
    private static (int ExitCode, string Output) ReadText(
        FlowStep step, ScriptOptions options, int stepIndex, VariableFrame frame)
    {
        WindowSelector selector = SelectorOf(step.Target);
        if (!HasSelector(selector))
            return (2, "read-text 需要 target（至少给 process 或 class，否则不知道该读哪个窗口）");

        if (options.DryRun)
        {
            var dry = WindowResolver.Resolve(selector, options.AllowRestore);
            if (dry.Usable.Count == 0) return (4, $"（dry-run）目标窗口现在不可用：{selector.Describe()}");
            try
            {
                Probe.ValidateRegion(dry.Usable[0], windowSpace: false, step.Region!.Rect());
            }
            catch (ArgumentException ex)
            {
                return (2, ex.Message);
            }
            return (0, $"（dry-run）会把 {step.Into} 设成读到的内容（区域已校验）");
        }

        var resolution = WindowResolver.Resolve(selector, options.AllowRestore);
        if (resolution.Usable.Count == 0) return (4, $"目标窗口现在不可用：{selector.Describe()}");

        try
        {
            var (text, confidence, lines) = Probe.ReadOnce(resolution.Usable[0], windowSpace: false, step.Region!.Rect());
            string value = text;
            if (!string.IsNullOrEmpty(step.Text))
            {
                TextMatch? match = TextLocator.Find(lines, step.Text!, step.Match ?? TextPredicate.Contains,
                    Math.Clamp(step.MaxErrors ?? 1, 0, 20));
                if (match is null)
                    return (3, $"没找到「{step.Text}」（读到「{text}」）——{step.Into} 没被改动");
                value = match.Matched;
            }

            frame.Set(step.Into!, value);
            return (0, $"{step.Into} = 「{value}」" + (confidence is double c ? $"（置信度 {c:0.0}）" : ""));
        }
        catch (CommandFailure ex)
        {
            return (ex.Code, ex.Message);
        }
    }
    /// <summary>Waits until a usable window matches the step's selector, then reports how long it took.</summary>
    private static (int ExitCode, string Output) WaitWindow(FlowStep step, ScriptOptions options)
    {
        int timeoutMs = Math.Clamp(step.TimeoutMs ?? 5000, 0, 600_000);
        int intervalMs = Math.Clamp(step.IntervalMs ?? 200, 20, 5000);
        string[] selector = step.Target?.Selector() ?? [];
        if (selector.Length == 0) return (2, "wait-window 需要 target（窗口选择器）");
        if (options.DryRun) return (0, $"（dry-run）检查选择器即可：{string.Join(' ', selector)}");

        var windowSelector = new WindowSelector
        {
            ProcessName = step.Target?.Process,
            ClassName = step.Target?.Class,
        };
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var resolution = WindowResolver.Resolve(windowSelector, options.AllowRestore);
            if (resolution.Usable.Count > 0)
            {
                return (0, $"等到「{resolution.Usable[0].Title}」（{clock.ElapsedMilliseconds} ms）");
            }
            if (clock.ElapsedMilliseconds >= timeoutMs)
            {
                return (3, $"等了 {timeoutMs} ms 仍没有可用窗口匹配 {string.Join(' ', selector)}");
            }
            Thread.Sleep(intervalMs);
        }
    }

    /// <summary>
    /// Polls a region until it reads as the expected text, and reports where the text is: one OCR read
    /// per poll, and a match has to hold for `confirm` consecutive polls **with the same text** before
    /// it counts. One poll would be a coin flip on a frame caught mid-repaint (measured: a caret or a
    /// repaint makes a single read disagree with the next), so the confirmation is what makes the
    /// predicate trustworthy rather than fast. Every poll is a real engine call (~0.2-0.6 s at this
    /// region size), which is why the default interval is short and the timeout is the caller's
    /// statement of patience.
    ///
    /// `wait-text`, `click-text` and `when` all run through this one loop, so "did I see it" cannot
    /// mean three slightly different things.
    /// </summary>
    private static PollResult PollForText(
        WindowSelector selector, FlowRegion region, string expected, string mode, int maxErrors,
        int timeoutMs, int intervalMs, int confirm, bool dryRun, bool allowRestore, int stepIndex)
    {
        // 区域可以落在客户区，也可以落在整个窗口（标题栏那类）。引擎两种都支持。
        bool windowSpace = region.Space == FlowSpace.Window;

        if (dryRun)
        {
            // No engine call in a dry run: resolve the window and check the rectangle, which is what a
            // dry run can honestly verify about a condition.
            var resolution = WindowResolver.Resolve(selector, allowRestore);
            if (resolution.Usable.Count == 0)
                return new PollResult(false, null, 4, $"（dry-run）目标窗口现在不可用：{selector.Describe()}", "");
            try
            {
                Probe.ValidateRegion(resolution.Usable[0], windowSpace, region.Rect());
            }
            catch (ArgumentException ex)
            {
                return new PollResult(false, null, 2, ex.Message, "");
            }
            return new PollResult(true, null, 0, $"（dry-run）会等「{expected}」最多 {timeoutMs} ms（匹配方式 {mode}）", "");
        }

        var clock = Stopwatch.StartNew();
        int consecutive = 0;
        string lastText = "";
        string lastDetail = "还没有读到东西";
        string confirmedText = "";

        while (true)
        {
            // A long wait has to be cancellable (and pausable) like any other step.
            Execution.BetweenSteps(stepIndex);

            var resolution = WindowResolver.Resolve(selector, allowRestore);
            if (resolution.Usable.Count == 0)
            {
                consecutive = 0;
                lastDetail = $"目标窗口不可用（{selector.Describe()}）";
            }
            else
            {
                try
                {
                    var (text, _, lines) = Probe.ReadOnce(resolution.Usable[0], windowSpace, region.Rect());
                    lastText = text;
                    TextMatch? match = TextLocator.Find(lines, expected, mode, maxErrors);
                    if (match is not null)
                    {
                        if (text == confirmedText)
                        {
                            consecutive++;
                        }
                        else
                        {
                            confirmedText = text;
                            consecutive = 1;
                        }
                        lastDetail = $"第 {consecutive}/{confirm} 次确认「{text}」";
                        if (consecutive >= confirm)
                        {
                            return new PollResult(true, match, 0,
                                $"等到「{match.Matched}」（{clock.ElapsedMilliseconds} ms，" +
                                $"连续 {consecutive} 次读到同一段文字）", lastText);
                        }
                    }
                    else
                    {
                        consecutive = 0;
                        confirmedText = "";
                        lastDetail = TextPredicate.Matches(text, expected, mode, maxErrors, out string why)
                            ? why
                            : $"读到「{text}」，没找到「{expected}」";
                    }
                }
                catch (CommandFailure ex)
                {
                    // A capture that fails (window vanished, region clipped) is a reason to keep
                    // polling until the timeout: that is exactly what a wait is for.
                    consecutive = 0;
                    lastDetail = ex.Message;
                }
            }

            if (clock.ElapsedMilliseconds >= timeoutMs)
            {
                return new PollResult(false, null, 3,
                    $"等了 {timeoutMs} ms 没等到「{expected}」：{lastDetail}" +
                    (lastText.Length > 0 ? $"（最后读到「{lastText}」）" : ""), lastText);
            }
            if (intervalMs > 0) Thread.Sleep(intervalMs);
        }
    }

    internal sealed record PollResult(bool Found, TextMatch? Match, int ExitCode, string Detail, string LastText);

    /// <summary>把流程里的目标变成窗口选择器。全项目只此一份（截图证据也用它）。</summary>
    internal static WindowSelector SelectorOf(FlowTarget? target) => new()
    {
        ProcessName = string.IsNullOrWhiteSpace(target?.Process) ? null : target!.Process,
        ClassName = string.IsNullOrWhiteSpace(target?.Class) ? null : target!.Class,
    };

    private static bool HasSelector(WindowSelector selector) =>
        selector.ProcessName is not null || selector.ClassName is not null;

    private static PollResult PollStep(FlowStep step, bool dryRun, bool allowRestore, int stepIndex) => PollForText(
        SelectorOf(step.Target), step.Region!, step.Text ?? "",
        step.Match ?? TextPredicate.Contains, Math.Clamp(step.MaxErrors ?? 1, 0, 20),
        Math.Clamp(step.TimeoutMs ?? 5000, 0, 600_000), Math.Clamp(step.IntervalMs ?? 250, 0, 5000),
        Math.Clamp(step.Confirm ?? 2, 1, 10), dryRun, allowRestore, stepIndex);

    /// <summary>
    /// 等到流程时间线上的某一刻（毫秒，从本次回放开始算）。分片睡眠，
    /// 这样暂停/停止能在 10ms 内生效——长停顿也是可打断的。
    /// </summary>
    private static void WaitForTimeline(Stopwatch clock, int atMs, int stepIndex)
    {
        while (true)
        {
            long remaining = atMs - clock.ElapsedMilliseconds;
            if (remaining <= 0) return;
            Thread.Sleep((int)Math.Min(remaining, 10));
            Execution.BetweenSteps(stepIndex);
        }
    }

    private static (int ExitCode, string Output) WaitText(FlowStep step, ScriptOptions options, int stepIndex)
    {
        WindowSelector selector = SelectorOf(step.Target);
        if (!HasSelector(selector))
            return (2, "wait-text 需要 target（至少给 process 或 class，否则不知道该读哪个窗口）");

        PollResult poll = PollStep(step, options.DryRun, options.AllowRestore, stepIndex);
        return poll.ExitCode == 0 ? (0, poll.Detail) : (poll.ExitCode, poll.Detail);
    }

    /// <summary>
    /// The step that acts on what it sees: find the text, then click the middle of the box it was
    /// found in. The point is computed from the engine's own boxes (client coordinates, because the
    /// region is client-relative), so "read the screen" and "click the thing" cannot disagree about
    /// where it is.
    /// </summary>
    private static (int ExitCode, string Output) ClickText(FlowStep step, ScriptOptions options, Func<string[], int> execute, int stepIndex)
    {
        WindowSelector selector = SelectorOf(step.Target);
        if (!HasSelector(selector))
            return (2, "click-text 需要 target（至少给 process 或 class，否则不知道该读哪个窗口）");

        PollResult poll = PollStep(step, options.DryRun, options.AllowRestore, stepIndex);
        if (!poll.Found) return (poll.ExitCode, poll.Detail);
        if (poll.Match is null)
        {
            // dry run: the rectangle was validated, nothing was read and nothing is clicked.
            return (0, poll.Detail);
        }

        FlowRegion region = step.Region!;
        int x = region.X + poll.Match.Rect[0] + poll.Match.Rect[2] / 2;
        int y = region.Y + poll.Match.Rect[1] + poll.Match.Rect[3] / 2;

        // 客户区坐标可以直接交给 -wx/-wy；窗口坐标要先加上窗口原点变成屏幕坐标再点。
        // （两种都按同一个窗口选择器换算，所以"读到哪儿"与"点到哪儿"不会分叉。）
        bool absolute = region.Space == FlowSpace.Window;
        if (absolute)
        {
            var target = WindowResolver.Resolve(selector, options.AllowRestore);
            if (target.Usable.Count == 0)
                return (4, $"要点的窗口现在不可用：{selector.Describe()}");
            if (!NativeWindow.GetWindowRect(target.Usable[0].Handle, out NativeWindow.RECT rect))
                return (4, "窗口矩形读不出来——没有发出任何点击");
            x += rect.Left;
            y += rect.Top;
        }

        var arguments = new List<string> { "mouse", "click", step.Button ?? "left" };
        if (absolute) arguments.AddRange(["-x", x.ToString(), "-y", y.ToString()]);
        else arguments.AddRange(["-wx", x.ToString(), "-wy", y.ToString()]);
        if (!string.IsNullOrWhiteSpace(step.Target?.Process)) arguments.AddRange(["--process", step.Target!.Process]);
        if (!string.IsNullOrWhiteSpace(step.Target?.Class)) arguments.AddRange(["--class", step.Target!.Class]);

        var (exitCode, output) = ScriptRunner.RunOnce(execute, arguments);
        return (exitCode, $"「{poll.Match.Matched}」在{(absolute ? "屏幕" : "客户区")} {x},{y}（框 {poll.Match.Rect[0]},{poll.Match.Rect[1]} " +
                          $"{poll.Match.Rect[2]}x{poll.Match.Rect[3]}，置信度 {poll.Match.Confidence:0.0}）→ " +
                          Shorten(output));
    }

    /// <summary>
    /// Evaluates a step's precondition. Made a separate call so `when` reads exactly like the other
    /// text conditions: it either holds (run the step) or it does not (skip it, or fail with exit 3 -
    /// which still means "nothing was sent").
    /// </summary>
    private static PollResult EvaluateWhen(FlowStep step, FlowCondition condition, bool dryRun, bool allowRestore, int stepIndex) =>
        PollForText(
            SelectorOf(condition.Target ?? step.Target), condition.Region!, condition.Text ?? "",
            condition.Match ?? TextPredicate.Contains, Math.Clamp(condition.MaxErrors ?? 1, 0, 20),
            Math.Clamp(condition.TimeoutMs ?? 2000, 0, 600_000), Math.Clamp(condition.IntervalMs ?? 250, 0, 5000),
            Math.Clamp(condition.Confirm ?? 2, 1, 10), dryRun, allowRestore, stepIndex);

    private static string Describe(FlowStep step) => step.Type switch
    {
        "sleep" => $"等 {step.Ms ?? 0} ms",
        "wait-window" => $"等窗口 {step.Target?.Process ?? step.Target?.Class ?? "?"}（上限 {step.TimeoutMs ?? 5000} ms）",
        "wait-text" => $"等文字「{step.Text}」（{step.Match ?? TextPredicate.Contains}，" +
                       $"容错 {step.MaxErrors ?? 1}，上限 {step.TimeoutMs ?? 5000} ms）",
        "click-text" => $"找「{step.Text}」并点它的中心（{step.Match ?? TextPredicate.Contains}，" +
                        $"容错 {step.MaxErrors ?? 1}，上限 {step.TimeoutMs ?? 5000} ms）",        "read-text" => $"读一块区域存进 {step.Into}",
        _ => "KeyMouse " + string.Join(' ', FlowDocument.ToArguments(step)),
    };

    private static string Shorten(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " / ").Trim();
        return flat.Length <= 120 ? flat : flat[..117] + "…";
    }
}
