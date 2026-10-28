using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace KeyMouse.Runtime;

/// <summary>
/// 批处理语言的解析：一行怎么切成标记、选项怎么读、循环怎么预先配对、
/// 变量怎么校验与替换、等待类子命令怎么解析。
/// </summary>
internal static partial class ScriptRunner
{
    /// <summary>Runs one command with its output captured, so a runner can format it itself.
    /// Shared with the flow runner: a JSON flow is another way to produce command lines, not a
    /// second execution engine.</summary>
    internal static (int ExitCode, string Output) RunOnce(Func<string[], int> execute, List<string> tokens)
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
                case "--allow-restore":
                    options.AllowRestore = true;
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
    /// What the repeat/end blocks of a script look like: which loops enclose each line, and how
    /// many commands the script will dispatch once they are expanded. Analysed in full before
    /// the first command runs, so a malformed loop cannot abort a half-executed script.
    /// </summary>
    internal sealed class LoopPlan
    {
        public LoopPlan(IReadOnlyList<List<(string Name, int Count)>> enclosing, int[] endOf, int total)
        {
            Enclosing = enclosing;
            EndOf = endOf;
            TotalExecutions = total;
        }

        /// <summary>For each line, the loops that enclose it, outermost first.</summary>
        public IReadOnlyList<List<(string Name, int Count)>> Enclosing { get; }

        /// <summary>For each `repeat` line, the index of its matching `end`.</summary>
        public int[] EndOf { get; }

        /// <summary>Commands that will be dispatched once the loops are expanded.</summary>
        public int TotalExecutions { get; }
    }

    internal const int MaxRepeatCount = 1_000_000;
    internal const int MaxExecutions = 100_000;

    internal static LoopPlan AnalyzeLoops(List<(int LineNumber, List<string> Tokens)> commands)
    {
        var enclosing = new List<List<(string Name, int Count)>>(commands.Count);
        var endOf = new int[commands.Count];
        var open = new List<(int Index, string Name, int Count)>();
        long total = 0;

        for (int i = 0; i < commands.Count; i++)
        {
            var (lineNumber, tokens) = commands[i];
            string head = tokens[0].ToLowerInvariant();

            if (head == "repeat")
            {
                var (count, name, error) = ParseRepeat(tokens);
                if (error is not null) throw new CommandFailure(2, $"第 {lineNumber} 行：{error}");
                if (open.Any(frame => frame.Name == name))
                    throw new CommandFailure(2,
                        $"第 {lineNumber} 行：循环变量 '{name}' 与外层同名，里面那个会盖住外面的——" +
                        $"写成 repeat {count} as 另一个名字");

                open.Add((i, name, count));
                enclosing.Add(new List<(string, int)>());
                continue;
            }

            if (head == "end")
            {
                if (tokens.Count != 1)
                    throw new CommandFailure(2, $"第 {lineNumber} 行：'end' 后面不能有参数");
                if (open.Count == 0)
                    throw new CommandFailure(2, $"第 {lineNumber} 行：'end' 没有对应的 repeat");

                (int start, string name, int count) = open[^1];
                open.RemoveAt(open.Count - 1);
                endOf[start] = i;
                enclosing.Add(new List<(string, int)>());
                continue;
            }

            var chain = new List<(string, int)>(open.Count);
            long multiplier = 1;
            foreach (var frame in open)
            {
                chain.Add((frame.Name, frame.Count));
                multiplier *= frame.Count;
            }
            enclosing.Add(chain);
            total += multiplier;

            if (total > MaxExecutions)
                throw new CommandFailure(2,
                    $"循环展开后要执行超过 {MaxExecutions} 条命令（已算到 {total} 条）——请减少 repeat 次数");
        }

        if (open.Count > 0)
            throw new CommandFailure(2,
                $"第 {commands[open[^1].Index].LineNumber} 行：'repeat' 没有对应的 'end'");

        return new LoopPlan(enclosing, endOf, (int)total);
    }

