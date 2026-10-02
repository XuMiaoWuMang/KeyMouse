using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KeyMouse;

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
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);
}

internal sealed class CommandRecord
{
    public int Index { get; set; }
    public int Line { get; set; }
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
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
internal static class ScriptRunner
{
    private static bool _running;

    public static int Run(string[] args, Program.GlobalOptions global, Func<string[], int> execute)
    {
        if (_running)
            return Fail(2, "run：不允许脚本里再跑脚本（拒绝嵌套）");

        if (global.HasSelector || global.Wx.HasValue || global.Wy.HasValue || global.Pick is not null)
            return Fail(2, "run：窗口选择器要写在每一行命令上，不能挂在 run 自己身上");

        var options = new ScriptOptions();
        if (ParseOptions(args, options) is { } optionError) return Fail(2, optionError);
        if (options.Path is null)
            return Fail(2, "用法：KeyMouse run <文件|-> [--delay 毫秒] [--keep-going] [--dry-run] [--echo] " +
                           "[--retry 次数] [--retry-delay 毫秒] [--set 名=值] [--report 文件.json]");

        List<(int LineNumber, List<string> Tokens)> commands;
        try
        {
            commands = Expand(Parse(ReadScript(options.Path)), options.Variables);
            ValidatePseudoCommands(commands);
        }
        catch (CommandFailure ex)
        {
            return Fail(ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(1, $"读不到脚本 '{options.Path}'：{ex.Message}");
        }

        if (commands.Count == 0)
            return Fail(2, $"脚本 '{options.Path}' 里没有任何命令");

        string source = options.Path == "-" ? "stdin" : Path.GetFullPath(options.Path);
        var report = new ScriptReport
        {
            Script = source,
            StartedAt = DateTimeOffset.Now.ToString("O"),
            DryRun = options.DryRun,
            Total = commands.Count
        };

        int firstFailureCode = 0;
        int maxAttempts = 1 + Math.Max(0, options.Retry);

        ExecutionMode.DryRun = options.DryRun;
        _running = true;
        try
        {
            Console.WriteLine(options.DryRun
                ? $"演练模式：{source} 共 {commands.Count} 条命令——资格检查照常执行，一个字节都不会发"
                : $"{source} 共 {commands.Count} 条命令");

            int total = commands.Count;
            for (int i = 0; i < total; i++)
            {
                var (lineNumber, tokens) = commands[i];
                string shown = string.Join(' ', tokens.Select(Quote));
                var record = new CommandRecord { Index = i + 1, Line = lineNumber, Command = shown };
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
                        Console.WriteLine($"[{i + 1,3}/{total}] 第 {record.Attempts} 次重试（共 {maxAttempts - 1} 次），" +
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
                Console.WriteLine($"[{i + 1,3}/{total}] {ConsoleText.Pad(tag, 6)}{shown}{retryNote}");
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

                if (options.DelayMs > 0 && i < total - 1) Thread.Sleep(options.DelayMs);
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

    /// <summary>Runs one command with its output captured, so the runner can format it itself.</summary>
    private static (int ExitCode, string Output) RunOnce(Func<string[], int> execute, List<string> tokens)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        TextWriter oldOut = Console.Out;
        TextWriter oldErr = Console.Error;
        int code;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            code = execute(tokens.ToArray());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
        return (code, (stdout.ToString() + stderr.ToString()).Trim());
    }

    // -------------------------------------------------------------- options

    internal static string? ParseOptions(string[] args, ScriptOptions options)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? Value(string what)
            {
                if (i + 1 >= args.Length) return null;
                return args[++i];
            }

            switch (arg.ToLowerInvariant())
            {
                case "--delay":
                {
                    if (!int.TryParse(Value("毫秒"), out int delay) || delay < 0)
                        return "--delay 需要一个非负的毫秒数";
                    options.DelayMs = delay;
                    break;
                }
                case "--retry":
                {
                    if (!int.TryParse(Value("次数"), out int retry) || retry < 0)
                        return "--retry 需要一个非负的次数";
                    options.Retry = retry;
                    break;
                }
                case "--retry-delay":
                {
                    if (!int.TryParse(Value("毫秒"), out int retryDelay) || retryDelay < 0)
                        return "--retry-delay 需要一个非负的毫秒数";
                    options.RetryDelayMs = retryDelay;
                    break;
                }
                case "--report":
                    options.ReportPath = Value("路径");
                    if (options.ReportPath is null) return "--report 需要一个文件路径";
                    break;
                case "--set":
                {
                    string? assignment = Value("名=值");
                    if (assignment is null) return "--set 需要 名=值";
                    int eq = assignment.IndexOf('=');
                    if (eq <= 0) return $"--set 的格式是 名=值（收到 '{assignment}'）";
                    options.Variables[assignment[..eq]] = assignment[(eq + 1)..];
                    break;
                }
                case "--keep-going" or "-k":
                    options.KeepGoing = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--echo":
                    options.Echo = true;
                    break;
                default:
                    if (arg.Length > 1 && arg[0] == '-') return $"run：未知选项 '{arg}'";
                    if (options.Path is not null) return "run：只能给一个脚本文件";
                    options.Path = arg;
                    break;
            }
        }
        return null;
    }

    // -------------------------------------------------------------- parsing

    internal static List<(int LineNumber, List<string> Tokens)> Parse(string[] lines)
    {
        var commands = new List<(int, List<string>)>();
        for (int i = 0; i < lines.Length; i++)
        {
            List<string> tokens = Tokenize(lines[i]);
            if (tokens.Count == 0) continue;

            // tolerate lines copied straight out of the README
            if (tokens[0].Equals("KeyMouse", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("KeyMouse.exe", StringComparison.OrdinalIgnoreCase))
            {
                tokens.RemoveAt(0);
                if (tokens.Count == 0) continue;
            }

            commands.Add((i + 1, tokens));
        }
        return commands;
    }

    /// <summary>
    /// Splits one line into argv. Supports "double quoted" tokens, \" and \\ escapes,
    /// '#' comments (whole line or trailing) and ignores blank lines.
    /// </summary>
    internal static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length || line[i] == '#') break;

            var token = new StringBuilder();
            bool inQuotes = false;
            while (i < line.Length)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '\\'))
                {
                    token.Append(line[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    i++;
                    continue;
                }
                if (!inQuotes && (char.IsWhiteSpace(c) || c == '#')) break;
                token.Append(c);
                i++;
            }
            if (inQuotes) throw new CommandFailure(2, $"引号没有闭合：{line.Trim()}");
            tokens.Add(token.ToString());
        }
        return tokens;
    }

