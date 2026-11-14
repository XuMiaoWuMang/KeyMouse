using System.Diagnostics;
using System.Globalization;

namespace KeyMouse.Flow;

/// <summary>
/// Executes a statement tree (`run flow.json`). It is deliberately thin: every executable step is
/// compiled into the same command line a human would type, and dispatched through the same entry
/// point, so selectors, the eligibility gate, focus verification, `--dry-run` and the exit codes mean
/// exactly the same thing here as everywhere else.
///
/// The walk is recursive because conditions decide at run time. What it keeps from the old flat plan:
/// one report row per **executed step**, one `origin` per document row, and exactly one place where
/// pause, cancel, retry, delay and per-step events live. Blocks themselves are not steps; only their
/// bodies are, which is why `origin` still points at the row the user is looking at.
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
            return Commands.Fail(2,
                "用法：KeyMouse run <流程.json> [--delay 毫秒] [--keep-going] [--dry-run] [--retry 次数] " +
                "[--max-steps 步数] [--report 文件.json]");

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

        FlowPlan plan;
        try
        {
            plan = FlowPlan.Build(document, Path.GetFullPath(options.Path!));
        }
        catch (CommandFailure ex)
        {
            return Commands.Fail(ex.Code, ex.Message);
        }
        if (plan.PlannedSteps == 0) return Commands.Fail(2, $"流程 '{options.Path}' 里没有任何步骤");

        string source = Path.GetFullPath(options.Path);
        var report = new ScriptReport
        {
            Script = source,
            StartedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            DryRun = options.DryRun,
            Total = plan.PlannedSteps,
        };

        Console.WriteLine($"流程 {source}");
        Console.WriteLine($"  {plan.PlannedSteps} 步" +
                          (plan.HasDynamicControl ? "（含条件/循环，实际步数以运行为准）" : "") +
                          (options.DryRun ? "（--dry-run：只做资格检查，不发送输入）" : ""));
        if (document.RecordedAt is not null) Console.WriteLine($"  录制于 {document.RecordedAt}");
        if (document.DurationMs is int recordedMs)
            Console.WriteLine($"  时长 {recordedMs / 1000.0:0.#} 秒（按录制时的时间线回放）");

        var root = new VariableFrame(null, VariableFrame.FromTexts(document.Texts));
        if (root.LookupValue("last") is null) root.SetText("last", "");

        var state = new RunState
        {
            Options = options,
            Plan = plan,
            Report = report,
            Execute = execute,
            Root = root,
        };

        _running = true;
        try
        {
            ControlSignal signal = RunBlock(plan.Root, root, state);
            if (signal is ControlSignal.Break or ControlSignal.Continue)
                throw new CommandFailure(2, "break/continue 只能出现在循环里（加载时应该已经拒绝）");
            if (signal == ControlSignal.Return)
                Console.WriteLine("  流程在 return 处结束");
        }
        catch (CommandFailure ex)
        {
            // A condition type error, a broken expression, or a cancellation. These are not step
            // results, so there is no row to attach them to - say it once and stop.
            Console.WriteLine($"  !! {ex.Message}");
            state.ExitCode = ex.Code;
            report.ExitCode = ex.Code;
            // A cancellation is not a failed step; it is the client asking to stop.
            if (ex.Code != 7) report.Failed++;
            report.StoppedAtLine ??= state.StepNo;
        }
        finally
        {
            _running = false;
        }

        int exitCode = state.ExitCode;
        report.ExitCode = exitCode;
        Console.WriteLine();
        Console.WriteLine($"流程结束：{report.Succeeded} 步成功，{report.Failed} 步失败，退出码 {exitCode}");
        if (options.ReportPath is not null)
        {
            // 写报告的人知道实际落点（主路径写不了会改到临时目录），不要在这里
            // 再按用户给的路径打印一遍：那会出现“已写到 A / 报告 B”的自相矛盾。
            ScriptRunner.WriteReport(options.ReportPath, report);
        }
        return exitCode;
    }

    // ------------------------------------------------------------------ the walk

    private enum ControlSignal
    {
        None,
        Break,
        Continue,
        Return,
        Stop,
    }

    private sealed class RunState
    {
        internal required ScriptOptions Options { get; init; }
        internal required FlowPlan Plan { get; init; }
        internal required ScriptReport Report { get; init; }
        internal required Func<string[], int> Execute { get; init; }
        internal required VariableFrame Root { get; init; }

        /// <summary>From the first step of the flow; what expression <c>elapsed()</c> reports.</summary>
        internal Stopwatch FlowClock { get; } = Stopwatch.StartNew();

        /// <summary>Timeline replay (`atMs`) starts when the first step is about to run.</summary>
        internal Stopwatch Timeline { get; } = Stopwatch.StartNew();

        internal int StepNo;
        internal int ExitCode;
        internal long ExecutedSteps;
        internal bool AnyStep;
        internal bool Stop;

        /// <summary>State behind <c>last</c>, <c>found()</c>: the most recent step that read the screen.</summary>
        internal bool LastFound;
        internal string LastText = "";
        internal string? LastNeedle;
        internal string? LastDetail;

        internal bool Dynamic => Plan.HasDynamicControl;
    }

    private static ControlSignal RunBlock(FlowBlock block, VariableFrame frame, RunState state)
    {
        foreach (FlowStatement statement in block.Statements)
        {
            if (state.Stop) return ControlSignal.Stop;
            ControlSignal signal = RunStatement(statement, frame, state);
            if (signal != ControlSignal.None) return signal;
        }
        return ControlSignal.None;
    }

    private static ControlSignal RunStatement(FlowStatement statement, VariableFrame frame, RunState state)
    {
        switch (statement)
        {
            case StepStatement step:
                return ExecuteStep(step, frame, state);

            case IfStatement branch:
            {
                bool holds = EvaluateCondition(branch.Condition, branch.Probe, branch.Origin, "if 的条件", frame, state);
                Console.WriteLine($"  [条件] if {Shorten(branch.Condition.Source)} → {(holds ? "成立" : "不成立")}");
                FlowBlock chosen = holds ? branch.Then : branch.Else ?? EmptyBlock;
                if (chosen.Statements.Count == 0) return ControlSignal.None;
                // A branch has its own scope, like C's braces: a name first introduced inside it
                // does not exist afterwards, while set on an existing outer name still updates it.
                return RunBlock(chosen, new VariableFrame(frame), state);
            }

            case WhileStatement loop:
                return RunWhile(loop, frame, state);

            case DoStatement loop:
                return RunDo(loop, frame, state);

            case ForStatement loop:
                return RunFor(loop, frame, state);

            case RepeatStatement loop:
            {
                for (int round = 0; round < loop.Times; round++)
                {
                    if (state.Stop) return ControlSignal.Stop;
                    Execution.BetweenSteps(state.StepNo);
                    var loopFrame = new VariableFrame(frame, [
                        new KeyValuePair<string, FlowValue>("index", FlowValue.Number(round)),
                    ]);
                    ControlSignal signal = RunBlock(loop.Body, loopFrame, state);
                    if (signal is ControlSignal.Stop or ControlSignal.Return) return signal;
                    if (signal == ControlSignal.Break) break;
                }
                return ControlSignal.None;
            }

            case ForeachStatement loop:
            {
                for (int index = 0; index < loop.Items.Length; index++)
                {
                    if (state.Stop) return ControlSignal.Stop;
                    Execution.BetweenSteps(state.StepNo);
                    var loopFrame = new VariableFrame(frame, [
                        new KeyValuePair<string, FlowValue>("index", FlowValue.Number(index)),
                        new KeyValuePair<string, FlowValue>("item", FlowValue.Text(loop.Items[index])),
                    ]);
                    ControlSignal signal = RunBlock(loop.Body, loopFrame, state);
                    if (signal is ControlSignal.Stop or ControlSignal.Return) return signal;
                    if (signal == ControlSignal.Break) break;
                }
                return ControlSignal.None;
            }

            case CallStatement call:
                return RunCall(call, frame, state);

            case BreakStatement:
                return ControlSignal.Break;

            case ContinueStatement:
                return ControlSignal.Continue;

            case ReturnStatement:
                return ControlSignal.Return;

            default:
                throw new CommandFailure(2, $"运行时遇到不认识的语句：{statement.GetType().Name}");
        }
    }

    private static readonly FlowBlock EmptyBlock = new();

    private static ControlSignal RunWhile(WhileStatement loop, VariableFrame frame, RunState state)
    {
        var loopFrame = new VariableFrame(frame);
        int rounds = 0;
        bool leftByBreak = false;
        while (true)
        {
            Execution.BetweenSteps(state.StepNo);
            if (!EvaluateCondition(loop.Condition, loop.Probe, loop.Origin, "while 的条件", loopFrame, state)) break;

            if (state.Options.DryRun)
            {
                ControlSignal dry = RunBlock(loop.Body, loopFrame, state);
                if (dry is ControlSignal.Stop or ControlSignal.Return) return dry;
                rounds++;
                Console.WriteLine($"  [循环] while {Shorten(loop.Condition.Source)}（--dry-run 只走一遍）");
                return ControlSignal.None;
            }

            if (rounds >= loop.MaxRounds)
            {
                StopWith(state, 3,
                    $"while 循环跑了 {rounds} 轮仍未结束（maxRounds = {loop.MaxRounds}）：{Shorten(loop.Condition.Source)}",
                    loop.Origin);
                return ControlSignal.Stop;
            }

            rounds++;
            ControlSignal signal = RunBlock(loop.Body, loopFrame, state);
            if (signal is ControlSignal.Stop or ControlSignal.Return) return signal;
            if (signal == ControlSignal.Break) { leftByBreak = true; break; }
        }

        Console.WriteLine($"  [循环] while 结束：{rounds} 轮{(leftByBreak ? "（break）" : "")}");
        return ControlSignal.None;
    }

    private static ControlSignal RunDo(DoStatement loop, VariableFrame frame, RunState state)
    {
        var loopFrame = new VariableFrame(frame);
        int rounds = 0;
        bool leftByBreak = false;
        while (true)
        {
            if (!state.Options.DryRun && rounds >= loop.MaxRounds)
            {
                StopWith(state, 3,
                    $"do-while 循环跑了 {rounds} 轮仍未结束（maxRounds = {loop.MaxRounds}）：{Shorten(loop.Condition.Source)}",
                    loop.Origin);
                return ControlSignal.Stop;
            }

            Execution.BetweenSteps(state.StepNo);
            rounds++;
            ControlSignal signal = RunBlock(loop.Body, loopFrame, state);
            if (signal is ControlSignal.Stop or ControlSignal.Return) return signal;
            if (signal == ControlSignal.Break) { leftByBreak = true; break; }

            bool again = EvaluateCondition(loop.Condition, loop.Probe, loop.Origin, "do-while 的条件", loopFrame, state);
            if (state.Options.DryRun)
            {
                Console.WriteLine($"  [循环] do-while {Shorten(loop.Condition.Source)}（--dry-run 只走一遍）");
                return ControlSignal.None;
            }
            if (!again) break;
        }

        Console.WriteLine($"  [循环] do-while 结束：{rounds} 轮{(leftByBreak ? "（break）" : "")}");
        return ControlSignal.None;
    }

    private static ControlSignal RunFor(ForStatement loop, VariableFrame frame, RunState state)
    {
        var loopFrame = new VariableFrame(frame);
        foreach (var (name, expression) in loop.Init)
        {
            loopFrame.Set(name, EvaluateExpression(expression, loop.Probe, loop.Origin, "for 的 init", loopFrame, state));
        }

        int rounds = 0;
        bool leftByBreak = false;
        while (true)
        {
            Execution.BetweenSteps(state.StepNo);
            if (!EvaluateCondition(loop.Condition, loop.Probe, loop.Origin, "for 的条件", loopFrame, state)) break;

            if (state.Options.DryRun)
            {
                ControlSignal dry = RunBlock(loop.Body, loopFrame, state);
                if (dry is ControlSignal.Stop or ControlSignal.Return) return dry;
                if (dry != ControlSignal.Break)
                {
                    foreach (var (name, expression) in loop.Step)
                    {
                        loopFrame.Assign(name, EvaluateExpression(expression, loop.Probe, loop.Origin, "for 的 step", loopFrame, state));
                    }
                }
                rounds++;
                Console.WriteLine($"  [循环] for {Shorten(loop.Condition.Source)}（--dry-run 只走一遍）");
                return ControlSignal.None;
            }

            if (rounds >= loop.MaxRounds)
            {
                StopWith(state, 3,
                    $"for 循环跑了 {rounds} 轮仍未结束（maxRounds = {loop.MaxRounds}）：{Shorten(loop.Condition.Source)}",
                    loop.Origin);
                return ControlSignal.Stop;
            }

            rounds++;
            ControlSignal signal = RunBlock(loop.Body, loopFrame, state);
            if (signal is ControlSignal.Stop or ControlSignal.Return) return signal;
            if (signal == ControlSignal.Break) { leftByBreak = true; break; }

            foreach (var (name, expression) in loop.Step)
            {
                loopFrame.Assign(name, EvaluateExpression(expression, loop.Probe, loop.Origin, "for 的 step", loopFrame, state));
            }
        }

        Console.WriteLine($"  [循环] for 结束：{rounds} 轮{(leftByBreak ? "（break）" : "")}");
        return ControlSignal.None;
    }

    private static ControlSignal RunCall(CallStatement call, VariableFrame frame, RunState state)
    {
        var overrides = new Dictionary<string, FlowValue>(StringComparer.Ordinal);
        foreach (var (name, value) in call.Vars)
        {
            overrides[name] = FlowValue.Text(FlowDocument.Expand(value, frame));
        }

        var callFrame = new VariableFrame(
            new VariableFrame(frame, VariableFrame.FromTexts(call.Document.Texts)), overrides);

        ControlSignal signal = RunBlock(call.Body, callFrame, state);
        if (signal == ControlSignal.Stop) return ControlSignal.Stop;
        if (signal is ControlSignal.Break or ControlSignal.Continue)
            throw new CommandFailure(2, "子流程里不能 break/continue 到调用者的循环（加载时应该已经拒绝）");

        // Export is the caller's side of the promise: only names listed there leave the subflow.
        foreach (string name in call.Export)
        {
            if (callFrame.LookupValue(name) is { } value) frame.Set(name, value);
        }
        return ControlSignal.None;
    }

    // ------------------------------------------------------------------ one step

    private static ControlSignal ExecuteStep(StepStatement statement, VariableFrame frame, RunState state)
    {
        if (state.Options.MaxSteps > 0 && state.ExecutedSteps >= state.Options.MaxSteps)
        {
            StopWith(state, 3,
                $"超过 --max-steps（{state.Options.MaxSteps} 步）：流程可能写成了死循环", statement.Origin);
            return ControlSignal.Stop;
        }

        // Delay is between steps, not after the last one - what the flat plan did, said the same way.
        if (state.AnyStep && state.Options.DelayMs > 0) Thread.Sleep(state.Options.DelayMs);
        state.AnyStep = true;
        state.ExecutedSteps++;
        state.StepNo++;
        int stepNo = state.StepNo;

        // The Runner's seam: no control installed (CLI) = no-op; a client gets pause/cancel and the
        // row that is executing right now.
        Execution.BetweenSteps(stepNo);

        FlowStep step = FlowDocument.Resolve(statement.Step, frame);

        if (state.Plan.HasTimeline && !state.Options.DryRun)
        {
            // 等到时间线上的这一刻。等到就已经落后（例如上一步很慢）时不再补等，
            // 直接往下走：宁可晚一点，也不要越拖越晚。
            if (step.AtMs is int atMs) WaitForTimeline(state.Timeline, atMs, stepNo);

            // sleep 的等待已经由 atMs 表达，执行它会变成双倍等待。
            if (step.Type == "sleep" && step.AtMs is not null) step = new FlowStep { Type = "sleep", Ms = 0 };
        }

        var record = new CommandRecord { Index = stepNo, Line = stepNo, Origin = statement.Origin };
        var clock = Stopwatch.StartNew();
        int attempt = 0;
        int code;
        string output;
        bool skipped = false;
        Execution.StepStarted(stepNo, step.Type, statement.Origin, step);

        // 给桌面 HUD 一条“现在开始”的行。只在编辑器起的进程里打（外壳设 KEYMOUSE_HUD=1），
        // 直接跑命令行时输出保持原样。HUD 按步骤号合并开始/完成两行，所以日志不会翻倍。
        if (!state.Dynamic && Environment.GetEnvironmentVariable("KEYMOUSE_HUD") == "1")
        {
            Console.WriteLine($"  [{stepNo,3}/{state.Plan.PlannedSteps}] 执行中  {Describe(step)}");
        }

        // A step may carry a precondition: run it only if that text is on screen in time.
        // Skipping sends nothing, and failing is exit 3 for the same reason - the caller can
        // retry without wondering whether half of something happened.
        if (step.When is { } condition)
        {
            PollResult when = EvaluateWhen(step, condition, state.Options.DryRun, state.Options.AllowRestore, stepNo);
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
                    state.Report.Commands.Add(record);
                    Execution.StepFinished(stepNo, code, clock.ElapsedMilliseconds);
                    state.Report.Failed++;
                    state.ExitCode = code;
                    state.Report.ExitCode = code;
                    state.Report.StoppedAtLine = stepNo;
                    return ControlSignal.Stop;
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

        while (!skipped)
        {
            attempt++;
            (code, output) = RunStep(statement, step, state.Options, state.Execute, stepNo, frame, state);
            if (code == 0 || attempt > state.Options.Retry || !ScriptRunner.IsRetryable(code)) break;
            Console.WriteLine($"  [{stepNo,3}] 第 {attempt} 次重试（退出码 {code} 可重试，等 {state.Options.RetryDelayMs} ms）");
            Thread.Sleep(state.Options.RetryDelayMs);
        }

        clock.Stop();
        Execution.StepFinished(stepNo, code, clock.ElapsedMilliseconds);
        record.Attempts = attempt;
        record.ExitCode = code;
        record.DurationMs = clock.ElapsedMilliseconds;
        record.Output = output;
        record.Command = Describe(step);
        record.Skipped = skipped;
        state.Report.Commands.Add(record);

        string tag = skipped ? "skip" : code == 0 ? "ok" : $"exit {code}";
        string counter = state.Dynamic
            ? $"[{stepNo,3}]"
            : $"[{stepNo,3}/{state.Plan.PlannedSteps}]";
        Console.WriteLine($"  {counter} {tag,-8} {record.Command}" +
                          (output.Length > 0 && (state.Options.Echo || code != 0 || skipped)
                              ? $"  <- {Shorten(output)}"
                              : ""));

        if (code == 0)
        {
            state.Report.Succeeded++;
            return ControlSignal.None;
        }

        state.Report.Failed++;
        if (!state.Options.KeepGoing)
        {
            state.ExitCode = code;
            state.Report.ExitCode = code;
            state.Report.StoppedAtLine = stepNo;
            return ControlSignal.Stop;
        }

        state.ExitCode = state.ExitCode == 0 ? code : state.ExitCode;
        return ControlSignal.None;
    }

    private static void StopWith(RunState state, int code, string message, string origin)
    {
        if (state.Stop) return;
        Console.WriteLine($"  !! {message}");
        state.Report.Commands.Add(new CommandRecord
        {
            Index = state.StepNo,
            Line = state.StepNo,
            Origin = origin,
            Command = message,
            ExitCode = code,
            Attempts = 1,
            Output = message,
        });
        state.Report.Failed++;
        state.Report.ExitCode = code;
        state.Report.StoppedAtLine = state.StepNo;
        state.ExitCode = code;
        state.Stop = true;
    }

    private static bool EvaluateCondition(
        Expression expression, FlowProbe? probe, string origin, string what, VariableFrame frame, RunState state)
    {
        FlowValue value = EvaluateExpression(expression, probe, origin, what, frame, state);
        return value.AsBool($"第 {origin} 步的 {what}");
    }

    private static FlowValue EvaluateExpression(
        Expression expression, FlowProbe? probe, string origin, string what, VariableFrame frame, RunState state)
    {
        Execution.BetweenSteps(state.StepNo);
        return expression.Evaluate(frame, new Sense(state, FlowDocument.ResolveProbe(probe, frame), state.StepNo));
    }

    private static (int ExitCode, string Output) RunStep(
        StepStatement statement, FlowStep step, ScriptOptions options, Func<string[], int> execute,
        int stepIndex, VariableFrame frame, RunState state)
    {
        switch (step.Type)
        {
            case "sleep":
                int ms = Number(step, "ms", 0);
                if (!options.DryRun) Thread.Sleep(ms);
                return (0, $"等待 {ms} ms");

            case "set":
            {
                FlowValue value = statement.Value!.Evaluate(frame,
                    new Sense(state, FlowDocument.ResolveProbe(statement.Probe, frame), stepIndex));
                frame.Assign(step.Name!, value);
                return (0, $"{step.Name} = {value.AsText()}");
            }

            case "assert":
            {
                bool holds = statement.Assertion!.Evaluate(frame,
                        new Sense(state, FlowDocument.ResolveProbe(statement.Probe, frame), stepIndex))
                    .AsBool($"第 {stepIndex} 步的 assert");
                if (holds) return (0, "成立");
                string reason = string.IsNullOrWhiteSpace(step.Message)
                    ? $"断言不成立：{Shorten(statement.Assertion.Source)}"
                    : step.Message!;
                return (3, reason);
            }

            // 长按是一步，不是三步：按下 → 按住 → 松开在同一步里做完。
            // 写成一步的好处是它可以被当一个操作来编辑（按键/落点/时长/窗口），
            // 而文件里也不会出现"按下"和"松开"被别的步骤隔开却仍算一次长按的情况。
            case "hold":
            case "mousehold":
            case "keyhold":
            {
                int holdMs = Number(step, "ms", 0);
                string what = step.Button is { Length: > 0 } button
                    ? $"鼠标{button switch { "right" => "右键", "middle" => "中键", _ => "左键" }}"
                    : $"键盘 {step.Combo ?? step.Text ?? "?"}";
                if (options.DryRun) return (0, $"长按 {what} {holdMs} ms（试运行未真的按）");

                bool mouse = step.Type == "mousehold" || (step.Type == "hold" && step.Button is { Length: > 0 });
                if (mouse && step.At is { Space: FlowSpace.Screen } at) NativeInput.MoveTo(at.X, at.Y);
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
                return WaitText(step, options, stepIndex, state);

            case "click-text":
                return ClickText(step, options, execute, stepIndex, state);

            case "read-text":
                return ReadText(step, options, stepIndex, frame, state);

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

    // ------------------------------------------------------------------ predicates

    /// <summary>
    /// The outside world as an expression sees it. One instance per evaluation, carrying the probe of
    /// the statement whose condition is being evaluated; tests stub the interface instead of this.
    /// </summary>
    private sealed class Sense : IExpressionRuntime
    {
        private readonly RunState _state;
        private readonly FlowProbe? _probe;
        private readonly int _stepIndex;

        internal Sense(RunState state, FlowProbe? probe, int stepIndex)
        {
            _state = state;
            _probe = probe;
            _stepIndex = stepIndex;
        }

        public FlowValue Call(string function, IReadOnlyList<FlowValue> args) => function switch
        {
            "text" => Text(args),
            "window" => FlowValue.Bool(WindowExists(args[0].AsText())),
            "front" => FlowValue.Bool(IsFront(args[0].AsText())),
            "found" => FlowValue.Bool(Found(args)),
            "elapsed" => FlowValue.Number(_state.FlowClock.ElapsedMilliseconds),
            _ => throw new CommandFailure(2, $"不认识的函数 '{function}'"),
        };

        private FlowValue Text(IReadOnlyList<FlowValue> args)
        {
            if (_probe?.Region is not { } region ||
                _probe.Target is not { } target ||
                (string.IsNullOrWhiteSpace(target.Process) && string.IsNullOrWhiteSpace(target.Class)))
            {
                throw new CommandFailure(2, "text() 需要这一步的 probe（目标窗口与读的区域）");
            }

            string needle = args[0].AsText();
            if (args.Count == 5)
            {
                region = new FlowRegion
                {
                    Space = region.Space,
                    X = Whole(args[1], "x"),
                    Y = Whole(args[2], "y"),
                    Width = Whole(args[3], "宽"),
                    Height = Whole(args[4], "高"),
                };
            }

            bool windowSpace = region.Space == FlowSpace.Window;
            var (x, y, width, height) = region.Rect();
            var selector = new WindowSelector
            {
                ProcessName = string.IsNullOrWhiteSpace(target.Process) ? null : target.Process,
                ClassName = string.IsNullOrWhiteSpace(target.Class) ? null : target.Class,
            };
            string mode = _probe.Match ?? TextPredicate.Contains;
            int errors = _probe.MaxErrors ?? 1;

            if (_state.Options.DryRun)
            {
                var dry = WindowResolver.Resolve(selector, _state.Options.AllowRestore);
                if (dry.Usable.Count == 0)
                    throw new CommandFailure(4, $"（dry-run）text() 的目标窗口现在不可用：{selector.Describe()}");
                try
                {
                    Probe.ValidateRegion(dry.Usable[0], windowSpace, (x, y, width, height));
                }
                catch (ArgumentException ex)
                {
                    throw new CommandFailure(2, ex.Message);
                }
                return FlowValue.Bool(true);
            }

            Execution.BetweenSteps(_stepIndex);
            var resolution = WindowResolver.Resolve(selector, _state.Options.AllowRestore);
            if (resolution.Usable.Count == 0)
            {
                _state.LastDetail = $"目标窗口不可用（{selector.Describe()}）";
                return FlowValue.Bool(false);
            }

            try
            {
                var (read, _, _) = Probe.ReadOnce(resolution.Usable[0], windowSpace, (x, y, width, height));
                bool found = TextPredicate.Matches(read, needle, mode, errors, out string why);
                _state.LastDetail = found ? $"text(\"{needle}\") 成立" : why;
                return FlowValue.Bool(found);
            }
            catch (CommandFailure ex)
            {
                _state.LastDetail = ex.Message;
                return FlowValue.Bool(false);
            }
            catch (ArgumentException ex)
            {
                _state.LastDetail = ex.Message;
                return FlowValue.Bool(false);
            }
        }

        private bool Found(IReadOnlyList<FlowValue> args)
        {
            if (!_state.LastFound) return false;
            if (args.Count == 0) return true;
            string wanted = args[0].AsText();
            if (string.Equals(_state.LastNeedle, wanted, StringComparison.Ordinal)) return true;
            return TextPredicate.Matches(_state.LastText, wanted, TextPredicate.Contains, 0, out _);
        }

        private bool WindowExists(string process)
        {
            string name = ProcessName(process);
            var resolution = WindowResolver.Resolve(
                new WindowSelector { ProcessName = name }, _state.Options.AllowRestore);
            return resolution.Usable.Count > 0;
        }

        private bool IsFront(string process)
        {
            IntPtr foreground = NativeWindow.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            NativeWindow.GetWindowThreadProcessId(foreground, out uint pid);
            return pid != 0 &&
                   string.Equals(NativeWindow.ProcessNameOf(pid), ProcessName(process), StringComparison.OrdinalIgnoreCase);
        }

        private static string ProcessName(string value)
        {
            string name = value.Trim();
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        }

        private static int Whole(FlowValue value, string what)
        {
            double number = value.AsNumber($"text() 的第 {what} 个参数");
            if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
                throw new CommandFailure(2, $"text() 的{what}坐标必须是整数，得到 {value.AsText()}");
            return (int)number;
        }
    }

    // ------------------------------------------------------------------ one action

    /// <summary>
    /// Reads a region once and stores what was read in a variable, so a flow can carry data from one
    /// step to the next ("read the total, then type it somewhere else"). With `text` given it stores
    /// the matched piece instead of the whole line - the same locator `--find` uses.
    /// </summary>
    private static (int ExitCode, string Output) ReadText(
        FlowStep step, ScriptOptions options, int stepIndex, VariableFrame frame, RunState state)
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
                    Number(step, "maxErrors", 1));
                if (match is null)
                {
                    Remember(state, false, step.Text, text);
                    return (3, $"没找到「{step.Text}」（读到「{text}」）——{step.Into} 没被改动");
                }
                value = match.Matched;
            }

            frame.SetText(step.Into!, value);
            Remember(state, true, step.Text, value);
            return (0, $"{step.Into} = 「{value}」" + (confidence is double c ? $"（置信度 {c:0.0}）" : ""));
        }
        catch (CommandFailure ex)
        {
            Remember(state, false, step.Text, state.LastText);
            return (ex.Code, ex.Message);
        }
    }


    /// <summary>The one place that updates `last` / `found()`: a screen read is the only thing that does.</summary>
    private static void Remember(RunState state, bool found, string? needle, string text)
    {
        state.LastFound = found;
        state.LastNeedle = needle;
        state.LastText = text;
        state.Root.SetText("last", text);
    }

    /// <summary>Waits until a usable window matches the step's selector, then reports how long it took.</summary>
    private static (int ExitCode, string Output) WaitWindow(FlowStep step, ScriptOptions options)
    {
        int timeoutMs = Number(step, "timeoutMs", 5000);
        int intervalMs = Number(step, "intervalMs", 200);
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
            return new PollResult(true, null, 0,
                $"（dry-run）会等「{expected}」最多 {timeoutMs} ms（匹配方式 {mode}）", "");
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
        step.Match ?? TextPredicate.Contains, Number(step, "maxErrors", 1),
        Number(step, "timeoutMs", 5000), Number(step, "intervalMs", 200),
        Number(step, "confirm", 2), dryRun, allowRestore, stepIndex);

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

    private static (int ExitCode, string Output) WaitText(
        FlowStep step, ScriptOptions options, int stepIndex, RunState state)
    {
        WindowSelector selector = SelectorOf(step.Target);
        if (!HasSelector(selector))
            return (2, "wait-text 需要 target（至少给 process 或 class，否则不知道该读哪个窗口）");

        PollResult poll = PollStep(step, options.DryRun, options.AllowRestore, stepIndex);
        if (!options.DryRun) Remember(state, poll.Found, step.Text, poll.Match?.Matched ?? poll.LastText);
        return poll.ExitCode == 0 ? (0, poll.Detail) : (poll.ExitCode, poll.Detail);
    }

    /// <summary>
    /// The step that acts on what it sees: find the text, then click the middle of the box it was
    /// found in. The point is computed from the engine's own boxes (client coordinates, because the
    /// region is client-relative), so "read the screen" and "click the thing" cannot disagree about
    /// where it is.
    /// </summary>
    private static (int ExitCode, string Output) ClickText(
        FlowStep step, ScriptOptions options, Func<string[], int> execute, int stepIndex, RunState state)
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

        // The read succeeded before the click; last/found() should describe the read, not whether
        // the click itself landed.
        Remember(state, true, step.Text, poll.Match.Matched);

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
            condition.Match ?? TextPredicate.Contains, ShapeNumber("when", condition.MaxErrors, "maxErrors", 1),
            ShapeNumber("when", condition.TimeoutMs, "timeoutMs", 2000), ShapeNumber("when", condition.IntervalMs, "intervalMs", 200),
            ShapeNumber("when", condition.Confirm, "confirm", 2), dryRun, allowRestore, stepIndex);

    /// <summary>
    /// 一个数字字段的取值：没填就用契约的默认值，填了也按契约夹进范围。
    ///
    /// 加载时已经查过越界（`FlowModel.CheckRanges` 会把越界当错误），这里再夹一次是因为运行时的值
    /// 可能来自占位符替换或子流程参数——夹取总好过睡一个负毫秒。上下限同样出自契约，不在这里另写。
    /// </summary>
    private static int Number(FlowStep step, string field, int whenAbsent)
    {
        int? given = field switch
        {
            "ms" => step.Ms,
            "timeoutMs" => step.TimeoutMs,
            "intervalMs" => step.IntervalMs,
            "maxErrors" => step.MaxErrors,
            "confirm" => step.Confirm,
            _ => null,
        };

        if (!FlowSchema.TryGetNumberRule(step.Type, field, out NumberRule rule)) return given ?? whenAbsent;
        return given is int value ? rule.Clamp(value) : rule.Fallback(whenAbsent);
    }

    /// <summary>同上，但读的是 `when` 这种嵌套形状的字段（`shapes.when`）。</summary>
    private static int ShapeNumber(string kind, int? given, string field, int whenAbsent)
    {
        if (!FlowSchema.TryGetShapeNumberRule(kind, field, out NumberRule rule)) return given ?? whenAbsent;
        return given is int value ? rule.Clamp(value) : rule.Fallback(whenAbsent);
    }

    private static string Describe(FlowStep step) => step.Type switch
    {
        "sleep" => $"等 {step.Ms ?? 0} ms",
        "set" => $"设 {step.Name} = {step.Value}",
        "assert" => $"确认 {step.Cond}",
        "wait-window" => $"等窗口 {step.Target?.Process ?? step.Target?.Class ?? "?"}（上限 {step.TimeoutMs ?? 5000} ms）",
        "wait-text" => $"等文字「{step.Text}」（{step.Match ?? TextPredicate.Contains}，" +
                       $"容错 {step.MaxErrors ?? 1}，上限 {step.TimeoutMs ?? 5000} ms）",
        "click-text" => $"找「{step.Text}」并点它的中心（{step.Match ?? TextPredicate.Contains}，" +
                        $"容错 {step.MaxErrors ?? 1}，上限 {step.TimeoutMs ?? 5000} ms）",
        "read-text" => $"读一块区域存进 {step.Into}",
        _ => "KeyMouse " + string.Join(' ', FlowDocument.ToArguments(step)),
    };

    private static string Shorten(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " / ").Trim();
        return flat.Length <= 120 ? flat : flat[..117] + "…";
    }
}