    private static (int Count, string Name, string? Error) ParseRepeat(List<string> tokens)
    {
        if (tokens.Count < 2)
            return (0, "", "repeat 需要一个次数：repeat 5，或 repeat 5 as 名字");

        if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            return (0, "", $"'{tokens[1]}' 不是循环次数（需要一个整数）");
        if (count < 0) return (0, "", "循环次数不能是负数");
        if (count > MaxRepeatCount) return (0, "", $"循环次数最多 {MaxRepeatCount}");

        string name = "i";
        if (tokens.Count > 2)
        {
            if (tokens.Count != 4 || !tokens[2].Equals("as", StringComparison.OrdinalIgnoreCase))
                return (0, "", "写法是 repeat 5 as 名字（as 与名字要么都写，要么都不写）");

            name = tokens[3];
            if (!char.IsLetter(name[0]) && name[0] != '_')
                return (0, "", $"'{name}' 不是合法的变量名（字母或下划线开头）");
            if (!name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                return (0, "", $"'{name}' 不是合法的变量名（只能用字母、数字、下划线）");
        }

        return (count, name, null);
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
    /// Checks every ${name} a script mentions before anything runs: a name must come from
    /// --set, or be a loop variable that encloses that line. Loops are why this cannot simply
    /// be a substitution pass any more - a loop variable only exists inside its body - but the
    /// promise it keeps is the same one: a typo is reported with its line number before the
    /// first command runs, never halfway through a script.
    /// </summary>
    internal static void ValidateVariables(
        List<(int LineNumber, List<string> Tokens)> commands,
        LoopPlan plan,
        IReadOnlyDictionary<string, string> variables)
    {
        for (int i = 0; i < commands.Count; i++)
        {
            var (lineNumber, tokens) = commands[i];
            foreach (string token in tokens)
            {
                foreach (string name in VariableNames(token, lineNumber))
                {
                    if (variables.ContainsKey(name)) continue;
                    if (plan.Enclosing[i].Any(loop => loop.Name == name)) continue;
                    throw new CommandFailure(2,
                        $"第 {lineNumber} 行：变量 '${{{name}}}' 没有定义——用 --set {name}=值 指定");
                }
            }
        }
    }

    private static List<string> VariableNames(string token, int lineNumber)
    {
        var names = new List<string>();
        int i = 0;
        while (true)
        {
            int open = token.IndexOf("${", i, StringComparison.Ordinal);
            if (open < 0) return names;

            int close = token.IndexOf('}', open + 2);
            if (close < 0)
                throw new CommandFailure(2, $"第 {lineNumber} 行：'{token}' 里的 ${{...}} 没有闭合");

            names.Add(token[(open + 2)..close]);
            i = close + 1;
        }
    }

    internal static string Substitute(string token, IReadOnlyDictionary<string, string> variables, int lineNumber)
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
    /// The options that name a target window. A line carrying one sets the script's current
    /// target; a later mouse/key line without one inherits it instead of repeating it.
    /// </summary>
    private static readonly string[] TargetOptions =
    {
        "title", "title-exact", "class", "process", "pid", "hwnd", "pick"
    };

    /// <summary>
    /// Returns the target options of a line (each flag with its value), or null when the line
    /// names no target. Handles both --flag value and --flag=value.
    /// </summary>
    internal static List<string>? ExtractTargetTokens(IReadOnlyList<string> tokens)
    {
        List<string>? found = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if (token.Length < 2 || token[0] != '-') continue;

            string name = token.TrimStart('-');
            int equals = name.IndexOf('=');
            bool inlineValue = equals >= 0;
            if (inlineValue) name = name[..equals];
            if (!TargetOptions.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

            found ??= new List<string>();
            found.Add(token);
            if (!inlineValue && i + 1 < tokens.Count) found.Add(tokens[++i]);
        }

        return found;
    }

    /// <summary>
    /// Commands that may fall back to the script's current target: the ones that act on a
    /// window the script already named. `waitfor` / `waitgone` are deliberately excluded -
    /// which window you are waiting for is a statement worth making explicitly - and so is
    /// `window list`, which describes every window and would be narrowed by inheritance.
    /// </summary>
    internal static bool InheritsTarget(IReadOnlyList<string> tokens) =>
        tokens.Count > 0 && tokens[0].ToLowerInvariant() is "mouse" or "key" or "keyboard";

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
        Commands.GlobalOptions global;
        try
        {
            rest = Commands.ExtractGlobalOptions(tokens.Skip(1).ToArray(), out global);
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
}