    /// <summary>
    /// Replaces ${name} inside already-tokenised arguments. Substitution happens per token,
    /// so a script that writes --title "${title}" keeps a value with spaces as one argument.
    /// </summary>
    internal static List<(int LineNumber, List<string> Tokens)> Expand(
        List<(int LineNumber, List<string> Tokens)> commands,
        IReadOnlyDictionary<string, string> variables)
    {
        // Always run the pass, even with no variables defined: a script that uses ${x}
        // without --set x must fail here with its line number, not leak a raw ${x} into
        // the command and produce a confusing downstream error.
        var expanded = new List<(int, List<string>)>(commands.Count);
        foreach (var (lineNumber, tokens) in commands)
        {
            var result = new List<string>(tokens.Count);
            foreach (string token in tokens) result.Add(Substitute(token, variables, lineNumber));
            expanded.Add((lineNumber, result));
        }
        return expanded;
    }

    private static string Substitute(string token, IReadOnlyDictionary<string, string> variables, int lineNumber)
    {
        int search = token.IndexOf("${", StringComparison.Ordinal);
        if (search < 0) return token;

        var result = new StringBuilder();
        int i = 0;
        while (i < token.Length)
        {
            int open = token.IndexOf("${", i, StringComparison.Ordinal);
            if (open < 0)
            {
                result.Append(token, i, token.Length - i);
                break;
            }
            int close = token.IndexOf('}', open + 2);
            if (close < 0)
                throw new CommandFailure(2, $"第 {lineNumber} 行：'{token}' 里的 ${{...}} 没有闭合");

            result.Append(token, i, open - i);
            string name = token[(open + 2)..close];
            if (!variables.TryGetValue(name, out string? value))
                throw new CommandFailure(2,
                    $"第 {lineNumber} 行：变量 '${{{name}}}' 没有定义——用 --set {name}=值 指定");
            result.Append(value);
            i = close + 1;
        }
        return result.ToString();
    }

