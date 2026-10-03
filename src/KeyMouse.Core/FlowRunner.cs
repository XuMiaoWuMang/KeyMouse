using System.Diagnostics;
using System.Globalization;

namespace KeyMouse;

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
            document = FlowDocument.Load(options.Path);
        }
        catch (CommandFailure ex)
        {
            return Commands.Fail(ex.Code, ex.Message);
        }

        if (document.Steps.Count == 0) return Commands.Fail(2, $"流程 '{options.Path}' 里没有任何步骤");

        string source = Path.GetFullPath(options.Path);
        var report = new ScriptReport
        {
            Script = source,
            StartedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            DryRun = options.DryRun,
            Total = document.Steps.Count,
        };

        Console.WriteLine($"流程 {source}");
        Console.WriteLine($"  {document.Steps.Count} 步" + (options.DryRun ? "（--dry-run：只做资格检查，不发送输入）" : ""));
        if (document.RecordedAt is not null) Console.WriteLine($"  录制于 {document.RecordedAt}");

        int exitCode = 0;
        _running = true;
        try
        {
            for (int i = 0; i < document.Steps.Count; i++)
            {
                // The Runner's seam: with no control installed (the CLI path) these are no-ops; with a
                // client attached they block while paused, throw when cancelled, and report the step
                // boundary so a UI can highlight the row that is executing right now.
                Execution.BetweenSteps(i + 1);

                FlowStep step = document.Steps[i];
                var record = new CommandRecord { Index = i + 1, Line = i + 1 };
                var clock = Stopwatch.StartNew();
                int attempt = 0;
                int code;
                string output;
                Execution.StepStarted(i + 1, step.Type);

                while (true)
                {
                    attempt++;
                    (code, output) = RunStep(step, options, execute);
                    if (code == 0 || attempt > options.Retry || !ScriptRunner.IsRetryable(code)) break;
                    Console.WriteLine($"  [{i + 1,3}] 第 {attempt} 次重试（退出码 {code} 可重试，等 {options.RetryDelayMs} ms）");
                    Thread.Sleep(options.RetryDelayMs);
                }

                clock.Stop();
                Execution.StepFinished(i + 1, code, clock.ElapsedMilliseconds);
                record.Attempts = attempt;
                record.ExitCode = code;
                record.DurationMs = clock.ElapsedMilliseconds;
                record.Output = output;
                record.Command = Describe(step);
                report.Commands.Add(record);

                string tag = code == 0 ? "ok" : $"exit {code}";
                Console.WriteLine($"  [{i + 1,3}/{document.Steps.Count}] {tag,-8} {record.Command}" +
                                  (output.Length > 0 && (options.Echo || code != 0) ? $"  <- {Shorten(output)}" : ""));

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
                        report.StoppedAtLine = i + 1;
                        break;
                    }
                    exitCode = exitCode == 0 ? code : exitCode;
                }

                if (options.DelayMs > 0 && i < document.Steps.Count - 1) Thread.Sleep(options.DelayMs);
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

    private static (int ExitCode, string Output) RunStep(FlowStep step, ScriptOptions options, Func<string[], int> execute)
    {
        switch (step.Type)
        {
            case "sleep":
                int ms = Math.Clamp(step.Ms ?? 0, 0, 3_600_000);
                if (!options.DryRun) Thread.Sleep(ms);
                return (0, $"等待 {ms} ms");

            case "wait-window":
                return WaitWindow(step, options);

            case "wait-text":
                return WaitText(step, options);

            default:
                string[] argv = FlowDocument.ToArguments(step);
                if (options.DryRun) argv = [.. argv, "--dry-run"];
                var tokens = new List<string>(argv);
                return ScriptRunner.RunOnce(execute, tokens);
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
            var resolution = WindowResolver.Resolve(windowSelector, allowRestore: false);
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
    /// Waits until the region reads as the expected text: one OCR read per poll, and the match has to
    /// hold for `confirm` consecutive polls **with the same text** before it counts. One poll would be
    /// a coin flip on a frame caught mid-repaint (measured: a caret or a repaint makes a single read
    /// disagree with the next), so the confirmation is what makes the predicate trustworthy rather
    /// than fast. Every poll is a real engine call (~0.2-0.6 s at this region size), which is why the
    /// default interval is short and the timeout is the caller's statement of patience.
    /// </summary>
    private static (int ExitCode, string Output) WaitText(FlowStep step, ScriptOptions options)
    {
        int timeoutMs = Math.Clamp(step.TimeoutMs ?? 5000, 0, 600_000);
        int intervalMs = Math.Clamp(step.IntervalMs ?? 250, 0, 5000);
        int confirm = Math.Clamp(step.Confirm ?? 2, 1, 10);
        int maxErrors = Math.Clamp(step.MaxErrors ?? 1, 0, 20);
        string mode = step.Match ?? TextPredicate.Contains;
        string expected = step.Text ?? "";
        FlowRegion region = step.Region!;

        var selector = new WindowSelector
        {
            ProcessName = string.IsNullOrWhiteSpace(step.Target?.Process) ? null : step.Target!.Process,
            ClassName = string.IsNullOrWhiteSpace(step.Target?.Class) ? null : step.Target!.Class,
        };
        if (selector.ProcessName is null && selector.ClassName is null)
            return (2, "wait-text 需要 target（至少给 process 或 class，否则不知道该读哪个窗口）");

        if (options.DryRun)
        {
            // No engine call in a dry run: resolve the window and check the rectangle, which is what a
            // dry run can honestly verify about a condition.
            var resolution = WindowResolver.Resolve(selector, allowRestore: false);
            if (resolution.Usable.Count == 0) return (4, $"（dry-run）目标窗口现在不可用：{selector.Describe()}");
            try
            {
                Probe.ValidateRegion(resolution.Usable[0], windowSpace: false, region.Rect());
            }
            catch (ArgumentException ex)
            {
                return (2, ex.Message);
            }
            return (0, $"（dry-run）会等「{expected}」最多 {timeoutMs} ms（匹配方式 {mode}）");
        }

        var clock = Stopwatch.StartNew();
        int consecutive = 0;
        string lastText = "";
        string lastDetail = "还没有读到东西";
        string confirmedText = "";

        while (true)
        {
            var resolution = WindowResolver.Resolve(selector, allowRestore: false);
            if (resolution.Usable.Count == 0)
            {
                consecutive = 0;
                lastDetail = $"目标窗口不可用（{selector.Describe()}）";
            }
            else
            {
                try
                {
                    var (text, _) = Probe.ReadOnce(resolution.Usable[0], windowSpace: false, region.Rect());
                    lastText = text;
                    if (TextPredicate.Matches(text, expected, mode, maxErrors, out string why))
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
                            return (0, $"等到「{text}」（{clock.ElapsedMilliseconds} ms，" +
                                       $"连续 {consecutive} 次读到同一段文字）");
                        }
                    }
                    else
                    {
                        consecutive = 0;
                        confirmedText = "";
                        lastDetail = why;
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
                return (3, $"等了 {timeoutMs} ms 没等到「{expected}」：{lastDetail}" +
                           (lastText.Length > 0 ? $"（最后读到「{lastText}」）" : ""));
            }
            if (intervalMs > 0) Thread.Sleep(intervalMs);
        }
    }

    private static string Describe(FlowStep step) => step.Type switch
    {
        "sleep" => $"等 {step.Ms ?? 0} ms",
        "wait-window" => $"等窗口 {step.Target?.Process ?? step.Target?.Class ?? "?"}（上限 {step.TimeoutMs ?? 5000} ms）",
        "wait-text" => $"等文字「{step.Text}」（{step.Match ?? TextPredicate.Contains}，" +
                       $"容错 {step.MaxErrors ?? 1}，上限 {step.TimeoutMs ?? 5000} ms）",
        _ => "KeyMouse " + string.Join(' ', FlowDocument.ToArguments(step)),
    };

    private static string Shorten(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " / ").Trim();
        return flat.Length <= 120 ? flat : flat[..117] + "…";
    }
}
