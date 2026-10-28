using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KeyMouse.Runtime;
/// <summary>Switches the send / focus layers consult while a batch runs.</summary>
internal static class ExecutionMode
{
    public static bool DryRun { get; set; }

    /// <summary>Low-level input events actually injected through SendInput.</summary>
    public static long InjectedEvents { get; private set; }

    /// <summary>Input events that would have been injected but --dry-run suppressed them.</summary>
    public static long SuppressedEvents { get; private set; }

    public static void NoteInjected(int count) => InjectedEvents += count;
    public static void NoteSuppressed(int count) => SuppressedEvents += count;
}

/// <summary>Everything `run` was asked to do.</summary>
internal sealed class ScriptOptions
{
    public string? Path;
    public int DelayMs;
    public bool KeepGoing;
    public bool DryRun;
    public bool Echo;
    public int Retry;
    public int RetryDelayMs = 300;
    public string? ReportPath;

    /// <summary>
    /// May a minimized window be restored? Opt-in from two places - `--allow-restore` on the command
    /// line, or `"allowRestore": true` in a flow file - because bringing a window back is a visible
    /// change to someone's desktop.
    /// </summary>
    public bool AllowRestore;

    /// <summary>The flow asked for it; the command line asked for it; either is enough.</summary>
    public void AllowRestoreFrom(bool document) => AllowRestore |= document;
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);
}

internal sealed class CommandRecord
{
    public int Index { get; set; }
    public int Line { get; set; }
    public string Command { get; set; } = "";

    /// <summary>The target options this line inherited from an earlier line, if any.</summary>
    public string? InheritedTarget { get; set; }

    /// <summary>Loop variable values for this execution, e.g. { "row": 1, "col": 2 }.</summary>
    public Dictionary<string, int>? Iterations { get; set; }
    public int ExitCode { get; set; }

    /// <summary>True when the step's `when` precondition did not hold and the step was skipped:
    /// nothing was sent, and the exit code stays 0.</summary>
    public bool Skipped { get; set; }
    public int Attempts { get; set; }
    public long DurationMs { get; set; }
    public long InjectedEvents { get; set; }
    public string Output { get; set; } = "";
}

internal sealed class ScriptReport
{
    public string Script { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public bool DryRun { get; set; }
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int RetriedCommands { get; set; }
    public long InjectedEvents { get; set; }
    public long SuppressedEvents { get; set; }
    public int ExitCode { get; set; }
    public int? StoppedAtLine { get; set; }
    public List<CommandRecord> Commands { get; set; } = new();
}

/// <summary>
/// Runs a batch of KeyMouse commands, one per line, in order. Every line goes through
/// exactly the same dispatcher as a standalone invocation, so selectors, the eligibility
/// gate and the focus verification apply per line - the runner adds sequencing, not a
/// second command language.
/// </summary>
internal static partial class ScriptRunner
{
    private static bool _running;