    /// <summary>
    /// `sleep` and the wait pseudo-commands are validated before anything runs, so a typo
    /// cannot abort a half-executed script.
    /// </summary>
    private static void ValidatePseudoCommands(List<(int LineNumber, List<string> Tokens)> commands)
    {
        foreach (var (lineNumber, tokens) in commands)
        {
            if (tokens[0].Equals("sleep", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count != 2 || !int.TryParse(tokens[1], out int ms) || ms < 0)
                    throw new CommandFailure(2,
                        $"第 {lineNumber} 行：'sleep' 只接受一个非负的毫秒数");
                continue;
            }

            if (tokens[0].Equals("waitfor", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("waitgone", StringComparison.OrdinalIgnoreCase))
            {
                var (_, _, _, error) = ParseWait(tokens);
                if (error is not null) throw new CommandFailure(2, $"line {lineNumber}: {error}");
            }
        }
    }

    /// <summary>
    /// Parses `waitfor &lt;selector&gt; [--timeout MS] [--interval MS]`. The selector uses the
    /// same global options as every other command, resolved through the same gate.
    /// </summary>
    internal static (WindowSelector? Selector, int TimeoutMs, int IntervalMs, string? Error) ParseWait(
        List<string> tokens)
    {
        string name = tokens[0];
        string[] rest;
        Program.GlobalOptions global;
        try
        {
            rest = Program.ExtractGlobalOptions(tokens.Skip(1).ToArray(), out global);
        }
        catch (ArgumentException ex)
        {
            return (null, 0, 0, $"{name}: {ex.Message}");
        }

        if (!global.HasSelector)
            return (null, 0, 0, $"{name}：需要一个窗口选择器（--title/--class/--process/--pid/--hwnd）");

        int timeout = 5000, interval = 200;
        for (int i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--timeout":
                    if (i + 1 >= rest.Length || !int.TryParse(rest[++i], out timeout) || timeout < 0)
                        return (null, 0, 0, $"{name}：--timeout 需要一个非负的毫秒数");
                    break;
                case "--interval":
                    if (i + 1 >= rest.Length || !int.TryParse(rest[++i], out interval) || interval <= 0)
                        return (null, 0, 0, $"{name}：--interval 需要一个正的毫秒数");
                    break;
                default:
                    return (null, 0, 0, $"{name}：多余的参数 '{rest[i]}'");
            }
        }

        return (global.ToSelector(), timeout, interval, null);
    }

    /// <summary>Polls until the condition holds or the timeout expires. Zero timeout = check once.</summary>
    private static (bool Satisfied, long ElapsedMs) WaitUntil(Func<bool> satisfied, int timeoutMs, int intervalMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            if (satisfied()) return (true, stopwatch.ElapsedMilliseconds);
            long remaining = timeoutMs - stopwatch.ElapsedMilliseconds;
            if (remaining <= 0) return (false, stopwatch.ElapsedMilliseconds);
            Thread.Sleep((int)Math.Min(intervalMs, remaining));
        }
    }

    /// <summary>
    /// Only failures that provably sent nothing may be retried: 3 (selector), 4 (not usable),
    /// 5 (focus verification). A retry can never repeat a half-executed action.
    /// </summary>
    internal static bool IsRetryable(int exitCode) => exitCode is 3 or 4 or 5;

    private static string[] ReadScript(string path)
    {
        byte[] bytes = path == "-" ? ReadStdin() : File.ReadAllBytes(path);

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new CommandFailure(2, "脚本不是合法的 UTF-8——请另存为 UTF-8" +
                                        "（ANSI/GBK 无法解码，中文必须用 UTF-8）");
        }

        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static byte[] ReadStdin()
    {
        using Stream stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void WriteReport(string path, ScriptReport report)
    {
        try
        {
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Keep CJK window titles and quotes readable instead of \uXXXX escapes.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
            Console.WriteLine($"报告已写入 {Path.GetFullPath(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The script's own result stays authoritative; a broken report path must not hide it.
            Console.Error.WriteLine($"错误：写不了报告 '{path}'：{ex.Message}");
        }
    }

    private static string Quote(string token) =>
        token.Length == 0 || token.Any(ch => char.IsWhiteSpace(ch) || ch == '#') ? $"\"{token}\"" : token;

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine("错误：" + message);
        return code;
    }
}
