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

    internal static int Run(string[] args, Program.GlobalOptions global, Func<string[], int> execute)
    {
        if (_running) return Program.Fail(2, "run：流程里不能再跑流程（拒绝嵌套）");
        if (global.HasSelector || global.Wx.HasValue || global.Wy.HasValue || global.Pick is not null)
            return Program.Fail(2, "run：窗口选择器要写在步骤里，不能挂在 run 自己身上");

        var options = new ScriptOptions();
        if (ScriptRunner.ParseOptions(args, options) is { } optionError) return Program.Fail(2, optionError);
        if (options.Path is null)
            return Program.Fail(2, "用法：KeyMouse run <流程.json> [--delay 毫秒] [--keep-going] [--dry-run] [--retry 次数] [--report 文件.json]");

        FlowDocument document;
        try
        {
            document = FlowDocument.Load(options.Path);
        }
        catch (CommandFailure ex)
        {
            return Program.Fail(ex.Code, ex.Message);
        }

        if (document.Steps.Count == 0) return Program.Fail(2, $"流程 '{options.Path}' 里没有任何步骤");

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
                FlowStep step = document.Steps[i];
                var record = new CommandRecord { Index = i + 1, Line = i + 1 };
                var clock = Stopwatch.StartNew();
                int attempt = 0;
                int code;
                string output;

                while (true)
                {
                    attempt++;
                    (code, output) = RunStep(step, options, execute);
                    if (code == 0 || attempt > options.Retry || !ScriptRunner.IsRetryable(code)) break;
                    Console.WriteLine($"  [{i + 1,3}] 第 {attempt} 次重试（退出码 {code} 可重试，等 {options.RetryDelayMs} ms）");
                    Thread.Sleep(options.RetryDelayMs);
                }

                clock.Stop();
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
                // The predicate lands with the probe work it needs; failing loudly beats pretending.
                return (2, "wait-text 还没实现（需要 probe 的判定谓词，下一步做）");

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

    private static string Describe(FlowStep step) => step.Type switch
    {
        "sleep" => $"等 {step.Ms ?? 0} ms",
        "wait-window" => $"等窗口 {step.Target?.Process ?? step.Target?.Class ?? "?"}（上限 {step.TimeoutMs ?? 5000} ms）",
        "wait-text" => $"等文字「{step.Text}」",
        _ => "KeyMouse " + string.Join(' ', FlowDocument.ToArguments(step)),
    };

    private static string Shorten(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " / ").Trim();
        return flat.Length <= 120 ? flat : flat[..117] + "…";
    }
}