    public static int Run(string[] args, Commands.GlobalOptions global, Func<string[], int> execute)
    {
        if (_running)
            return Fail(2, "run：不允许脚本里再跑脚本（拒绝嵌套）");

        if (global.HasSelector || global.Wx.HasValue || global.Wy.HasValue || global.Pick is not null)
            return Fail(2, "run：窗口选择器要写在命令上，不能挂在 run 自己身上");

        var options = new ScriptOptions();
        if (ParseOptions(args, options) is { } optionError) return Fail(2, optionError);
        if (options.Path is null)
            return Fail(2, "用法：KeyMouse run <文件|-> [--delay 毫秒] [--keep-going] [--dry-run] [--echo] " +
                           "[--retry 次数] [--retry-delay 毫秒] [--set 名=值] [--report 文件.json]");

        List<(int LineNumber, List<string> Tokens)> commands;
        LoopPlan plan;
        try
        {
            commands = Parse(ReadScript(options.Path));
            plan = AnalyzeLoops(commands);
            ValidatePseudoCommands(commands);
            ValidateVariables(commands, plan, options.Variables);
        }
        catch (CommandFailure ex)
        {
            return Fail(ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(1, $"读不到脚本 '{options.Path}'：{ex.Message}");
        }

        if (commands.Count == 0 || plan.TotalExecutions == 0)
            return Fail(2, $"脚本 '{options.Path}' 里没有任何命令");

        string source = options.Path == "-" ? "stdin" : Path.GetFullPath(options.Path);
        var report = new ScriptReport
        {
            Script = source,
            StartedAt = DateTimeOffset.Now.ToString("O"),
            DryRun = options.DryRun,
            Total = plan.TotalExecutions
        };

        int firstFailureCode = 0;
        int maxAttempts = 1 + Math.Max(0, options.Retry);

        ExecutionMode.DryRun = options.DryRun;
        _running = true;
        try
        {
            string scopeNote = plan.TotalExecutions == commands.Count
                ? $"共 {commands.Count} 条命令"
                : $"共 {commands.Count} 条命令，循环展开后 {plan.TotalExecutions} 条";

            Console.WriteLine(options.DryRun
                ? $"演练模式：{source} {scopeNote}——资格检查照常执行，一个字节都不会发"
                : $"{source} {scopeNote}");

            // Every command in a script - loops included - is dispatched in this one process.
            // Nothing here spawns a process; the count is what the progress line reports.
            int total = plan.TotalExecutions;
            int dispatched = 0;
            List<string>? currentTarget = null;
            var loopStack = new List<(int BodyStart, int Remaining, int Iteration, string Name)>();
            var scope = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int pc = 0; pc < commands.Count; pc++)
            {
                var (lineNumber, written) = commands[pc];
                string head = written[0].ToLowerInvariant();

                if (head == "repeat")
                {
                    var (count, name, _) = ParseRepeat(written);
                    if (count == 0) pc = plan.EndOf[pc];   // repeat 0: skip the body entirely
                    else loopStack.Add((pc + 1, count, 0, name));
                    continue;
                }

                if (head == "end")
                {
                    var frame = loopStack[^1];
                    loopStack.RemoveAt(loopStack.Count - 1);
                    if (frame.Remaining > 1)
                    {
                        loopStack.Add((frame.BodyStart, frame.Remaining - 1, frame.Iteration + 1, frame.Name));
                        pc = frame.BodyStart - 1;          // the for-loop's ++ lands on the body
                    }
                    continue;
                }

                // Loop variables exist only inside their body; everything else came from --set.
                scope.Clear();
                foreach (var pair in options.Variables) scope[pair.Key] = pair.Value;
                foreach (var frame in loopStack)
                    scope[frame.Name] = frame.Iteration.ToString(CultureInfo.InvariantCulture);

                // Substitution happens here, per execution, because a loop variable changes
                // between iterations. The scan is only worth skipping when no token mentions one.
                List<string> expanded = written.Any(token => token.Contains("${", StringComparison.Ordinal))
                    ? written.Select(token => Substitute(token, scope, lineNumber)).ToList()
                    : written;

                // A line that names a window becomes the script's current target; a later
                // mouse/key line without one inherits it, so the selector is written once.
                // Inheritance only saves typing: the inherited line still goes through the
                // full gate and focus verification on its own.
                List<string>? ownTarget = ExtractTargetTokens(expanded);
                if (ownTarget is not null) currentTarget = ownTarget;

                string inheritedNote = "";
                List<string> tokens = expanded;
                if (ownTarget is null && currentTarget is not null && InheritsTarget(expanded))
                {
                    tokens = new List<string>(expanded);
                    tokens.AddRange(currentTarget);
                    inheritedNote = "   ← 继承目标 " + string.Join(' ', currentTarget.Select(Quote));
                }

                dispatched++;
                string loopNote = loopStack.Count == 0
                    ? ""
                    : "[" + string.Join(' ', loopStack.Select(f => $"{f.Name}={f.Iteration}")) + "] ";

                // The console shows the line as written, with a note saying what was added, so
                // the command stays readable; the report records the effective command.
                string shown = string.Join(' ', expanded.Select(Quote));
                var record = new CommandRecord
                {
                    Index = dispatched,
                    Line = lineNumber,
                    Command = string.Join(' ', tokens.Select(Quote)),
                    InheritedTarget = inheritedNote.Length > 0 ? string.Join(' ', currentTarget!) : null,
                    Iterations = loopStack.Count == 0
                        ? null
                        : loopStack.ToDictionary(frame => frame.Name, frame => frame.Iteration)
                };
                var stopwatch = Stopwatch.StartNew();
                string tag;
                string? captured = null;
                bool isSleep = tokens[0].Equals("sleep", StringComparison.OrdinalIgnoreCase);
                bool isWait = tokens[0].Equals("waitfor", StringComparison.OrdinalIgnoreCase) ||
                              tokens[0].Equals("waitgone", StringComparison.OrdinalIgnoreCase);
                bool showDetail = false;

                if (isSleep)
                {
                    int ms = int.Parse(tokens[1]);
                    Thread.Sleep(ms);
                    record.Attempts = 1;
                    tag = "等待";
                }
                else if (isWait)
                {
                    showDetail = true;
                    record.Attempts = 1;
                    var (waitSelector, timeoutMs, intervalMs, _) = ParseWait(tokens);
                    bool wantPresent = tokens[0].Equals("waitfor", StringComparison.OrdinalIgnoreCase);
                    bool Satisfied() => wantPresent
                        ? WindowResolver.Resolve(waitSelector!, false).Usable.Count > 0
                        : WindowResolver.Resolve(waitSelector!, false).Usable.Count == 0;

                    if (options.DryRun)
                    {
                        // Nothing was sent, so the UI cannot have changed: report the current
                        // state instead of waiting for something that will never happen.
                        tag = "演练";
                        record.ExitCode = 0;
                        captured = Satisfied()
                            ? $"当前状态已经满足 {tokens[0]}"
                            : $"{tokens[0]} 本来最多等 {timeoutMs}ms";
                    }
                    else
                    {
                        var (satisfied, elapsed) = WaitUntil(Satisfied, timeoutMs, intervalMs);
                        record.ExitCode = satisfied ? 0 : 3;
                        tag = satisfied ? "成功" : "失败";
                        captured = satisfied
                            ? $"{tokens[0]} 在 {elapsed}ms 后满足"
                            : $"{tokens[0]} 等了 {timeoutMs}ms 超时——期望中的窗口始终没有出现";
                        if (satisfied) report.Succeeded++;
                        else
                        {
                            report.Failed++;
                            if (firstFailureCode == 0) firstFailureCode = record.ExitCode;
                        }
                    }
                    record.Output = captured ?? "";
                }
                else
                {
                    while (true)
                    {
                        record.Attempts++;
                        long before = ExecutionMode.InjectedEvents;
                        (record.ExitCode, captured) = RunOnce(execute, tokens);
                        record.InjectedEvents += ExecutionMode.InjectedEvents - before;

                        if (record.ExitCode == 0 || !IsRetryable(record.ExitCode) || record.Attempts >= maxAttempts)
                            break;

                        // Retryable means provably nothing was sent, so trying again is safe.
                        Console.WriteLine($"[{dispatched,3}/{total}] 第 {record.Attempts} 次重试（共 {maxAttempts - 1} 次），" +
                                          $"上一次退出码 {record.ExitCode}（确定没有发出任何输入）");
                        if (options.RetryDelayMs > 0) Thread.Sleep(options.RetryDelayMs);
                    }

                    record.Output = captured ?? "";
                    tag = record.ExitCode == 0 ? (options.DryRun ? "演练" : "成功") : "失败";
                    if (record.Attempts > 1) report.RetriedCommands++;
                    if (record.ExitCode != 0)
                    {
                        report.Failed++;
                        if (firstFailureCode == 0) firstFailureCode = record.ExitCode;
                    }
                    else
                    {
                        report.Succeeded++;
                    }
                    captured = record.Output;
                }

                stopwatch.Stop();
                record.DurationMs = stopwatch.ElapsedMilliseconds;
                report.Commands.Add(record);
                report.InjectedEvents = ExecutionMode.InjectedEvents;
                report.SuppressedEvents = ExecutionMode.SuppressedEvents;

                string retryNote = record.Attempts > 1
                    ? record.ExitCode == 0
                        ? $"（重试 {record.Attempts - 1} 次后成功）"
                        : $"（重试 {record.Attempts - 1} 次仍然失败）"
                    : "";
                Console.WriteLine($"[{dispatched,3}/{total}] {ConsoleText.Pad(tag, 6)}{loopNote}{shown}{retryNote}{inheritedNote}");
                if (captured is { Length: > 0 } && (options.Echo || tag == "失败" || showDetail))
                    foreach (string detail in captured.Split('\n'))
                        Console.WriteLine("           " + detail.TrimEnd());

                if (tag == "失败" && !options.KeepGoing)
                {
                    report.StoppedAtLine = lineNumber;
                    Console.WriteLine($"停止：第 {lineNumber} 行首次失败（退出码 {firstFailureCode}）；" +
                                      "加 --keep-going 可以继续跑后面的");
                    break;
                }

                if (options.DelayMs > 0 && dispatched < total) Thread.Sleep(options.DelayMs);
            }
        }
        finally
        {
            _running = false;
            ExecutionMode.DryRun = false;
        }

        if (options.DryRun)
            Console.WriteLine($"\n演练结束——共抑制 {ExecutionMode.SuppressedEvents} 个输入事件，什么都没有发送");

        int exitCode = firstFailureCode == 0 ? 0 : firstFailureCode;
        report.ExitCode = exitCode;

        if (options.ReportPath is { } reportPath) WriteReport(reportPath, report);

        if (exitCode == 0)
        {
            if (!options.DryRun)
                Console.WriteLine(report.RetriedCommands == 0
                    ? $"\n全部 {report.Total} 条命令执行成功"
                    : $"\n全部 {report.Total} 条命令执行成功（其中 {report.RetriedCommands} 条靠重试才成功）");
            return 0;
        }

        Console.WriteLine($"\n有 {report.Failed} 条命令失败：");
        foreach (var failure in report.Commands.Where(c => c.ExitCode != 0))
            Console.WriteLine($"  第 {failure.Line} 行：{failure.Command}");
        return exitCode;
    }
}
